#pragma once
#include "levelgen.h"
#include <memory>

namespace crust::levelgen::braid
{

class BraidLevelGenerator : public GeneratorAlgorithm
{
  public:
    std::unique_ptr<World> Generate(Size size) override;
};

} // namespace levelgen::braid
