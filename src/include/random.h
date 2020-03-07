#pragma once
#include <random>

bool     rand_bool(int odds) ;
void     rand_seed() ;

template <typename IntegerType>
IntegerType rand_int(IntegerType min, IntegerType max) {
	IntegerType range = max - min + 1;

	if (max <= min) return min;

	/* I know that using the % isn't entirely accurate, but it only uses
	 * integers, so w/e: */
	return (std::rand() % range) + min;
}



