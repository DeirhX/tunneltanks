#pragma once
#include "levelgen.h"
#include <level.h>

namespace levelgen::simple
{

class SimpleLevelGenerator : public GeneratorAlgorithm
{
  public:
    std::unique_ptr<Level> Generate(Size size) override;
};

} // namespace levelgen::simple
