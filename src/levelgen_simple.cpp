#include <level.h>
#include <levelgen_simple.h>
#include <levelgenutil.h>
#include <random.h>
#include <types.h>

namespace levelgen::simple
{

constexpr int BORDER = 80;
constexpr int STEP_MIN = 2;
constexpr int STEP_MAX = 8;
constexpr int MAX_SLOPE = 100;
constexpr int RARE_SLOPE = 600;
constexpr int RARITY = 40;

/* This just adds random points for now: */
enum Side : char
{
    SIDE_TOP = 2,
    SIDE_RIGHT = 3,
    SIDE_BOTTOM = 4,
    SIDE_LEFT = 5
};

static void add_rock_lines(Level *lvl, Side s)
{
    int xstep = 0, ystep = 0;
    Vector cur, prev;
    int is_rare, needs_flip = 0;
    int minx = 0, maxx = 0, miny = 0, maxy = 0;

    /* Configuration based on what side the rock is on: */
    if (s == SIDE_TOP)
    {
        minx = 0;
        maxx = lvl->GetSize().x - 1;
        miny = 0;
        maxy = BORDER;
    }
    else if (s == SIDE_BOTTOM)
    {
        minx = 0;
        maxx = lvl->GetSize().x - 1;
        miny = lvl->GetSize().y - BORDER - 1;
        maxy = lvl->GetSize().y - 1;
    }
    else if (s == SIDE_LEFT)
    {
        minx = 0;
        maxx = lvl->GetSize().y - 1;
        miny = 0;
        maxy = BORDER;
        needs_flip = 1;
    }
    else if (s == SIDE_RIGHT)
    {
        minx = 0;
        maxx = lvl->GetSize().y - 1;
        miny = lvl->GetSize().x - BORDER - 1;
        maxy = lvl->GetSize().x - 1;
        needs_flip = 1;
    }

    /* Let's get this party started: */
    prev = cur = Vector(minx, Random.Int(miny, maxy));

    do
    {
        /* Advance the current x position so it doesn't go over the edge: */
        cur.x += (xstep = Random.Int(STEP_MIN, STEP_MAX));
        if (cur.x > maxx)
        {
            xstep = cur.x - maxx;
            cur.x = maxx;
        }

        /* Advance the y position so that it is within bounds: */
        is_rare = Random.Bool(RARITY);
        do
        {
            int slope = is_rare ? RARE_SLOPE : MAX_SLOPE;
            ystep = (Random.Int(0, slope * 2) - slope) * xstep;
            ystep /= 100;
        } while ((cur.y + ystep) < miny || (cur.y + ystep) > maxy);

        cur.y += ystep;

        /* Draw this in whatever way is needed: */
        if (needs_flip)
            draw_line(lvl, Vector(cur.y, cur.x), Vector(prev.y, prev.x), static_cast<LevelPixel>(s), 0);
        else
            draw_line(lvl, cur, prev, static_cast<LevelPixel>(s), 0);

        prev = cur;

    } while (cur.x != maxx);

    /* Do the correct fill sequence, based on side: */

    Position p;
    if (s == SIDE_TOP)
        for (p.x = 0; p.x < lvl->GetSize().x; p.x++)
            for (p.y = 0; lvl->GetPixel(p) != static_cast<LevelPixel>(s); p.y++)
                lvl->SetVoxelRaw(p, LevelPixel::LevelGenRock);

    else if (s == SIDE_RIGHT)
        for (p.y = 0; p.y < lvl->GetSize().y; p.y++)
            for (p.x = lvl->GetSize().x - 1; lvl->GetPixel(p) != static_cast<LevelPixel>(s); p.x--)
                lvl->SetVoxelRaw(p, LevelPixel::LevelGenRock);

    else if (s == SIDE_BOTTOM)
        for (p.x = 0; p.x < lvl->GetSize().x; p.x++)
            for (p.y = lvl->GetSize().y - 1; lvl->GetPixel(p) != static_cast<LevelPixel>(s); p.y--)
                lvl->SetVoxelRaw(p, LevelPixel::LevelGenRock);

    else if (s == SIDE_LEFT)
        for (p.y = 0; p.y < lvl->GetSize().y; p.y++)
            for (p.x = 0; lvl->GetPixel(p) != static_cast<LevelPixel>(s); p.x++)
                lvl->SetVoxelRaw(p,  LevelPixel::LevelGenRock);
}

static void add_spawns(Level *lvl)
{

    lvl->SetSpawn(0, generate_inside(lvl->GetSize(), BORDER));

    for (TankColor i = 1; i < tweak::world::MaxPlayers; i++)
    {
        bool done = false;
        while (!done)
        {
            /* Try adding a new point: */
            lvl->SetSpawn(i, generate_inside(lvl->GetSize(), BORDER));

            TankColor j;
            /* Make sure that point isn't too close to others: */
            for (j = 0; j < i; j++)
            {
                if (pt_dist(lvl->GetSpawn(i)->GetPosition(), lvl->GetSpawn(j)->GetPosition()) <
                    tweak::base::MinDistance * tweak::base::MinDistance)
                    break;
            }

            /* We're done if we were able to get through that list: */
            done = (j == i);
        }
    }
}

std::unique_ptr<Level> SimpleLevelGenerator::Generate(Size size)
{
    std::unique_ptr<Level> level = std::make_unique<Level>(size);
    Level * lvl = level.get();
    /* Levels default to all rock. Set this to all dirt: */
    fill_all(lvl, LevelPixel::LevelGenDirt);

    /* Add rock walls on all sides: */
    add_rock_lines(lvl, SIDE_TOP);
    add_rock_lines(lvl, SIDE_BOTTOM);
    add_rock_lines(lvl, SIDE_LEFT);
    add_rock_lines(lvl, SIDE_RIGHT);

    /* Rough it up a little: */
    rough_up(lvl);

    /* Add a few spawns, and we're good to go! */
    add_spawns(lvl);
    return level;
}

} // namespace levelgen::simple