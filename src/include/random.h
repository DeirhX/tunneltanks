#pragma once
#include <random>

class Random
{
	inline static bool is_seeded = false;
public:
	static void Seed();
	static void Seed(int seed);

	static bool Bool(int odds_of_off_1000);
	 template <typename IntegerType>
	static IntegerType Int(IntegerType min, IntegerType max);
};

template <typename IntegerType>
IntegerType Random::Int(IntegerType min, IntegerType max)
{
	if (!is_seeded)
		Seed();
	
	IntegerType range = max - min + 1;

	if (max <= min) 
		return min;

	/* I know that using the % isn't entirely accurate, but it only uses
	 * integers, so w/e: */
	return (std::rand() % range) + min;
}
