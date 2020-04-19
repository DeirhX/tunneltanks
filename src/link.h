#pragma once
#include <optional>
#include <vector>


#include "containers.h"
#include "duration.h"
#include "tweak.h"
#include "types.h"

class Surface;

enum class LinkPointType
{
    Base,
    Machine, /* Placed machine */
    Transit, /* Not yet placed */
    Tank,    /* Tank being charged */
};

struct NeighborLinkPoint
{
    class LinkPoint * point;
    float distance;
};

/* LinkPoint: Linkable point to others */
class LinkPoint : public Invalidable
{
    LinkPointType type;
    Position position;

    class LinkMap * owner;

    std::vector<NeighborLinkPoint> possible_links; /* Links sufficiently near to be able to connect */
    std::vector<class Link *> active_links;        /* Links actually connected */

    bool is_enabled = true;  /* Is currently active and able to receive links */
    bool is_powered = false; /* Is connected to a powered node */

    bool is_part_of_graph = false; /* Is already parsed by solver  */
    public:
    LinkPoint(Position position, LinkPointType type_, LinkMap * owner_ = nullptr);
    ~LinkPoint();

    [[nodiscard]] LinkPointType GetType() const { return this->type; }
    [[nodiscard]] Position GetPosition() const { return this->position; }
    [[nodiscard]] LinkMap * GetLinkMap() const { return this->owner; }
    [[nodiscard]] const std::vector<class Link *> & GetActiveLinks() const { return this->active_links; }
    [[nodiscard]] bool IsOrphaned() const { return !this->is_part_of_graph; } /* Used by link solver.*/
    [[nodiscard]] const std::vector<NeighborLinkPoint> & GetNeighbors() const { return this->possible_links; }
    template <typename CompareFunc> /* CompareFunc(const NeighborLinkPoint & candidate) -> bool */
    [[nodiscard]] std::optional<NeighborLinkPoint> GetClosestOrphanedPoint(CompareFunc compare_func) const;
    [[nodiscard]] std::optional<NeighborLinkPoint> GetClosestOrphanedPoint() const;
    [[nodiscard]] bool IsInRange(LinkPoint * other_link) const;
    [[nodiscard]] bool IsEnabled() const { return this->is_enabled; }
    [[nodiscard]] bool IsPowered() const { return this->is_powered; }


    void SetPosition(Position position_);
    void SetIsPowered(bool value) { this->is_powered = value; }
    void SetIsPartOfGraph(bool value) { this->is_part_of_graph = value; }

    void RemovePossibleLink(LinkPoint * possible_link);
    void UpdatePossibleLink(LinkPoint * possible_link); 
    void ComputePossibleLinks();
    void ComputeIsPowered();

    void AddActiveLink(Link * active_link);
    void RemoveActiveLink(Link * active_link);

    void Disable() { this->is_enabled = false; }
    void Enable() { this->is_enabled = true; }
};


/* LinkPointSource: Wrapper to manage the lifetime of LinkPoint that resides in LinkMap array.
 *   Should be member of classes that own a LinkPoint
 */
class LinkPointSource
{
private:
    LinkPoint * link_point = nullptr;

public:
    LinkPointSource() = default;
    LinkPointSource(class World * world, Position position, LinkPointType type);
    /* Ban copies, force move to transfer ownership of link_point */
    LinkPointSource(const LinkPointSource &) = delete;
    LinkPointSource & operator=(const LinkPointSource &) = delete;
    LinkPointSource(LinkPointSource && movable) noexcept { *this = std::move(movable); }
    LinkPointSource & operator=(LinkPointSource && movable) noexcept;
    ~LinkPointSource() { Destroy(); }

    void Detach() { link_point = nullptr; }
    void Destroy();

    void UpdatePosition(Position position) { link_point->SetPosition(position); }
    void Disable() { this->link_point->Disable(); }
    void Enable() { this->link_point->Enable(); }
};


enum class LinkType
{
    Live,        /* Flowing normally */
    Blocked,     /* Blocked by and obstacle */
    Theoretical, /* Too far from source */
};

/* LinkPointConnector: Encapsulates reference to LinkPoint held by Link which
 *   should also sync with a list of Links inside a LinkPoint. It will delete
 *   the reference inside LinkPoint if this is destroyed.
 */
class LinkPointConnector
{
    Link * link = nullptr;
    LinkPoint * link_point = nullptr;

  public:
    LinkPointConnector(LinkPoint * point, Link * link_) : link(link_), link_point(point)
    {
        point->AddActiveLink(link_);
    }
    ~LinkPointConnector()
    {
        if (link_point)
            link_point->RemoveActiveLink(link);
    }
    LinkPointConnector(LinkPointConnector && movable) noexcept { *this = std::move(movable); }
    LinkPointConnector & operator=(LinkPointConnector && movable) noexcept
    {
        this->link = movable.link;
        std::swap(this->link_point, movable.link_point);
        return *this;
    }

    void Disconnect() { this->link_point = nullptr; }
    [[nodiscard]] LinkPoint * GetPoint() const { return this->link_point; }
};

/* Link: A connected link between two points */
class Link : public Invalidable
{
    /* Will register to LinkPoint and disconnect on destruction */
    LinkPointConnector from;
    LinkPointConnector to;

    LinkType type = LinkType::Blocked;
    RepetitiveTimer collision_check_timer = {tweak::world::LinkCollisionCheckInterval};
  public:
    Link(LinkPoint * from_, LinkPoint * to_);

    [[nodiscard]] LinkType GetType() const { return this->type; }
    [[nodiscard]] LinkPoint * GetSource() const { return this->from.GetPoint(); }
    [[nodiscard]] LinkPoint * GetTarget() const { return this->to.GetPoint(); }
    void Draw(Surface * surface) const;
    void DisconnectPoint(LinkPoint * point);
    void Advance();

    static bool IsConnectionBlocked(Position from, Position to);
    bool IsConnectionBlocked() const;

  private:
    void UpdateType(LinkType value);
};

/* LinkMap: Manages all link point updates and links */
class LinkMap
{
    Level * level;
    ValueContainer<LinkPoint> link_points = {};
    ValueContainer<Link> links = {};

    RepetitiveTimer relink_timer = {tweak::world::RefreshLinkMapInterval};

    bool is_collection_modified = false;
    bool is_linkpoint_moved = false;

  public:
    LinkMap(Level * level) : level(level) {}

    [[nodiscard]] ValueContainerView<LinkPoint> & GetLinkPoints() { return this->link_points; }

    template <class... LinkPointArgs>
    LinkPoint * RegisterLinkPoint(LinkPointArgs &&... args)
    {
        is_collection_modified = true;
        return &this->link_points.ConstructElement(std::forward<LinkPointArgs>(args)..., this);
    }
    void UnregisterPoint(LinkPoint * point);
    void RemoveAll();

    void UpdateLinksToPoint(LinkPoint * point);

    void SolveLinks();

    void Advance();
    void Draw(Surface * surface) const;
};

