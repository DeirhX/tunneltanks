#pragma once
#include "levelgen.h"
#include <Terrain.h>
#include <memory>

namespace levelgen::maze
{

class MazeLevelGenerator : public GeneratorAlgorithm
{
  public:
    std::unique_ptr<Terrain> Generate(Size size) override;
};

} // namespace levelgen::maze
