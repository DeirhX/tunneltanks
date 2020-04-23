#pragma once
#include "levelgen.h"
#include <level.h>
#include <memory>

namespace levelgen::maze
{

class MazeLevelGenerator : public GeneratorAlgorithm
{
  public:
    std::unique_ptr<Level> Generate(Size size) override;
};

} // namespace levelgen::maze
