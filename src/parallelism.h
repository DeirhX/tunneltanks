#pragma once
#include "random.h"
#include "tweak.h"
#include <future>
#include <type_traits>
#include <vector>

struct PhysicalCores
{
};

struct WorkerCount
{
    unsigned int worker_count;
    WorkerCount() : worker_count(tweak::perf::parallelism_degree) {}
    WorkerCount(int workers) : worker_count(std::max(1, workers)) {}
    WorkerCount(PhysicalCores) : WorkerCount() {}
    operator int() const { return worker_count; }
};

struct WorkerPercent : WorkerCount
{
    WorkerPercent(unsigned int percent) : WorkerCount(tweak::perf::parallelism_degree * percent / 100) {}
};

struct WorkerDivisor : WorkerCount
{
    WorkerDivisor(unsigned int divisor) : WorkerCount(tweak::perf::parallelism_degree / divisor) {}
};

struct ThreadLocal
{
    RandomGenerator random;

    ThreadLocal() { random = ::Random; } // Copy state
};

template <typename Func> /* void(int first, int last)  */
auto parallel_for(Func func, int minimum, int maximum, WorkerCount worker_count = {})
    -> std::invoke_result_t<Func, int, int, ThreadLocal *>
{
    /* Parallelize the process using std::async and slicing jobs */
    auto threadLocals = std::vector<ThreadLocal>();
    auto tasks = std::vector<std::future<int>>();
    threadLocals.reserve(worker_count);
    tasks.reserve(worker_count);

    int curr = minimum;
    for (int i = 0; i < worker_count; ++i)
    {
        if (curr <= maximum)
        {
            int until = curr + (maximum - minimum) / worker_count;
            threadLocals.emplace_back();
            tasks.emplace_back(std::async(std::launch::async, func, curr, std::min(maximum, until), &threadLocals[i]));
            curr = until + 1;
        }
    }
    /* Wait for everything done and sum the results */
    auto result = std::invoke_result_t<Func, int, int, ThreadLocal *>{}; //std::result_of<Func>::type{};
    for (auto & task : tasks)
    {
        result += task.get();
    }
    return result;
};
