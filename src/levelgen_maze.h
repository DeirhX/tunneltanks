#pragma once
#include <level.h>
#include <memory>

#include "levelgen.h"

namespace levelgen::maze
{

class MazeLevelGenerator : public GeneratorAlgorithm
{
  public:
    std::unique_ptr<Level> Generate(Size size) override;
};

} // namespace levelgen::maze
