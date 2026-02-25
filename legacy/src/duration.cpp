#include "pch.h"
#include "duration.h"
#include "tweak.h"
namespace crust
{

duration_t & Duration::operator--()
{
    this->value -= tweak::world::AdvanceStep;
    return this->value;
}

} // namespace crust