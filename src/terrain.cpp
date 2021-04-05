#include "color_palette.h"
#include "gamelib/sdl/bitmap.h"
#include "terrain_pixel.h"
#include "trace.h"
#include "world.h"
#include <cassert>
#include <cstdlib>
#include <Terrain.h>
#include <random.h>
#include <tweak.h>
#include <types.h>

Terrain::Terrain(Size size) : size(size), data(size), surfaces(size)
{
    surfaces.terrain_surface.SetDefaultColor(static_cast<Color>(Palette.Get(Colors::Rock)));
    surfaces.objects_surface.SetDefaultColor({});
    std::fill(this->data.begin(), this->data.end(), TerrainPixel::LevelGenRock);
}

void Terrain::OnConnectWorld(World *) {}

void Terrain::BeginGame()
{
    for (TankBase & tank_base : this->tank_bases)
    {
        tank_base.BeginGame();
    }
}

void Terrain::SetLevelData(int i, TerrainPixel value)
{
    this->data[i] = value;
    /*if (this->is_ready)
        this->dirt_adjacency_data.Invalidate(i);*/
}

void Terrain::SetLevelData(Position pos, TerrainPixel value)
{
    this->data[pos.y * this->size.x + pos.x] = value;
    /*if (this->is_ready)
        this->dirt_adjacency_data.Invalidate(pos);*/
}

void Terrain::SetPixel(Position pos, TerrainPixel voxel)
{
    assert(IsInside(pos));

    SetLevelData(pos.y * this->size.x + pos.x, voxel);
    CommitPixel(pos);
}

void Terrain::SetVoxelRaw(Position pos, TerrainPixel voxel) { SetLevelData(pos, voxel); }

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

void Terrain::MaterializeLevelTerrainAndBases()
{
    assert(!is_ready);
    this->GenerateDirtAndRocks();
    this->CreateBases();
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

void Terrain::CreateBase(Position pos, TankColor color)
{
    if (color >= tweak::world::MaxPlayers)
        return;

    for (int y = -tweak::base::BaseSize / 2; y <= tweak::base::BaseSize / 2; y++)
    {
        for (int x = -tweak::base::BaseSize / 2; x <= tweak::base::BaseSize / 2; x++)
        {
            Position pix = pos + Offset{x, y};
            if (abs(x) == tweak::base::BaseSize / 2 || abs(y) == tweak::base::BaseSize / 2)
            { // Outline
                if (x >= -tweak::base::DoorSize / 2 && x <= tweak::base::DoorSize / 2)
                    SetPixel(pix, TerrainPixel::BaseBarrier);
                else
                    SetPixel(pix, static_cast<TerrainPixel>(static_cast<char>(TerrainPixel::BaseMin) + color));
            }
            else
                SetPixel(pix, TerrainPixel::Blank);
        }
    }
}

/* TODO: Rethink the method for adding bases, as the current method DEMANDS that
 *       you use MAX_TANKS tanks. */
void Terrain::CreateBases()
{
    for (TankColor i = 0; i < tweak::world::MaxPlayers; i++)
    {
        CreateBase({this->tank_bases[i].GetPosition().x, this->tank_bases[i].GetPosition().y}, i);
    }
}

TankBase * Terrain::GetSpawn(TankColor color)
{
    assert(color >= 0 && color < (int)this->tank_bases.size());
    return &this->tank_bases[color];
}

void Terrain::SetSpawn(TankColor color, std::unique_ptr<TankBase> && tank_base)
{
    assert(color >= 0 && color < tweak::world::MaxPlayers);
    if (TankColor(this->tank_bases.size()) <= color)
        this->tank_bases.resize(color + 1);
    this->tank_bases[color] = *tank_base;
}

void Terrain::SetSpawn(TankColor color, Position position)
{
    this->SetSpawn(color, std::make_unique<TankBase>(position, color));
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

void Terrain::CommitAll()
{
    for (int y = 0; y < this->size.y; y++)
    {
        for (int x = 0; x < this->size.x; x++)
        {
            CommitPixel({x, y});
        }
    }
}

bool Terrain::IsInside(Position pos) const
{
    return !(pos.x < 0 || pos.y < 0 || pos.x >= this->size.x || pos.y >= this->size.y);
}

void Terrain::CommitPixel(Position pos) { surfaces.terrain_surface.SetPixel(pos, GetVoxelColor(this->GetPixel(pos))); }

void Terrain::CommitPixels(const std::vector<Position> & positions)
{
    for (auto & position : positions)
        surfaces.terrain_surface.SetPixel(position, GetVoxelColor(this->GetPixel(position)));
}

/* TODO: This needs to be done in a different way, as this approach will take 
 * MAX_TANKS^2 time to do all collision checks for all tanks. It should only
 * take MAX_TANKS time. */
TankBase * Terrain::CheckBaseCollision(Position pos)
{
    for (TankColor id = 0; id < tweak::world::MaxPlayers; id++)
    {
        if (this->tank_bases[id].IsInside(pos))
        {
            return &this->tank_bases[id];
        }
    }

    return nullptr;
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
