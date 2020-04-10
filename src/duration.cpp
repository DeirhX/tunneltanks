#include "duration.h"
#include "tweak.h"

duration_t & Duration::operator--()
{
    this->value -= tweak::world::AdvanceStep;
    return this->value;
}
