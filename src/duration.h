#pragma once
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
class Timer
{
    Duration cooldown;
    duration_t interval;
    bool auto_reset = false;
    bool is_running = false;
  public:
    Timer(duration_t timer_cooldown, bool auto_reset) : cooldown(timer_cooldown), interval(timer_cooldown), auto_reset(auto_reset), is_running(auto_reset) {}

    bool Ready() const { return this->cooldown.Finished(); }
    bool IsRunning() const { return this->is_running; }
    bool AdvanceAndCheckElapsed()
    {
        if (!this->is_running)
            return false;

        /* If we finished last frame, restart the timer */
        if (Ready() && this->auto_reset)
        {
            Reset();
        }

        --this->cooldown;

        /* Return true if we are now ready*/
        bool is_done = Ready();
        if (is_done && !this->auto_reset)
            Stop();
        return is_done;
    }
    void Start() { this->is_running = true; }
    void Stop() { this->is_running = false; }
    void Restart()
    {
        Reset();
        Start();
    }
    void Restart(duration_t new_duration)
    {
        Reset(new_duration);
        Start();
    }
    void Reset() { this->cooldown = Duration{this->interval}; };
    void Reset(duration_t new_duration)
    {
        this->interval = new_duration;
        Reset();
    }
};

class RepetitiveTimer : public Timer
{
public:
    RepetitiveTimer(duration_t timer_cooldown) : Timer(timer_cooldown, true) {}
};

class ManualTimer : public Timer
{
  public:
    ManualTimer(duration_t timer_cooldown) : Timer(timer_cooldown, false) {}
};
