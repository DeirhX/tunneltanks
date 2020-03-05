#include <stdio.h>
#include <stdlib.h>
#include <time.h>

#include <gamelib.h>
#include <random.h>


bool rand_bool(int odds) {
	return rand_int(0,999) < odds;
}

void rand_seed() {
	int seed = 0;
	FILE *urand;
	
	/* Try to load out of /dev/urandom, but it isn't fatal if we can't: */
	urand = NULL;
	urand = fopen("/dev/urandom", "r");
	if(urand) {
		if(fread(&seed, sizeof(seed), 1, urand) != 1) seed = 0;
		fclose(urand);
	}
	
	/* Throw in the time, so that computers w/o the urandom source don't get
	 * screwed... plus... it doesn't hurt. :) */
	seed ^= time(NULL);
	
	gamelib_print("Using seed: %d\n", seed);
	
	srand(seed);
}

