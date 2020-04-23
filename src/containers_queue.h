#pragma once
#include <boost/circular_buffer.hpp>
#include <boost/lockfree/queue.hpp>
#include <cassert>
#include <queue>

template <typename TValue> class GrowingDeque
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

    template <class... TArgs> void emplace_back(TArgs &&... args) { cont.emplace_back(std::forward<TArgs>(args)...); }
};

template <typename Value> class counted_lockfree_queue : private boost::lockfree::queue<Value>
{
    using parent = boost::lockfree::queue<Value>;
    std::atomic<int> count = 0;

  public:
    counted_lockfree_queue(int capacity) : parent(capacity) {}
    bool pop(Value & val)
    {
        count.fetch_sub(1, std::memory_order_relaxed);
        return parent::pop(val);
    }
    bool push(const Value & val)
    {
        count.fetch_add(1, std::memory_order_relaxed);
        return parent::push(val);
    }
    size_t size() { return count; }
};

template <typename Value> class queue_adaptor : private std::queue<Value>
{
    using parent = std::queue<Value>;

  public:
    queue_adaptor() : parent() {}
    bool pop(Value & val)
    {
        val = parent::front();
        parent::pop();
        return true;
    }
    bool push(const Value & val)
    {
        parent::push(val);
        return true;
    }
    size_t size() { return parent::size(); }
};

template <typename Value> struct circular_buffer_adaptor : private boost::circular_buffer<Value>
{
    using parent = boost::circular_buffer<Value>;
    circular_buffer_adaptor(int capacity) : parent(capacity) {}
    bool pop(Value & val)
    {
        val = parent::front();
        parent::pop_front();
        return true;
    }
    Value & peek() { return parent::front(); }
    bool push(const Value & val)
    {
        parent::push_back(val);
        return true;
    }
    size_t size() { return parent::size(); }
};
