#pragma once

#pragma once
#include <deque>
#include <debugapi.h>

// Effective container for storing in-place, cache-local objects - deleting does not shift, dead objects can be reused later
//  and references to live objects are valid forever.
// We risk fragmentation after long use but let's have this as an experiment how to reduce the number of dynamic allocations
//  to minimum.
// Iterator and foreach support - skips over dead elements 
template <typename T>
concept Invalidable = requires(T t) { { t.IsInvalid() } -> bool; { t.Invalidate() }; { T::Invalid() }->T; };

template <Invalidable TElement>
class ValueContainer
{
	using Container = std::deque<TElement>;  // As long we grow/shrink on start/end, element references are valid forever

	// Various ways to get to IsInvalid - value type vs. pointer type
	template <typename TArgument, std::enable_if_t<std::is_class<TArgument>::value, int> = 0>
	static bool IsInvalid(TArgument val) { return val.IsInvalid(); }
	//template <typename TArgument, std::enable_if_t<std::is_reference<TArgument>::value, int> = 0>
	//static bool IsInvalid(TArgument val) { return val.IsInvalid(); }
	//template <typename TArgument, std::enable_if_t<std::is_pointer<TArgument>::value, int> = 0>
	//static bool IsInvalid(TArgument val) { return val->IsInvalid(); }
public:
	class iterator
	{
		Container* container;
		typename Container::iterator it;
	public:
		iterator(Container& container, typename Container::iterator it) : container(&container), it(it) {}
		typename TElement& operator*() const { return *it; }
		bool operator!= (iterator other) { return it != other.it; }
		iterator operator++() // prefix increment
		{	// Advance over dead elements
			while (++it != container->end() && ValueContainer::IsInvalid(*it)) {};
			return *this;
		}
	};

	Container container;
public:
	/* Size and lookup */
	auto Size() { return container.size(); };
	TElement& operator[](int index) { return container[index]; } 

	/* In-place forwarding construction avoiding any copy. Must use if you want to use [this] in your constructor or if your item in non-copyable */
	template <typename... ConstructionArgs>
	TElement& ConstructElement(ConstructionArgs&&... args)
	{
		auto dead_item = std::find_if(container.begin(), container.end(), [this](auto val) { return IsInvalid(val); });

		/* Find if we can insert into already allocated space */
		if (dead_item != container.end()) {
			/* Manually destroy old and in-place construct new */
			(*dead_item).~TElement();
			new (&*dead_item) TElement(std::forward<ConstructionArgs>(args)...);
			return *dead_item;
		}

		/* If not, we need to grow with an invalid item, then in-place construct it */
		TElement& new_alloc = container.emplace_back(TElement::Invalid());
		/* Now we got the real, final address. In-place construct it and return reference */
		new_alloc.~TElement(); /* Destroy placeholder */
		new (&new_alloc) TElement(std::forward<ConstructionArgs>(args)...);
		return new_alloc;
	}
	/* Copying construction */
	TElement& Add(TElement item)
	{
		/* Place over a dead item by assignment if such dead item exists. Otherwise append to back. */
		auto dead_item = std::find_if(container.begin(), container.end(), [this](auto val) { return IsInvalid(val); });
		if (dead_item != container.end()) {
			*dead_item = item;
			return *dead_item;
		}
		return container.emplace_back(item);
	}
	void Remove(TElement& item)
	{	// O(n) search is not really needed here. Just flag it destroyed and it will be recycled later, maybe.
		item.Invalidate();
	}
	void Shrink()
	{	// Slice out dead objects on the beginning and end of deque. Don't invalidate references.
		auto size_before = container.size();
	    while (!container.empty() && ValueContainer::IsInvalid(container.front()))
			container.pop_front();
		while (!container.empty() && ValueContainer::IsInvalid(container.back()))
			container.pop_back();

		if (container.size() != size_before && size_before >= 50) {
			char buff[50];
			std::sprintf(buff, "Shrunk %zd items, now size: %zd\r\n", size_before - container.size(), container.size());
			OutputDebugString(buff);
		}
	}

	iterator begin()
	{	
		auto it = iterator(container, container.begin());
		// Skip also dead elements at the start
		while (it != end() && ValueContainer::IsInvalid(*it))
			++it;
		return it;
	}
	iterator end() { return iterator(container, container.end()); }
};