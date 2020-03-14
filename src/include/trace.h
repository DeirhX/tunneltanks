#pragma once
#include <chrono>
#include <type_traits>
#include <string_view>
#include <gamelib.h>
#include <cassert>

using TraceLevel = int;

template
<
	typename TimeUnit = std::chrono::microseconds,
	typename Clock = std::chrono::high_resolution_clock
>
class Stopwatch
{
	std::chrono::time_point<Clock> start;
	TimeUnit elapsed = {};
	bool is_running = false;

public:
	Stopwatch() { Start(); }

	void Start()
	{
		start = Clock::now();
		is_running = true;
	}
	void Stop()
	{
		assert(is_running);
		elapsed += std::chrono::duration_cast<TimeUnit>(Clock::now() - start);
		is_running = false;
	}

	TimeUnit GetElapsed()
	{
		if (is_running)
			Stop();
		return elapsed;
	}
};



/* debug_level <= DEBUG_TRACE_LEVEL */
template <TraceLevel debug_level, typename... TPrintfArgs, typename std::enable_if_t<(debug_level <= DEBUG_TRACE_LEVEL), int> = 0 >
void DebugTrace(TPrintfArgs&&... args)
{
	char debug_buff[1024];
	for (int i = 0; i < debug_level; ++i)
		debug_buff[i] = ' ';
	std::sprintf(debug_buff + debug_level, std::forward<TPrintfArgs>(args)...);
	gamelib_debug(debug_buff);
}
template <TraceLevel debug_level, typename... TPrintfArgs, typename std::enable_if_t<(debug_level > DEBUG_TRACE_LEVEL), int> = 0 >
void DebugTrace(TPrintfArgs&&... args)
{
}


/* Do not measure at all if trace level too low  */

template
<
	int TraceLevel,
	typename TimeUnit = std::chrono::microseconds,
	typename Clock = std::chrono::high_resolution_clock,
	typename = void
>
struct MeasureFunction { MeasureFunction(std::string_view name) { };  void Finish() {}  };

template
<
	int TraceLevel,
	typename TimeUnit,
	typename Clock
>
struct MeasureFunction<TraceLevel, TimeUnit, Clock, typename std::enable_if<(TraceLevel <= DEBUG_TRACE_LEVEL)>::type >
{
	Stopwatch<TimeUnit, Clock> watch;
	std::string_view name;
	bool is_done = false;
public:
	MeasureFunction(std::string_view name) : name(name) { };
	~MeasureFunction() { Finish(); }
	void Finish() {
		if (!is_done) {
			watch.Stop();

			DebugTrace<TraceLevel>("%s took %lld.%03lld ms \n", name.data(),
				watch.GetElapsed().count() / 1000, watch.GetElapsed().count() % 1000);
			is_done = true;
		}
	}
};
