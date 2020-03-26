#include "mymath.h"
#include "types.h"

namespace math
{

float ToRadians(DirectionF vector) { return std::atan2(vector.y, vector.x); }
DirectionF FromRadians(float radians) { return VectorF{std::cos(radians), std::sin(radians)}.Normalize(); }

Radians::Radians(DirectionF vector) : val(ToRadians(vector)) {} 
Radians::Radians(SpeedF vector) : val(ToRadians(vector)) {}

DirectionF Radians::ToDirection() const { return FromRadians(this->val); }


} // namespace math