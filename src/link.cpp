#include "link.h"

#include "color_palette.h"
#include "level.h"
#include "render_surface.h"
#include "shape_renderer.h"
#include "world.h"

LinkPoint::LinkPoint(Position position, LinkPointType type_, LinkMap * owner_)
    : type(type_), position(position), owner(owner_)
{
    this->owner->UpdateLinksToPoint(this);
    ComputePossibleLinks();
}

LinkPoint::~LinkPoint()
{
    this->destructor_called = true;
    for (Link * link : this->active_links)
    {
        link->DisconnectPoint(this);
    }
    if (this->owner)
        this->owner->UnregisterPoint(this);
}


template <typename CompareFunc>
std::optional<NeighborLinkPoint> LinkPoint::GetClosestOrphanedPoint(CompareFunc compare_func) const
{
    const NeighborLinkPoint * closest_point = nullptr;
    for (const NeighborLinkPoint & neighbor : this->possible_links)
    {
        if (neighbor.point->IsOrphaned() 
            && (!closest_point || neighbor.distance < closest_point->distance)
            && compare_func(neighbor))
        {
            closest_point = &neighbor;
        }
    }
    if (closest_point)
        return *closest_point;
    return std::nullopt;
}

std::optional<NeighborLinkPoint> LinkPoint::GetClosestOrphanedPoint() const
{
    return GetClosestOrphanedPoint([](const NeighborLinkPoint & candidate) { return true; });
}

bool LinkPoint::IsInRange(LinkPoint * other_link) const
{
    float distance = (other_link->GetPosition() - this->GetPosition()).GetSize();
    return distance <= tweak::world::MaximumLiveLinkDistance ||
        (this->type == LinkPointType::Transit && distance <= tweak::world::MaximumTheoreticalLinkDistance);
}

void LinkPoint::SetPosition(Position position_)
{
    if (this->position == position_)
        return;

    assert(this->owner);
    this->position = position_;
    ComputePossibleLinks();
    this->owner->UpdateLinksToPoint(this);
}

void LinkPoint::RemovePossibleLink(LinkPoint * possible_link)
{
    std::erase_if(this->possible_links, [possible_link](auto & value) { return value.point == possible_link; });
}

/* Retest distance and make sure that:
 *  - if outside distance, delete link from list of possibles if there
 *  - if inside distance, add link to list of possibles if not already there */
void LinkPoint::UpdatePossibleLink(LinkPoint * possible_link)
{
    /* Figure if we are a possible candidate */
    if (this == possible_link)
        return;
    bool in_range = IsInRange(possible_link);

    if (!in_range)
        std::erase_if(this->possible_links, [possible_link](auto & value) { return possible_link == value.point; });
    else
    {
        /* Add if not already present */
        auto existing = std::find_if(this->possible_links.begin(), this->possible_links.end(),
                                     [possible_link](auto & value) { return value.point == possible_link; });
        if (existing == this->possible_links.end())
            this->possible_links.push_back(
                {.point = possible_link, .distance = (this->GetPosition() - possible_link->GetPosition()).GetSize()});
        else
            existing->distance = (this->GetPosition() - possible_link->GetPosition()).GetSize();
    }
}

/*
 * Discard and recompute a list of possible links that are close enough to be candidates for connection
 */
void LinkPoint::ComputePossibleLinks()
{
    assert(this->owner);
    this->possible_links.clear();
    for (LinkPoint & link : this->owner->GetLinkPoints())
    {
        if (&link != this && IsInRange(&link))
            this->possible_links.push_back(
                NeighborLinkPoint{.point = &link, .distance = (link.GetPosition() - this->GetPosition()).GetSize()});
    }
}

void LinkPoint::AddActiveLink(Link * active_link)
{
    this->active_links.push_back(active_link);
}

void LinkPoint::RemoveActiveLink(Link * active_link)
{
    assert(std::find(this->active_links.begin(), this->active_links.end(), active_link) != this->active_links.end());
    std::erase(this->active_links, active_link);
}

/*
 * LinkPointSource
 */

LinkPointSource::LinkPointSource(World * world, Position position, LinkPointType type)
{
    this->link_point = world->GetLinkMap()->RegisterLinkPoint(position, type);
}

LinkPointSource & LinkPointSource::operator=(LinkPointSource && movable) noexcept
{
    this->link_point = movable.link_point;
    movable.link_point = nullptr;
    return *this;
}

void LinkPointSource::Destroy()
{
    if (this->link_point)
        this->link_point->GetLinkMap()->UnregisterPoint(link_point);
    this->link_point = nullptr;
}

/*
 * Link & Zelda
 */

Link::Link(LinkPoint * from_, LinkPoint * to_) : from(from_, this), to(to_, this)
{
    assert(from_ && to_);

    this->type = LinkType::Live;
    float distance = (this->to.GetPoint()->GetPosition() - this->from.GetPoint()->GetPosition()).GetSize();
    if (distance > tweak::world::MaximumLiveLinkDistance)
        this->type = LinkType::Theoretical;
    //else find if blocked
    //
}

void Link::Draw(Surface * surface) const
{
    if (!this->from.GetPoint() || !this->to.GetPoint())
        return;

    Color color;
    switch (this->type)
    {
    case LinkType::Live:
        color = Palette.Get(Colors::LinkActive);
        break;
    case LinkType::Theoretical:
        color = Palette.Get(Colors::LinkTheoretical);
        break;
    case LinkType::Blocked:
        color = Palette.Get(Colors::LinkBlocked);
        break;
    }
    ShapeRenderer::DrawLine(surface, this->from.GetPoint()->GetPosition(), this->to.GetPoint()->GetPosition(), color);
}

void Link::DisconnectPoint(LinkPoint * point)
{
    if (point == this->from.GetPoint())
        this->from.Disconnect();
    else if (point == this->to.GetPoint())
        this->to.Disconnect();
    else
    {
        assert("Invalid point");
    }
}


//LinkPoint * LinkMap::RegisterLinkPoint(LinkPoint && temp_point)
//{
//    LinkPoint * ret_val = &this->link_points.Add(temp_point);
//    return ret_val;
//}

/*
 * LinkMap
 */

void LinkMap::UnregisterPoint(LinkPoint * link_point)
{
    this->is_collection_modified = true;
    /* Doesn't do anything on this container but may be needed if they are switched */
    this->link_points.Remove(*link_point); 
    /* Must erase any notion of existence from cached possible links*/
    for (LinkPoint & point : this->link_points)
        if (&point != link_point)
            point.RemovePossibleLink(link_point);
}

void LinkMap::RemoveAll()
{
    this->links.RemoveAll();
    this->link_points.RemoveAll();
}

void LinkMap::UpdateLinksToPoint(LinkPoint * link_point)
{
    this->is_linkpoint_moved = true;
    /* Offer this point to all others to adopt it into their possible links */
    for (LinkPoint & point : this->link_points)
        if (&point != link_point)
            point.UpdatePossibleLink(link_point);
}

/*
 * Recompute the link graph, connecting all nodes to bases
 */
void LinkMap::SolveLinks()
{
    /* Throw away existing ones. We can optimize this to updating only modified parts if needed, not needed now  */
    this->links.RemoveAll();

    /* Prepare lists of all nodes and currently connected nodes */
    std::vector<LinkPoint *> all_nodes;
    all_nodes.reserve(this->link_points.CurrentCapacity());
    std::vector<LinkPoint *> connected_nodes;
    connected_nodes.reserve(this->link_points.CurrentCapacity());

    /* Start with base link points and consider them linked.
     *  Subsequently we'll try to link everything else to them */
    for (LinkPoint & point : this->link_points)
    {
        if (!point.IsEnabled())
            continue;
        ;
        if (point.GetType() == LinkPointType::Base)
        {
            connected_nodes.push_back(&point);
            point.SetIsPartOfGraph(true);
        }
        else
            point.SetIsPartOfGraph(false);

        all_nodes.push_back(&point);
    }

    /* Prepare functions to find best candidate based on variable predicates */
    struct BestCandidate
    {
        NeighborLinkPoint source = {};
        LinkPoint * target = nullptr;
    };
    auto find_best_candidate = [&connected_nodes](auto & predicate) {
        BestCandidate best_match;
        /* Walk through visited nodes and find one closest new node to any of them */
        for (LinkPoint * point : connected_nodes)
        {
            auto candidate = point->GetClosestOrphanedPoint(predicate);
            if (!candidate.has_value())
                continue;
            if (!best_match.source.point || best_match.source.distance > candidate.value().distance)
            {
                best_match.target = point;
                best_match.source = candidate.value();
            }
        }
        return best_match;
    };
    /* Predicates used in descending priorities. 
     * 1) connect immovable machines
     * 2) add tank reactors (possibly machines in transit?)
     */
    enum class ConnectPhase { Machines, Tanks, Done};
    auto connect_with_machines_only = [](const NeighborLinkPoint & possible_link) {
        return possible_link.point->GetType() == LinkPointType::Machine;
    };
    auto connect_with_tanks = [](const NeighborLinkPoint & possible_link) {
        return possible_link.point->GetType() == LinkPointType::Tank && possible_link.distance <= tweak::tank::MaximumAbsorbEnergyDistance;
    };

    /* Loop until we have connected everything we can */
    BestCandidate best_candidate;
    ConnectPhase phase = ConnectPhase::Machines;
    do 
    {
        if (phase == ConnectPhase::Machines)
        {
            best_candidate = find_best_candidate(connect_with_machines_only);
            if (best_candidate.target == nullptr)
                phase = ConnectPhase::Tanks;
        }
        if (phase == ConnectPhase::Tanks)
        {
            best_candidate = find_best_candidate(connect_with_tanks);
            if (best_candidate.target == nullptr)
                phase = ConnectPhase::Done;
        }
        if (best_candidate.target == nullptr)
            break;

        /* Connect the link */
        best_candidate.source.point->SetIsPartOfGraph(true);
        this->links.ConstructElement(best_candidate.source.point, best_candidate.target);
        connected_nodes.push_back(best_candidate.source.point);
        //closest_point.point->SetIsPartOfGraph();
    } while (best_candidate.target != nullptr);
}

/* Resolve connections immediately only if new link was added or removed, 
 *  otherwise do it only once per timer elapsed (links will still move, just not recompute) */
void LinkMap::Advance()
{
    if (this->relink_timer.AdvanceAndCheckElapsed() || this->is_collection_modified)
    {
        if (this->is_linkpoint_moved || this->is_collection_modified)
        {
            SolveLinks();
            this->is_collection_modified = false;
            this->is_linkpoint_moved = false;
        }
    }
}

void LinkMap::Draw(Surface * surface) const
{
    for (const Link & link : this->links)
    {
        link.Draw(surface);
    }
}
