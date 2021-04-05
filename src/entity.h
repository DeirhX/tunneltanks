#pragma once
#include "containers.h"

class Entity
{
public:
    
};

template <typename TAdvanceable>
concept Advanceable = requires(TAdvanceable t)
{
    t.Advance();
};

class Component
{
    
};

/*
template <typename... TComponent>
class ComponentList
{
    MultiTypeContainer<TComponent> list;
};
*/