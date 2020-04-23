#pragma once
#include <level.h>

#include "levelgen.h"

namespace levelgen::simple {

	class SimpleLevelGenerator : public GeneratorAlgorithm
{
  public:
    std::unique_ptr<Level> Generate(Size size) override;
};

}


