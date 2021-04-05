#pragma once
#include "types.h"
#include <chrono>
#include <cstdio>
#include <memory>
class Terrain;

namespace levelgen
{

enum class LevelGeneratorType
{
    None,
    Toast,
    Braid,
    Maze,
    Simple,
};

struct GeneratedLevel
{
    std::unique_ptr<Terrain> level;
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
    virtual std::unique_ptr<Terrain> Generate(Size size) = 0;
};

class Queries
{
public:
    static int CountNeighborValues(Position pos, Terrain * level);
};

} // namespace levelgen
