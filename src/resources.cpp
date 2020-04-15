#include "resources.h"

bool Materials::Pay(MaterialAmount payment)
{
    if ((this->current - payment).IsNegative())
        return false;
    this->current -= payment;
    return true;
}

bool Materials::Add(MaterialAmount gift)
{
    bool exceeded = false;
    if (this->current + gift > this->capacity)
        exceeded = true;

    /* Do the add */
    this->current += gift;

    /* Trim if it was too much */
    auto excess = this->current - this->capacity;
    excess.TrimNegative();
    this->current -= excess;

    return exceeded;
}

void Materials::Absorb(Materials & other)
{
    this->current += other.current;
    other.current = {};

    auto excess = this->current - this->capacity;
    excess.TrimNegative();
    this->current -= excess;
    other.current += excess;
}

