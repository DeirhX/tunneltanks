#pragma once
#include <limits>

struct SpeedF;
struct DirectionF;
struct VectorF;

namespace math
{

const float pi = 3.141592f;
const float two_pi = 2.0f * pi;
const float half_pi = 0.5f * pi;
const float epsilon = std::numeric_limits<float>::epsilon();

struct Radians
{
    float val = 0;

    Radians() = default;
    Radians(float radians) : val(radians) {}
    explicit Radians(DirectionF vector);
    explicit Radians(SpeedF vector);
    DirectionF ToDirection() const;
};

float ToRadians(DirectionF vector);
DirectionF FromRadians(float radians);

} // namespace math
