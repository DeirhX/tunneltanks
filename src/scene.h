#pragma once

#include <tank_list.h>

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
