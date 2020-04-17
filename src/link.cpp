#include "link.h"
#include "level.h"

void LinkPoint::Invalidate()
{
    if (!this->is_alive)
        return;

    this->is_alive = false;

    if (this->owner)
        this->owner->UnregisterPoint(this);
    this->owner = nullptr;
}

void LinkPoint::SetPosition(Position position_)
{
    if (this->position == position_)
        return;

    this->position = position_;
}

void LinkPoint::RemovePossibleLink(LinkPoint * possible_link)
{
    std::erase(this->possible_links, possible_link);
}

/* Retest distance and make sure that:
 *  - if outside distance, delete link from list of possibles if there
 *  - if inside distance, add link to list of possibles if not already there */
void LinkPoint::UpdateLink(LinkPoint * possible_link)
{
    /* Figure if we are a possible candidate */
    float distance = (possible_link->GetPosition() - this->GetPosition()).GetSize();
    bool in_range = distance <= tweak::rules::MaximumLinkDistance;

    if (!in_range)
        std::erase(this->possible_links, possible_link);
    else
    {
        /* Add if not already present */
        auto existing = std::find_if(this->possible_links.begin(), this->possible_links.end(),
                                     [possible_link](auto & value) { return value == possible_link; });
        if (existing != this->possible_links.end())
            this->possible_links.push_back(possible_link);
    }
}

/*
 * Discard and recompute a list of possible links that are close enough to be candidates for connection
 */
void LinkPoint::UpdateAllLinks()
{
    assert(this->owner);
    this->possible_links.clear();
    for (auto & link : this->owner->GetLinkPoints())
    {
        if ((link.GetPosition() - this->GetPosition()).GetSize() <= tweak::rules::MaximumLinkDistance)
            this->possible_links.emplace_back(&link);
    }
}

//LinkPoint * LinkMap::RegisterLinkPoint(LinkPoint && temp_point)
//{
//    LinkPoint * ret_val = &this->link_points.Add(temp_point);
//    return ret_val;
//}

void LinkMap::UnregisterPoint(LinkPoint * link_point)
{
    /* Doesn't do anything on this container but may be needed if they are switched */
    this->link_points.Remove(*link_point); 
    /* Must erase any notion of existence from cached possible links*/
    for (LinkPoint & point : this->link_points)
        if (&point != link_point)
            point.RemovePossibleLink(link_point);
}

void LinkMap::UpdateAll()
{
    //for( TankBase & base : this->level->GetSpawns())
    {

    }
}
