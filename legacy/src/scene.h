#pragma once

#include "projectile_list.h"
#include "tank_list.h"
namespace crust
{
class Scene
{
  public:
    virtual ~Scene() = default;
    virtual void Advance() = 0;
};

class MainGameScene : public Scene
{
  public:
    TankList tank_list;
    ProjectileList projectiles;

    void Advance() override;
};

} // namespace MyNamespace