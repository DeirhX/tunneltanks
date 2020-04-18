﻿#include "link.h"
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

NeighborLinkPoint LinkPoint::GetClosestUnconnectedPoint() const
{
    const NeighborLinkPoint * closest_point = nullptr;
    for (const NeighborLinkPoint & neighbor : this->possible_links)
    {
        if (!neighbor.point->IsConnected() && (!closest_point || neighbor.distance < closest_point->distance))
        {
            closest_point = &neighbor;
        }
    }
    return *closest_point;
}

void LinkPoint::SetPosition(Position position_)
{
    if (this->position == position_)
        return;

    assert(this->owner);
    this->position = position_;
    this->owner->UpdateLinksToPoint(this);
}

void LinkPoint::RemovePossibleLink(LinkPoint * possible_link)
{
    std::erase_if(this->possible_links, [possible_link](auto & value) { return value.point == possible_link; });
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
        std::erase_if(this->possible_links, [possible_link](auto & value) { return possible_link == value.point; });
    else
    {
        /* Add if not already present */
        auto existing = std::find_if(this->possible_links.begin(), this->possible_links.end(),
                                     [possible_link](auto & value) { return value.point == possible_link; });
        if (existing != this->possible_links.end())
            this->possible_links.push_back(
                {.point = possible_link, .distance = (this->GetPosition() - possible_link->GetPosition()).GetSize()});
    }
}

/*
 * Discard and recompute a list of possible links that are close enough to be candidates for connection
 */
void LinkPoint::UpdateAllLinks()
{
    assert(this->owner);
    this->possible_links.clear();
    for (LinkPoint & link : this->owner->GetLinkPoints())
    {
        if ((link.GetPosition() - this->GetPosition()).GetSize() <= tweak::rules::MaximumLinkDistance)
            this->possible_links.push_back(
                NeighborLinkPoint{.point = &link, .distance = (link.GetPosition() - this->GetPosition()).GetSize()});
    }
}

//LinkPoint * LinkMap::RegisterLinkPoint(LinkPoint && temp_point)
//{
//    LinkPoint * ret_val = &this->link_points.Add(temp_point);
//    return ret_val;
//}

void LinkMap::UnregisterPoint(LinkPoint * link_point)
{
    this->modified = true;
    /* Doesn't do anything on this container but may be needed if they are switched */
    this->link_points.Remove(*link_point); 
    /* Must erase any notion of existence from cached possible links*/
    for (LinkPoint & point : this->link_points)
        if (&point != link_point)
            point.RemovePossibleLink(link_point);
}

void LinkMap::UpdateLinksToPoint(LinkPoint * link_point)
{
    this->modified = true;
    /* Offer this point to all others to adopt it into their possible links */
    for (LinkPoint & point : this->link_points)
        if (&point != link_point)
            point.UpdateLink(link_point);
}

void LinkMap::SolveLinks()
{
    /* Prepare lists of all nodes and currently connected nodes */
    std::vector<LinkPoint *> all_nodes;
    all_nodes.reserve(this->link_points.CurrentCapacity());
    std::vector<LinkPoint *> connected_nodes;
    connected_nodes.reserve(this->link_points.CurrentCapacity());

    /* Start with base link points and consider them linked.
     *  Subsequently we'll try to link everything else to them */
    for (LinkPoint & point : this->link_points)
    {
        if (point.GetType() == LinkPointType::Base)
        {
            connected_nodes.push_back(&point);
            point.SetConnected(true);
        }
        else
            point.SetConnected(false);

        all_nodes.push_back(&point);
    }

    /* Links will be generated here */
    std::vector<Link> updated_links;
    updated_links.reserve(all_nodes.size() - 1);

    /* Loop until we have visited all nodes */
    while (connected_nodes.size() != all_nodes.size())
    {
        NeighborLinkPoint closest_point = {};
        LinkPoint * connect_target = nullptr;
        /* Walk through visited nodes and find one closest new node to any of them */
        for (LinkPoint * point : connected_nodes)
            if (!point->IsConnected())
            {
                NeighborLinkPoint candidate = point->GetClosestUnconnectedPoint();
                if (!closest_point.point || closest_point.distance > candidate.distance)
                {
                    connect_target = point;
                    closest_point = candidate;
                }
            }

        /* Connect the link */
        updated_links.emplace_back(closest_point.point, connect_target);
    }

    /* Replace current links with this one */
    this->links = updated_links;
}

void LinkMap::Advance()
{
    if (this->modified)
    {
        SolveLinks();
        this->modified = false;
    }
}
