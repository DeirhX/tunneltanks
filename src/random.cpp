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

float RandomGenerator::Float(float min, float max)
{
    if (min == max)
        return min;
    std::uniform_real_distribution<float> float_roll(min, max);
	return float_roll(gen);
}
