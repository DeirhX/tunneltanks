#include "color_palette.h"
#include "terrain_pixel.h"
#include "trace.h"
#include "world.h"
#include "gamelib/sdl/bitmap.h"
#include <cassert>
#include <cstdlib>
#include <random.h>
#include <terrain.h>
#include <tweak.h>
#include <types.h>

Terrain::Terrain(Size size) : size(size), data(size)
{
    std::ranges::fill(this->data, TerrainPixel::LevelGenRock);
}

void Terrain::OnConnectWorld(World *) {}

void Terrain::BeginGame()
{

}

void Terrain::SetTerrainData(int i, TerrainPixel value)
{
    this->data[i] = value;
    /*if (this->is_ready)
        this->dirt_adjacency_data.Invalidate(i);*/
}

void Terrain::SetTerrainData(Position pos, TerrainPixel value)
{
    this->data[pos.y * this->size.x + pos.x] = value;
    /*if (this->is_ready)
        this->dirt_adjacency_data.Invalidate(pos);*/
}

void Terrain::SetPixel(Position pos, TerrainPixel voxel)
{
    assert(IsInside(pos));

    SetTerrainData(pos.y * this->size.x + pos.x, voxel);
    CommitPixel(pos);
}

void Terrain::SetVoxelRaw(Position pos, TerrainPixel voxel) { SetTerrainData(pos, voxel); }

/*
void Level::SetVoxelRaw(int offset, TerrainPixel voxel)
{
	SetLevelData(offset, voxel);
}
*/

int Terrain::CountNeighbors(Position pos, TerrainPixel value)
{
    return !!(value == GetVoxelRaw((pos.x - 1 + GetSize().x * (pos.y - 1)))) +
           !!(value == GetVoxelRaw((pos.x + GetSize().x * (pos.y - 1)))) +
           !!(value == GetVoxelRaw((pos.x + 1 + GetSize().x * (pos.y - 1)))) +
           !!(value == GetVoxelRaw((pos.x - 1 + GetSize().x * (pos.y)))) +
           !!(value == GetVoxelRaw((pos.x + 1 + GetSize().x * (pos.y)))) +
           !!(value == GetVoxelRaw((pos.x - 1 + GetSize().x * (pos.y + 1)))) +
           !!(value == GetVoxelRaw((pos.x + GetSize().x * (pos.y + 1)))) +
           !!(value == GetVoxelRaw((pos.x + 1 + GetSize().x * (pos.y + 1))));
}

void Terrain::MaterializeLevelTerrain()
{
    assert(!is_ready);
    this->GenerateDirtAndRocks();
    this->is_ready = true;
}

TerrainPixel Terrain::GetPixel(Position pos) const
{
    if (!IsInside(pos))
        return TerrainPixel::Rock;
    return this->data[pos.y * this->size.x + pos.x];
}

TerrainPixel Terrain::GetVoxelRaw(Position pos) const { return this->data[pos.y * this->size.x + pos.x]; }

//TerrainPixel Level::GetVoxelRaw(int address) const
//{
//	return this->data[address];
//}

void Terrain::GenerateDirtAndRocks()
{
    Position pos;
    for (pos.y = 0; pos.y < this->size.y; pos.y++)
        for (pos.x = 0; pos.x < this->size.x; pos.x++)
        {
            auto spot = this->GetPixel(pos);
            if (spot != TerrainPixel::LevelGenDirt)
                this->SetPixel(pos, TerrainPixel::Rock);
            else
                this->SetPixel(pos, Random.Bool(500) ? TerrainPixel::DirtLow : TerrainPixel::DirtHigh);
        }
}

DigResult Terrain::DigTankTunnel(Position pos, bool dig_with_torch)
{
    auto result = DigResult{};

    for (int ty = pos.y - 3; ty <= pos.y + 3; ty++)
        for (int tx = pos.x - 3; tx <= pos.x + 3; tx++)
        {
            TerrainPixel pixel = GetPixel({tx, ty});

            /* Don't take out the corners: */
            if ((tx == pos.x - 3 || tx == pos.x + 3) && (ty == pos.y - 3 || ty == pos.y + 3))
                continue;

            if (Pixel::IsDiggable(pixel))
            {
                SetPixel({tx, ty}, TerrainPixel::Blank);
                if (Pixel::IsDirt(pixel))
                    ++result.dirt;
            }
            else if (Pixel::IsTorchable(pixel) && dig_with_torch && Random.Bool(tweak::world::DigThroughRockChance))
            {
                SetPixel({tx, ty}, TerrainPixel::Blank);
                if (Pixel::IsMineral(pixel))
                    ++result.minerals;
            }
        }

    return result;
}

bool Terrain::IsInside(Position pos) const
{
    return !(pos.x < 0 || pos.y < 0 || pos.x >= this->size.x || pos.y >= this->size.y);
}

void Terrain::DrawChangesToSurface(WorldRenderSurface & world_surface)
{
    for (auto& pos : this->change_list)
        world_surface.SetPixel(pos, GetVoxelColor(this->GetPixel(pos)));
    this->change_list.clear();
}

void Terrain::DrawAllToSurface(WorldRenderSurface & world_surface)
{
    for (int y = 0; y < this->size.y; y++)
    {
        for (int x = 0; x < this->size.x; x++)
        {
            world_surface.SetPixel({x, y}, GetVoxelColor(this->GetPixel({x, y})));
        }
    }
}

void Terrain::CommitPixel(Position pos)
{
    change_list.push_back(pos);
}

void Terrain::CommitPixels(const std::vector<Position>& positions)
{
    for (const auto& pos : positions)
        change_list.push_back(pos);
}


Color Terrain::GetVoxelColor(TerrainPixel voxel)
{
    if (voxel == TerrainPixel::DirtHigh)
        return Palette.Get(Colors::DirtHigh);
    else if (voxel == TerrainPixel::DirtLow)
        return Palette.Get(Colors::DirtLow);
    else if (voxel == TerrainPixel::DirtGrow)
        return Palette.Get(Colors::DirtGrow);
    else if (voxel == TerrainPixel::Rock)
        return Palette.Get(Colors::Rock);
    else if (voxel == TerrainPixel::DecalLow)
        return Palette.Get(Colors::DecalLow);
    else if (voxel == TerrainPixel::DecalHigh)
        return Palette.Get(Colors::DecalHigh);
    else if (voxel == TerrainPixel::ConcreteLow)
        return Palette.Get(Colors::ConcreteLow);
    else if (voxel == TerrainPixel::ConcreteHigh)
        return Palette.Get(Colors::ConcreteHigh);
    else if (voxel == TerrainPixel::BaseBarrier)
        return Palette.Get(Colors::Blank);
    else if (voxel == TerrainPixel::Blank)
        return Palette.Get(Colors::Blank);
    else if (voxel == TerrainPixel::EnergyLow)
        return Palette.Get(Colors::EnergyFieldLow);
    else if (voxel == TerrainPixel::EnergyMedium)
        return Palette.Get(Colors::EnergyFieldMedium);
    else if (voxel == TerrainPixel::EnergyHigh)
        return Palette.Get(Colors::EnergyFieldHigh);
    else if (Pixel::IsBase(voxel))
        return Palette.GetTank(static_cast<char>(voxel) - static_cast<char>(TerrainPixel::BaseMin))[0];
    else
    {
        assert(!"Unknown voxel.");
        return {};
    }
}

/* Dumps a level into a BMP file: */
void Terrain::DumpBitmap(const char * filename) const
{
    //for (int i = 0; i < 20; ++i)
    {
        auto color_data = ColorBitmap{this->size};

        for (int i = 0; i < this->size.y * this->size.x; i++)
            color_data[i] = static_cast<Color>(GetVoxelColor(this->GetVoxelRaw(i)));

        {
            [[maybe_unused]] auto trace = MeasureFunction<5>("DumpBitmap");
            BmpFile::SaveToFile(color_data, filename);
        }
    }
}

