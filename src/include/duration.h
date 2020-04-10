#pragma once
#include <cstdint>
#include <chrono>

/*
 * Duration: represents duration of things, decremented once per frame by AdvanceStep
 *           frame-independent
 */
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

/*
 * RepetitiveTimer: used for triggering things that should happen every X milliseconds
 *                  automatically restarted once cooldown elases
 *                  frame-independent
 */
class RepetitiveTimer
{
    Duration cooldown;
    const duration_t interval;
  public:
    RepetitiveTimer(duration_t timer_cooldown) : cooldown(timer_cooldown), interval(timer_cooldown) {}

    bool Ready() const { return this->cooldown.Finished(); }
    bool AdvanceAndCheckElapsed()
    {
        /* If we finished last frame, restart the timer */
        if (Ready())
            cooldown = Duration{interval};

        --this->cooldown;
        /* Return true if we are now ready*/
        return Ready();
    }
};