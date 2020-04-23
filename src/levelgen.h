#pragma once
#include "types.h"
#include <chrono>
#include <cstdio>
#include <memory>
class Level;

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
    std::unique_ptr<Level> level;
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
    virtual std::unique_ptr<Level> Generate(Size size) = 0;
};

class Queries
{
public:
    static int CountNeighborValues(Position pos, Level * level);
};

} // namespace levelgen
