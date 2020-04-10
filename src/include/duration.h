#pragma once
#include <cstdint>
#include <chrono>

/* Force duration to be always of this class, don't mix various integers */
using duration_t = std::chrono::microseconds;
class Duration
{

    duration_t value;
public:
    constexpr Duration() : value(0) {}
    constexpr explicit Duration(duration_t value) : value(value) {}

    static Duration Zero() { return Duration(); }
    bool Finished() const { return this->value.count() <= 0; }

    /* Decrement by AdvanceStep */
    duration_t & operator--();
};
