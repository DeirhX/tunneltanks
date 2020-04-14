#include "resources.h"

bool Resources::Pay(Cost payment)
{
    if ((this->dirt - payment.dirt).amount < 0 || (this->minerals - payment.minerals).amount < 0)
        return false;
    this->dirt -= payment.dirt;
    this->minerals -= payment.minerals;
    return true;
}

bool Resources::Add(Cost gift)
{
    bool exceeded = false;
    if ((this->dirt + gift.dirt).amount > dirt_max.amount ||
        (this->minerals + gift.minerals).amount > minerals_max.amount)
        exceeded = true;

    /* Do the add */
    this->dirt = this->dirt + gift.dirt;
    this->minerals += gift.minerals;

    /* Trim if it was too much */
    if (this->dirt.amount > this->dirt_max.amount)
        this->dirt = this->dirt_max;
    if (this->minerals.amount > this->minerals_max.amount)
        this->minerals = this->minerals_max;

    return exceeded;
}

void Resources::Absorb(Resources & other)
{
    this->dirt += other.dirt;
    this->minerals += other.minerals;
    other.dirt = 0_dirt;
    other.minerals = 0_minerals;

    if (this->dirt.amount > this->dirt_max.amount)
    {
        other.dirt += (this->dirt - this->dirt_max);
        this->dirt = this->dirt_max;
    }
    if (this->minerals.amount > this->minerals_max.amount)
    {
        other.minerals += (this->minerals - this->minerals_max);
        this->minerals = this->minerals_max;
    }
}

