#include "base.h"
#include <cstdio>
#include <ctime>

#include <random.h>
#include <gamelib.h>

RandomGenerator Random;

void RandomGenerator::Seed()
{
	gen.seed(int(time(nullptr)));
}

void RandomGenerator::Seed(int seed)
{
	gamelib_print("Using seed: %d\n", seed);
	gen.seed(seed);
	is_seeded = true;
}

bool RandomGenerator::Bool(int odds) {
	return Int(0,999) < odds;
}

