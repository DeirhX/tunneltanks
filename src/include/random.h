#pragma once
#include <random>

class RandomGenerator
{
	bool is_seeded = false;
	std::mt19937 gen;
public:
	void Seed();
	void Seed(int seed);

	bool Bool(int odds_of_off_1000);
	 template <typename IntegerType>
	IntegerType Int(IntegerType min, IntegerType max);
};

extern RandomGenerator Random;

template <typename IntegerType>
IntegerType RandomGenerator::Int(IntegerType min, IntegerType max)
{
	//if (!is_seeded)
	//	Seed();
	
	IntegerType range = max - min + 1;

	if (max <= min) 
		return min;

	/* I know that using the % isn't entirely accurate, but it only uses
	 * integers, so w/e: */
	return (gen() % range) + min;
}
