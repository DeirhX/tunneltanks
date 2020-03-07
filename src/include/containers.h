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
concept Invalidable = requires { { true } -> bool; };

template <Invalidable TElement>
class ValueContainer
{
	using Container = std::deque<TElement>;  // As long we grow/shrink on start/end, element references are valid forever

	// Various ways to get to IsDestroyed - value type vs. pointer type
	template <typename T, typename std::enable_if_t<!std::is_pointer<T>::value> * = 0>
	static bool IsDestroyed(T val) { return val.IsDestroyed(); }
	template <typename T, typename std::enable_if_t<std::is_pointer<T>::value> * = 0>
	static bool IsDestroyed(T val) { return val->IsDestroyed(); }

	class iterator
	{
		Container& container;
		typename Container::iterator it;
	public:
		iterator(Container& container, typename Container::iterator it) : container(container), it(it) {}
		TElement& operator*() const { return *it; }
		bool operator!= (iterator other) { return it != other.it; }
		iterator operator++() // prefix increment
		{	// Advance over dead elements
			while (++it != container.end() && IsDestroyed(*it)) {};
			return *this;
		}
	};

	Container container;
public:
	auto Size() { return container.size(); };
	TElement& operator[](int index) { return container[index]; } 

	TElement& Add(TElement item)
	{
		auto& dead_item = std::find_if(container.begin(), container.end(), [this](auto val) { return IsDestroyed(val); });
		if (dead_item != container.end()) {
			*dead_item = item;
			return *dead_item;
		}
		return container.emplace_back(item);
	}
	void Remove(TElement& item)
	{	// O(n) search is not really needed here. Just flag it destroyed and it will be recycled later, maybe.
		item.Destroy();
	}
	void Shrink()
	{	// Slice out dead objects on the beginning and end of deque. Don't invalidate references.
		auto size_before = container.size();
	    while (!container.empty() && ValueContainer::IsDestroyed(container.front()))
			container.pop_front();
		while (!container.empty() && ValueContainer::IsDestroyed(container.back()))
			container.pop_back();

		if (container.size() != size_before) {
			OutputDebugString(std::printf("Shrunk %d items, now size: %d", size_before - container.size(), container.size()));
		}
	}

	iterator begin()
	{	// Skip dead elements at the start
		auto it = iterator(container, container.begin());
		while (it != end() && IsDestroyed(*it))
			++it;
		return it;
	}
	iterator end() { return iterator(container, container.end()); }
};