#pragma once

#include <tanklist.h>

class Scene
{
    virtual void Advance() = 0;
};

class MainGameScene : public Scene
{
  public:
    TankList tank_list;
    ProjectileList projectiles;

    void Advance() override;
};
