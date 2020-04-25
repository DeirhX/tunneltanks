#include "link.h"

#include "color_palette.h"
#include "level.h"
#include "render_surface.h"
#include "shape_renderer.h"
#include "world.h"

#include <algorithm>

LinkPoint::LinkPoint(Position position, LinkPointType type_, LinkMap * owner_)
    : type(type_), position(position), owner(owner_)
{
    this->owner->UpdateLinksToPoint(this);
    ComputePossibleLinks();
    ComputeIsPowered();
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
        if (neighbor.point->IsOrphaned() && neighbor.point->IsEnabled() &&
            (!closest_point || neighbor.distance < closest_point->distance) &&
            compare_func(*this, neighbor))
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
    return GetClosestOrphanedPoint([](const LinkPoint &, const NeighborLinkPoint &) { return true; });
}

bool LinkPoint::IsInRange(LinkPoint * other_link) const
{
    float distance = (other_link->GetPosition() - this->GetPosition()).GetSize();
    return this->IsEnabled() &&
           (distance <= tweak::world::MaximumLiveLinkDistance ||
           (this->type == LinkPointType::Transit && distance <= tweak::world::MaximumTheoreticalLinkDistance));
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

void LinkPoint::ComputeIsPowered()
{
    this->is_powered =
        this->type == LinkPointType::Base || std::any_of(this->active_links.begin(), this->active_links.end(),
                                                         [this](Link * link) { return link->GetType() == LinkType::Live && 
                                                         link->GetSource() != this && /* Consider only links that lead to this one (this one is the target, not source) */
                                                         link->IsConnectedToLiveLink() ; });
        
}

void LinkPoint::AddActiveLink(Link * active_link) { this->active_links.push_back(active_link); }

void LinkPoint::RemoveActiveLink(Link * active_link)
{
    assert(std::find(this->active_links.begin(), this->active_links.end(), active_link) != this->active_links.end());
    std::erase(this->active_links, active_link);
}

void LinkPoint::Advance()
{
    ComputeIsPowered();
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

    float distance = (this->to.GetPoint()->GetPosition() - this->from.GetPoint()->GetPosition()).GetSize();
    if (distance > tweak::world::MaximumLiveLinkDistance)
    {
        UpdateType(LinkType::Theoretical);
    }
    else
    {
        if (IsConnectionBlocked())
            UpdateType(LinkType::Blocked);
        else
        {
            if (from.GetPoint()->GetType() == LinkPointType::Base)
                UpdateType(LinkType::Live);
            else
            {
                if (IsConnectedToLiveLink())
                    UpdateType(LinkType::Live);
                else
                    UpdateType(LinkType::Blocked);
            }
        }
    }
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

void Link::Advance()
{
    if (!this->from.GetPoint() || !this->to.GetPoint())
    {
        Invalidate();
    }

    if (!IsConnectedToLiveLink())
    {
        UpdateType(LinkType::Blocked);
    }

    if (this->collision_check_timer.AdvanceAndCheckElapsed())
    {
        if (this->GetType() == LinkType::Live)
            if (IsConnectionBlocked())
            {
                UpdateType(LinkType::Blocked);
                this->from.GetPoint()->GetLinkMap()->RequestRecompute();
            }
    }
}

bool Link::IsConnectionBlocked(Position from, Position to)
{
    return !Raycaster::Cast(PositionF{from}, PositionF{to}, [](PositionF tested_pos, PositionF) {
        auto pixel = GetWorld()->GetLevel()->GetPixel(tested_pos.ToIntPosition());
        return Pixel::IsAnyCollision(pixel) ? false : true;
    });
}

bool Link::IsConnectionBlocked() const
{
    return IsConnectionBlocked(this->from.GetPoint()->GetPosition(), this->to.GetPoint()->GetPosition());
}

bool Link::IsConnectedToLiveLink() const
{
    return from.GetPoint()->IsPowered();
    /*return std::any_of(from.GetPoint()->GetActiveLinks().begin(), from.GetPoint()->GetActiveLinks().end(),
                       [this](Link * link) {
                           return link->to.GetPoint() != this->to.GetPoint() && link->GetType() == LinkType::Live;
                       });*/
}

void Link::UpdateType(LinkType value)
{
    if (value == this->type)
        return;

    this->type = value;
    if (value == LinkType::Live)
    { /* Optimization to true but maybe should be unified to call ComputeIsPowered? */
        this->from.GetPoint()->SetIsPowered(true);
        this->to.GetPoint()->SetIsPowered(true);
    }
    else if (value == LinkType::Blocked)
    {
        this->from.GetPoint()->ComputeIsPowered();
        this->to.GetPoint()->ComputeIsPowered();
    }
}


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
        LinkPoint * source = {};
        NeighborLinkPoint target = {};
    };
    auto find_best_candidate = [&connected_nodes](auto & predicate) {
        BestCandidate best_match;
        /* Walk through visited nodes and find one closest new node to any of them */
        for (LinkPoint * point : connected_nodes)
        {
            auto candidate = point->GetClosestOrphanedPoint(predicate);
            if (!candidate.has_value())
                continue;
            if (!best_match.source || best_match.target.distance > candidate.value().distance)
            {
                best_match.target = candidate.value();
                best_match.source = point;
            }
        }
        return best_match;
    };
    /* Predicates used in descending priorities. 
     * 1) connect immovable machines
     * 2) add tank reactors (possibly machines in transit?)
     */
    enum class ConnectPhase
    {
        LiveMachines,
        BlockedMachines,
        LiveTanks,
        BlockedTanks,
        Done
    };
    auto connect_with_live_machines_only = [](const LinkPoint & from, const NeighborLinkPoint & possible_link)-> bool {
        return possible_link.point->GetType() == LinkPointType::Machine && 
               from.IsPowered() &&
               !Link::IsConnectionBlocked(from.GetPosition(), possible_link.point->GetPosition());
    };
    auto connect_with_blocked_machines = [](const LinkPoint &, const NeighborLinkPoint & possible_link) -> bool {
        return possible_link.point->GetType() == LinkPointType::Machine;
    };
    auto connect_with_live_tanks = [](const LinkPoint & from, const NeighborLinkPoint & possible_link) -> bool {
        return possible_link.point->GetType() == LinkPointType::Tank &&
               possible_link.distance <= tweak::tank::MaximumAbsorbEnergyDistance && 
               from.IsPowered() &&
               !Link::IsConnectionBlocked(from.GetPosition(), possible_link.point->GetPosition());
    };
    auto connect_with_blocked_tanks = [](const LinkPoint &, const NeighborLinkPoint & possible_link) -> bool {
        return possible_link.point->GetType() == LinkPointType::Tank &&
               possible_link.distance <= tweak::tank::MaximumAbsorbEnergyDistance;
    };

    /* Loop until we have connected everything we can */
    BestCandidate best_candidate;
    ConnectPhase phase = ConnectPhase::LiveMachines;
    do
    {

        if (phase == ConnectPhase::LiveMachines)
        {
            best_candidate = find_best_candidate(connect_with_live_machines_only);
            if (best_candidate.source == nullptr)
                phase = ConnectPhase::BlockedMachines;
        }
        if (phase == ConnectPhase::BlockedMachines)
        {
            best_candidate = find_best_candidate(connect_with_blocked_machines);
            if (best_candidate.source == nullptr)
                phase = ConnectPhase::LiveTanks;
        }
        if (phase == ConnectPhase::LiveTanks)
        {
            best_candidate = find_best_candidate(connect_with_live_tanks);
            if (best_candidate.source == nullptr)
                phase = ConnectPhase::BlockedTanks;
        }
        if (phase == ConnectPhase::BlockedTanks)
        {
            best_candidate = find_best_candidate(connect_with_blocked_tanks);
            if (best_candidate.source == nullptr)
                phase = ConnectPhase::Done;
        }
        if (best_candidate.source == nullptr)
            break;

        /* Connect the link */
        best_candidate.target.point->SetIsPartOfGraph(true);
        this->links.ConstructElement(best_candidate.source, best_candidate.target.point);
        connected_nodes.push_back(best_candidate.target.point);
        //closest_point.point->SetIsPartOfGraph();
    } while (best_candidate.source != nullptr);
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

    for (Link & link : this->links)
        link.Advance();
}

void LinkMap::Draw(Surface * surface) const
{
    for (const Link & link : this->links)
    {
        link.Draw(surface);
    }
}
