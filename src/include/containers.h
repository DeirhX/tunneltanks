#pragma once

#pragma once
#include <queue>
#include <deque>
#include <trace.h>
#include <vector>
#include <boost/circular_buffer.hpp>
#include <boost/lockfree/queue.hpp>

// Effective container for storing in-place, cache-local objects - deleting does not shift, dead objects can be reused later
//  and references to live objects are valid forever.
// We risk fragmentation after long use but let's have this as an experiment how to reduce the number of dynamic allocations
//  to minimum.
// Iterator and foreach support - skips over dead elements 
template <typename T>
concept Invalidable = requires(T t) { { t.IsInvalid() } -> bool; { t.Invalidate() }; };

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
		size_t index;
	public:
		iterator(Container& container, size_t index) : container(&container), index(index) {}
		typename TElement& operator*() const { return (*container)[index]; }
		bool operator!= (iterator other) { return index != other.index; }
		iterator operator++() // prefix increment
		{	// Advance over dead elements
			while (++index < container->size() && ValueContainer::IsInvalid((*container)[index])) {};
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
		TElement& new_alloc = container.emplace_back(std::forward<ConstructionArgs>(args)...);
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
			DebugTrace<6>("Shrunk %zd items, now size: %zd\r\n", size_before - container.size(), container.size());
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
};


template <typename TValue>
class GrowingDeque
{
	using Container = std::vector<TValue>;
	Container cont;
	int front_index = 0;
public:
	GrowingDeque(int reservedCapacity = 0)
	{
		if (reservedCapacity)
			cont.reserve(reservedCapacity);
	}
	
	TValue pop_front()
	{
		assert(front_index < size());
		TValue val = cont[front_index];
		++front_index;
		return val;
	}

	size_t size() { return cont.size(); }
	void push_back(TValue val) { cont.push_back(val); }

	template <class... TArgs>
	void emplace_back(TArgs&&... args)
	{
		cont.emplace_back(std::forward<TArgs>(args)...);
	}
};

template <typename Value>
class counted_lockfree_queue : private boost::lockfree::queue<Value> {
	using parent = boost::lockfree::queue<Value>;
	std::atomic<int> count = 0;
public:
	counted_lockfree_queue(int capacity) : parent(capacity) {}
	bool pop(Value& val) {
		count.fetch_sub(1, std::memory_order::memory_order_relaxed);
		return parent::pop(val);
	}
	bool push(const Value& val) {
		count.fetch_add(1, std::memory_order::memory_order_relaxed);
		return parent::push(val);
	}
	size_t size() {
		return count;
	}
};

template <typename Value>
class queue_adaptor : private std::queue<Value> {
	using parent = std::queue<Value>;
public:
	queue_adaptor() : parent() {}
	bool pop(Value& val) {
		val = parent::front();
		parent::pop();
		return true;
	}
	bool push(const Value& val) {
		parent::push(val);
		return true;
	}
	size_t size() {
		return parent::size();
	}
};

template <typename Value>
struct circular_buffer_adaptor : private boost::circular_buffer<Value> {
	using parent = boost::circular_buffer<Value>;
	circular_buffer_adaptor(int capacity) : parent(capacity) {}
	bool pop(Value& val) {
		val = parent::front();
		parent::pop_front();
		return true;
	}
	Value& peek() {
		return parent::front();
	}
	bool push(const Value& val) {
		parent::push_back(val);
		return true;
	}
	size_t size() {
		return parent::size();
	}
};
