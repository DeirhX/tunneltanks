#pragma once
#include "levelgen_type.h"
#include "types.h"
#include "world.h"

#include <chrono>
#include <cstdio>
#include <memory>
#include <vector>

class TankBase;
class Terrain;

namespace levelgen
{

struct GeneratedLevel
{
    std::unique_ptr<World> world;
    std::chrono::milliseconds generation_time;
};

class LevelGenerator
{
  public:
    static GeneratedLevel Generate(LevelGeneratorType generator, Size size);
    static LevelGeneratorType FromName(const char * name);
    static void PrintAllGenerators(FILE * out);
};

class GeneratorAlgorithm
{
  public:
    virtual ~GeneratorAlgorithm() = default;
    /* Generate a level based on an id: */
  public:
    virtual std::unique_ptr<World> Generate(Size size) = 0;
};

class Queries
{
public:
    static int CountNeighborValues(Position pos, Terrain * level);
};

} // namespace levelgen
