#pragma once
#include "levelgen.h"
#include <memory>

namespace crust::levelgen::maze
{

class MazeLevelGenerator : public GeneratorAlgorithm
{
  public:
    std::unique_ptr<World> Generate(Size size) override;
};

} // namespace levelgen::maze
