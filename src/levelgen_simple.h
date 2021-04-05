#pragma once
#include "levelgen.h"
#include <Terrain.h>

namespace levelgen::simple
{

class SimpleLevelGenerator : public GeneratorAlgorithm
{
  public:
    std::unique_ptr<World> Generate(Size size) override;
};

} // namespace levelgen::simple
