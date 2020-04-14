#include "link.h"
#include "level.h"

LinkPoint * LinkMap::RegisterLinkPoint(LinkPoint && temp_point) { return &this->link_points.Add(temp_point); }

void LinkMap::UpdateAll()
{
    //for( TankBase & base : this->level->GetSpawns())
    {

    }
}
