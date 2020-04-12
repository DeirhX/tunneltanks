#pragma once

#include <deque>
#include <queue>
#include <trace.h>
#include <vector>

// Effective container for storing in-place, cache-local objects - deleting does not shift, dead objects can be reused
// later
//  and references to live objects are valid forever.
// We risk fragmentation after long use but let's have this as an experiment how to reduce the number of dynamic
// allocations
//  to minimum.
// Iterator and foreach support - skips over dead elements
template <typename T>
concept Invalidable = requires(T t)
{
    {
        t.IsInvalid()
    }
    ->bool;
    {t.Invalidate()};
};

template <Invalidable TElement>
class ValueContainer
{
    using Container = std::deque<TElement>; // As long we grow/shrink on start/end, element references are valid forever

    // Various ways to get to IsInvalid - value type vs. pointer type
    template <typename TArgument, std::enable_if_t<std::is_class<TArgument>::value, int> = 0>
    static bool IsInvalid(TArgument val)
    {
        return val.IsInvalid();
    }
    // template <typename TArgument, std::enable_if_t<std::is_reference<TArgument>::value, int> = 0>
    // static bool IsInvalid(TArgument val) { return val.IsInvalid(); }
    // template <typename TArgument, std::enable_if_t<std::is_pointer<TArgument>::value, int> = 0>
    // static bool IsInvalid(TArgument val) { return val->IsInvalid(); }
  public:
    using ItemType = TElement;
    class iterator
    {
        Container * container;
        size_t index;

      public:
        iterator(Container & container, size_t index) : container(&container), index(index) {}
        typename TElement & operator*() const { return (*container)[index]; }
        bool operator!=(iterator other) { return index != other.index; }
        iterator operator++() // prefix increment
        {                     // Advance over dead elements
            while (++index < container->size() && ValueContainer::IsInvalid((*container)[index]))
            {
            };
            return *this;
        }
    };

    Container container;

  public:
    /* Size and lookup */
    auto Size() { return container.size(); };
    TElement & operator[](int index) { return container[index]; }

    /* In-place forwarding construction avoiding any copy. Must use if you want to use [this] in your constructor or if
     * your item in non-copyable */
    template <typename... ConstructionArgs>
    TElement & ConstructElement(ConstructionArgs &&... args)
    {
        auto dead_item = std::find_if(container.begin(), container.end(), [this](auto val) { return IsInvalid(val); });

        /* Find if we can insert into already allocated space */
        if (dead_item != container.end())
        {
            /* Manually destroy old and in-place construct new */
            (*dead_item).~TElement();
            new (&*dead_item) TElement(std::forward<ConstructionArgs>(args)...);
            return *dead_item;
        }

        /* If not, we need to grow with an invalid item, then in-place construct it */
        TElement & new_alloc = container.emplace_back(std::forward<ConstructionArgs>(args)...);
        return new_alloc;
    }
    /* Copying construction */
    TElement & Add(TElement item)
    {
        /* Place over a dead item by assignment if such dead item exists. Otherwise append to back. */
        auto dead_item = std::find_if(container.begin(), container.end(), [this](auto val) { return IsInvalid(val); });
        if (dead_item != container.end())
        {
            *dead_item = item;
            return *dead_item;
        }
        return container.emplace_back(item);
    }
    /* Merge two containers together */
    // TODO: Add const correctness, const iterators
    void MergeFrom(ValueContainer & other)
    {
        for (const auto & value : other)
            this->Add(value);
    }

    void Remove(TElement & item) { item.Invalidate(); }
    void RemoveAll()
    { // O(n) search is not really needed here. Just flag it destroyed and it will be recycled later, maybe.
        for (auto & el : this->container)
            el.Invalidate();
        this->container.clear();
    }
    void Shrink()
    { // Slice out dead objects on the beginning and end of deque. Don't invalidate references.
        auto size_before = container.size();
        while (!container.empty() && ValueContainer::IsInvalid(container.front()))
            container.pop_front();
        while (!container.empty() && ValueContainer::IsInvalid(container.back()))
            container.pop_back();

        if (container.size() != size_before && size_before >= 50)
        {
            DebugTrace<5>("Shrunk %zd items, now size: %zd\r\n", size_before - container.size(), container.size());
        }
    }

    iterator begin()
    {
        auto it = iterator(container, 0);
        // Skip also dead elements at the start
        while (it != end() && ValueContainer::IsInvalid(*it))
            ++it;
        return it;
    }
    iterator end() { return iterator(container, container.size()); }
    // TODO: add cbegin cend
};

/* List of containers for arbitrary number of types, hiding it under a unified interface of a single list
 *  Use for storing heterogeneous data without the performance penalties of pointer indirection and virtual calls */
template <typename... TValues>
class MultiTypeContainer
{
  private:
    std::tuple<ValueContainer<TValues>...> items;
  private:
    MultiTypeContainer(const MultiTypeContainer &) = delete; /* Do not allow implicit copy. It's most likely a mistake. */
  public:
    MultiTypeContainer() = default;

    template <typename TValue>
    TValue & Add(const TValue & item)
    {
        using Typo = typename ValueContainer<TValue>::ItemType;
        return std::get<ValueContainer<TValue>>(this->items).Add(item);
    }
    // TODO: Add const correctness
    void MergeFrom(MultiTypeContainer & other)
    {
        std::apply(
            [&other](auto &&... cont) {
                (..., cont.MergeFrom(other.GetContainer(cont))); /* TODO: No idea how to extract type from [cont]. Passing as parameter as a workaround */
            },
            items);
    }
    /* In-place construction of element, TValue has to be specified */
    template <typename TValue, typename... ConstructionArgs>
    TValue & ConstructElement(ConstructionArgs &&... args)
    {
        return GetContainer<TValue>().ConstructElement(std::forward<ConstructionArgs>(args)...);
    }
    template <typename TValue>
    void Remove(TValue & item)
    {
        return GetContainer<TValue>().Remove(item);
    }
    void RemoveAll()
    {
        std::apply([](auto &... cont) { (..., cont.RemoveAll()); }, items);
    }
    void Shrink()
    {
        std::apply([](auto &... cont) { (..., cont.Shrink()); }, items);
    }
    /* Call visitor on every collection contained */
    template <typename TVisit>
    void ForEachContainer(TVisit visitor)
    {
        auto for_each_container = [visitor](auto & container) { visitor(container); };
        std::apply([for_each_container](auto &... cont) { (..., for_each_container(cont)); }, items);
    }

    /* Call visitor on every element contained, irrespective of its type */
    template <typename TVisit>
    void ForEach(TVisit visitor)
    {
        auto for_each_element = [visitor](auto & container) {
            for (auto & el : container)
                visitor(el);
        };
        std::apply([for_each_element](auto &... cont) { (..., for_each_element(cont)); }, items);
    }
    /* Call visitor on all elements of a supplied type TValue */
    template <typename TVisit, typename TValue>
    void ForEach(TVisit visitor)
    {
        auto for_each_element = [visitor](auto & container) {
            for (auto & el : container)
                visitor(el);
        };
        for_each_element(std::get<ValueContainer<TValue>>(items));
    }
    /* Number of items in collection, irrespective on type */
    size_t Size()
    {
        size_t total_size = 0;
        auto for_each_element = [&total_size](auto & container) { total_size += container.size(); };
        std::apply([for_each_element](auto &... cont) { (..., for_each_element(cont)); }, items);
        return total_size;
    };

  private:
    /* Get container for a specified type */
    template <typename TValue>
    ValueContainer<TValue> & GetContainer()
    {
        return std::get<ValueContainer<TValue>>(this->items);
    }
    template <typename TValue>
    ValueContainer<TValue> & GetContainer(const ValueContainer<TValue> & /* Deduction only */) 
    {
        return std::get<ValueContainer<TValue>>(this->items);
    }
    /* Get container for a specified type */
    template <typename TValue>
    void MergeContainers(const ValueContainer<TValue> & other)
    {
        GetContainer<TValue>(this->items).MergeFrom(other);
    }
};

/*
 * Container for raw level data
 */
template <typename ValueType>
class Container2D
{
    using Container = std::vector<ValueType>;
    Container array;
    Size size;
  public:
    Container2D(Size size) : array(size.x * size.y), size(size) {}
    Container2D(const Container2D &) = delete; /* Expensive to copy, don't do it unintentionally */

    ValueType & operator[](int i) { return array[i]; }
    const ValueType & operator[](int i) const { return array[i]; }
    ValueType & operator[](Position pos) { return array[pos.x + pos.y * this->size.x]; }
    const ValueType & operator[](Position pos) const { return array[pos.x + pos.y * this->size.x]; }

    typename Container::iterator begin() { return array.begin(); }
    typename Container::iterator end() { return array.end(); }
    typename Container::const_iterator cbegin() const { return array.cbegin(); }
    typename Container::const_iterator cend() const { return array.cend(); }
};