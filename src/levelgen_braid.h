#pragma once
#include "Terrain.h"
#include "levelgen.h"
#include <memory>

namespace levelgen::braid
{

class BraidLevelGenerator : public GeneratorAlgorithm
{
  public:
    std::unique_ptr<World> Generate(Size size) override;
};

} // namespace levelgen::braid
