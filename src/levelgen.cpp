#include <cstdio>
#include <cstring>
#include <array>
#include <gamelib.h>
#include <levelgen.h>
#include <memory>

#include <levelgen_braid.h>
#include <levelgen_maze.h>
#include <levelgen_simple.h>
#include <levelgen_toast.h>
#include <trace.h>

namespace levelgen
{

struct LevelGeneratorDesc
{
    LevelGeneratorType id;
    const char * name;
    std::unique_ptr<GeneratorAlgorithm> generator;
    const char * desc;
};

/* === All the generator headers go here: =================================== */

/* Add an entry for every generator: */
std::array<LevelGeneratorDesc, 4> LevelGenerators = {
    LevelGeneratorDesc{.id = LevelGeneratorType::Toast,
                       .name = "toast",
                       .generator = std::make_unique<levelgen::toast::ToastLevelGenerator>(),
                       .desc = "Twisty, cavernous maps."},
    LevelGeneratorDesc{.id = LevelGeneratorType::Braid,
                       .name = "braid",
                       .generator = std::make_unique<levelgen::braid::BraidLevelGenerator>(),
                       .desc = "Maze-like maps with no dead ends."},
    LevelGeneratorDesc{.id = LevelGeneratorType::Maze,
                       .name = "maze",
                       .generator = std::make_unique<levelgen::maze::MazeLevelGenerator>(),
                       .desc = "Complicated maps with a maze surrounding the bases."},
    LevelGeneratorDesc{.id = LevelGeneratorType::Simple,
                       .name = "simple",
                       .generator = std::make_unique<levelgen::simple::SimpleLevelGenerator>(),
                       .desc = "Simple rectangular maps with ragged sides."},

};

LevelGeneratorType LevelGenerator::FromName(const char * name)
{
    if (name)
    {
        /* Look for the id: */
        for (auto & generator : LevelGenerators)
        {
            if (!strcmp(name, generator.name))
            {
                return generator.id;
            }
        }
    }
    return LevelGeneratorType::None;
}

/* ========================================================================== */

/* Linear search is ok here, since there aren't many level generators: */
GeneratedLevel LevelGenerator::Generate(LevelGeneratorType generator, Size size)
{

    /* If 'id' is null, go with the default: */
    if (generator == LevelGeneratorType::None)
        generator = LevelGenerators[0].id;

    auto found = std::find_if(LevelGenerators.begin(), LevelGenerators.end(),
                              [generator](const auto & desc) { return desc.id == generator; });
    if (found == LevelGenerators.end())
    {
        /* Report what level generator we found: */
        gamelib_print("Using default level generator: '%s'\n", LevelGenerators[0].id);
    }
    gamelib_print("Using level generator: '%s'\n", found->name);
    {
        Stopwatch<std::chrono::milliseconds> s;

        /* Ok, now generate the level: */
        std::unique_ptr<World> world =  found->generator->Generate(size);

        gamelib_print("Level generated in: ");
        auto msecs = s.GetElapsed();
        gamelib_print("%lld.%03lld sec\n", msecs.count() / 1000, msecs.count() % 1000);

        return {.world = std::move(world),
                .generation_time = std::chrono::duration_cast<std::chrono::milliseconds>(msecs)};
    }
}

/* Will print a specified number of spaces to the file: */
static void put_chars(size_t i, char c)
{
    while (i--)
        gamelib_print("%c", c);
}

void LevelGenerator::PrintAllGenerators(FILE *)
{
    size_t max_id = 7;
    size_t max_desc = strlen("Description:");

    /* Get the longest ID/Description length: */
    for (auto & generator : LevelGenerators)
    {
        max_id = std::max(max_id, strlen(generator.name));
        max_desc = std::max(max_desc, strlen(generator.desc));
    }

    /* Print the header: */
    gamelib_print("ID:  ");
    put_chars(max_id - strlen("ID:"), ' ');
    gamelib_print("Description:\n");
    put_chars(max_id + max_desc + 2, '-');
    gamelib_print("\n");

    /* Print all things: */
    for (auto i = 0u; i < LevelGenerators.size(); i++)
    {
        gamelib_print("%s  ", LevelGenerators[i].name);
        put_chars(max_id - strlen(LevelGenerators[i].name), ' ');
        gamelib_print("%s%s\n", LevelGenerators[i].desc, i == 0 ? " (Default)" : "");
    }
    gamelib_print("\n");
}


int Queries::CountNeighborValues(Position pos, Terrain * level)
{
    return (char)level->GetVoxelRaw((pos.x - 1 + level->GetSize().x * (pos.y - 1))) +
           (char)level->GetVoxelRaw((pos.x + level->GetSize().x * (pos.y - 1))) +
           (char)level->GetVoxelRaw((pos.x + 1 + level->GetSize().x * (pos.y - 1))) +
           (char)level->GetVoxelRaw((pos.x - 1 + level->GetSize().x * (pos.y))) +
           (char)level->GetVoxelRaw((pos.x + 1 + level->GetSize().x * (pos.y))) +
           (char)level->GetVoxelRaw((pos.x - 1 + level->GetSize().x * (pos.y + 1))) +
           (char)level->GetVoxelRaw((pos.x + level->GetSize().x * (pos.y + 1))) +
           (char)level->GetVoxelRaw((pos.x + 1 + level->GetSize().x * (pos.y + 1)));
}

} // namespace levelgen