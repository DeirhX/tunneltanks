#include <cstdio>
#include <ctime>

#include <random.h>
#include <gamelib.h>

RandomGenerator Random;

void RandomGenerator::Seed()
{
	/* Try to load out of /dev/urandom, but it isn't fatal if we can't: */
	int seed = 0;
	FILE* urand = fopen("/dev/urandom", "r");
	if (urand) {
		if (fread(&seed, sizeof(seed), 1, urand) != 1) seed = 0;
		fclose(urand);
	}

	/* Throw in the time, so that computers w/o the urandom source don't get
	 * screwed... plus... it doesn't hurt. :) */
	seed ^= time(nullptr);
	Seed(seed);
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

