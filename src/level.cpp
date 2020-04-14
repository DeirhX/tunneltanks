#include "base.h"
#include <cstdlib>


#include <gamelib.h>
#include <level.h>
#include <random.h>
#include <tweak.h>
#include <types.h>

#include "exceptions.h"
#include <cassert>

#include "bitmap.h"
#include "color_palette.h"
#include "trace.h"
#include "level_pixel.h"
#include "world.h"


Level::Level(Size size)
    : size(size), data(size), surfaces(size)
{
    surfaces.terrain_surface.SetDefaultColor(static_cast<Color>(Palette.Get(Colors::Rock)));
    surfaces.objects_surface.SetDefaultColor({});
    std::fill(this->data.begin(), this->data.end(), LevelPixel::LevelGenRock);
}

void Level::OnConnectWorld(World * world)
{
    for (TankBase & tank_base : this->tank_bases)
        tank_base.RegisterLinkPoint(world);
}

void Level::SetLevelData(int i, LevelPixel value)
{
    this->data[i] = value;
    /*if (this->is_ready)
        this->dirt_adjacency_data.Invalidate(i);*/
}

void Level::SetLevelData(Position pos, LevelPixel value)
{
    this->data[pos.y * this->size.x + pos.x] = value;
    /*if (this->is_ready)
        this->dirt_adjacency_data.Invalidate(pos);*/
}

void Level::SetPixel(Position pos, LevelPixel voxel)
{
    assert(IsInBounds(pos));

    SetLevelData(pos.y * this->size.x + pos.x, voxel);
    CommitPixel(pos);
}

void Level::SetVoxelRaw(Position pos, LevelPixel voxel) { SetLevelData(pos, voxel); }

/*
void Level::SetVoxelRaw(int offset, LevelPixel voxel)
{
	SetLevelData(offset, voxel);
}
*/

int Level::CountNeighbors(Position pos, LevelPixel value)
{
    return !!(value == GetVoxelRaw({pos.x - 1 + GetSize().x * (pos.y - 1)})) +
           !!(value == GetVoxelRaw({pos.x + GetSize().x * (pos.y - 1)})) +
           !!(value == GetVoxelRaw({pos.x + 1 + GetSize().x * (pos.y - 1)})) +
           !!(value == GetVoxelRaw({pos.x - 1 + GetSize().x * (pos.y)})) +
           !!(value == GetVoxelRaw({pos.x + 1 + GetSize().x * (pos.y)})) +
           !!(value == GetVoxelRaw({pos.x - 1 + GetSize().x * (pos.y + 1)})) +
           !!(value == GetVoxelRaw({pos.x + GetSize().x * (pos.y + 1)})) +
           !!(value == GetVoxelRaw({pos.x + 1 + GetSize().x * (pos.y + 1)}));
}

void Level::MaterializeLevelTerrainAndBases()
{
    assert(!is_ready);
    this->GenerateDirtAndRocks();
    this->CreateBases();
    this->is_ready = true;
}

LevelPixel Level::GetPixel(Position pos) const
{
    if (!IsInBounds(pos))
        return LevelPixel::Rock;
    return this->data[pos.y * this->size.x + pos.x];
}

LevelPixel Level::GetVoxelRaw(Position pos) const { return this->data[pos.y * this->size.x + pos.x]; }

//LevelPixel Level::GetVoxelRaw(int address) const
//{
//	return this->data[address];
//}

void Level::GenerateDirtAndRocks()
{
    Position pos;
    for (pos.y = 0; pos.y < this->size.y; pos.y++)
        for (pos.x = 0; pos.x < this->size.x; pos.x++)
        {
            auto spot = this->GetPixel(pos);
            if (spot != LevelPixel::LevelGenDirt)
                this->SetPixel(pos, LevelPixel::Rock);
            else
                this->SetPixel(pos, Random.Bool(500) ? LevelPixel::DirtLow : LevelPixel::DirtHigh);
        }
}

void Level::CreateBase(Position pos, TankColor color)
{
    if (color >= tweak::world::MaxPlayers)
        return;

    for (int y = -tweak::world::BaseSize / 2; y <= tweak::world::BaseSize / 2; y++)
    {
        for (int x = -tweak::world::BaseSize / 2; x <= tweak::world::BaseSize / 2; x++)
        {
            Position pix = pos + Offset{x, y};
            if (abs(x) == tweak::world::BaseSize / 2 || abs(y) == tweak::world::BaseSize / 2)
            { // Outline
                if (x >= -tweak::world::BaseDoorSize / 2 && x <= tweak::world::BaseDoorSize / 2)
                    SetPixel(pix, LevelPixel::BaseBarrier);
                else
                    SetPixel(pix, static_cast<LevelPixel>(static_cast<char>(LevelPixel::BaseMin) + color));
            }
            else
                SetPixel(pix, LevelPixel::Blank);
        }
    }
}

/* TODO: Rethink the method for adding bases, as the current method DEMANDS that
 *       you use MAX_TANKS tanks. */
void Level::CreateBases()
{
    for (TankColor i = 0; i < tweak::world::MaxPlayers; i++)
    {
        CreateBase({this->tank_bases[i].GetPosition().x, this->tank_bases[i].GetPosition().y}, i);
    }

}

TankBase * Level::GetSpawn(TankColor color) 
{
    assert(color >= 0 && color < (int)this->tank_bases.size());
    return &this->tank_bases[color];
}

void Level::SetSpawn(TankColor color, std::unique_ptr<TankBase> && tank_base)
{
    assert(color >= 0 && color < tweak::world::MaxPlayers);
    if (TankColor(this->tank_bases.size()) <= color)
        this->tank_bases.resize(color + 1);
    this->tank_bases[color] = *tank_base;
}

void Level::SetSpawn(TankColor color, Position position)
{
    this->SetSpawn(color, std::make_unique<TankBase>(position));
}

DigResult Level::DigTankTunnel(Position pos, bool dig_with_torch)
{
    auto result = DigResult{};

    for (int ty = pos.y - 3; ty <= pos.y + 3; ty++)
        for (int tx = pos.x - 3; tx <= pos.x + 3; tx++)
        {
            LevelPixel pixel = GetPixel({tx, ty});

            /* Don't take out the corners: */
            if ((tx == pos.x - 3 || tx == pos.x + 3) && (ty == pos.y - 3 || ty == pos.y + 3))
                continue;

            if (Pixel::IsDiggable(pixel))
            {
                SetPixel({tx, ty}, LevelPixel::Blank);
                if (Pixel::IsDirt(pixel))
                    ++result.dirt;
            }
            else if (Pixel::IsTorchable(pixel) && dig_with_torch && Random.Bool(tweak::world::DigThroughRockChance))
            {
                SetPixel({tx, ty}, LevelPixel::Blank);
                if (Pixel::IsMineral(pixel))
                    ++result.minerals;
            }
        }

    return result;
}

void Level::CommitAll()
{
    for (int y = 0; y < this->size.y; y++)
    {
        for (int x = 0; x < this->size.x; x++)
        {
            CommitPixel({x, y});
        }
    }
}

bool Level::IsInBounds(Position pos) const
{
    return !(pos.x < 0 || pos.y < 0 || pos.x >= this->size.x || pos.y >= this->size.y);
}

void Level::CommitPixel(Position pos) { surfaces.terrain_surface.SetPixel(pos, GetVoxelColor(this->GetPixel(pos))); }

void Level::CommitPixels(const std::vector<Position> & positions)
{
    for (auto & position : positions)
        surfaces.terrain_surface.SetPixel(position, GetVoxelColor(this->GetPixel(position)));
}

/* TODO: This needs to be done in a different way, as this approach will take 
 * MAX_TANKS^2 time to do all collision checks for all tanks. It should only
 * take MAX_TANKS time. */
BaseCollision Level::CheckBaseCollision(Position pos, TankColor color)
{
    for (TankColor id = 0; id < tweak::world::MaxPlayers; id++)
    {
        if (std::abs(this->tank_bases[id].GetPosition().x - pos.x) < tweak::world::BaseSize / 2 &&
            std::abs(this->tank_bases[id].GetPosition().y - pos.y) < tweak::world::BaseSize / 2)
        {
            if (id == color)
                return BaseCollision::Yours;
            return BaseCollision::Enemy;
        }
    }

    return BaseCollision::None;
}

Color Level::GetVoxelColor(LevelPixel voxel)
{
    if (voxel == LevelPixel::DirtHigh)
        return Palette.Get(Colors::DirtHigh);
    else if (voxel == LevelPixel::DirtLow)
        return Palette.Get(Colors::DirtLow);
    else if (voxel == LevelPixel::DirtGrow)
        return Palette.Get(Colors::DirtGrow);
    else if (voxel == LevelPixel::Rock)
        return Palette.Get(Colors::Rock);
    else if (voxel == LevelPixel::DecalLow)
        return Palette.Get(Colors::DecalLow);
    else if (voxel == LevelPixel::DecalHigh)
        return Palette.Get(Colors::DecalHigh);
    else if (voxel == LevelPixel::ConcreteLow)
        return Palette.Get(Colors::ConcreteLow);
    else if (voxel == LevelPixel::ConcreteHigh)
        return Palette.Get(Colors::ConcreteHigh);
    else if (voxel == LevelPixel::BaseBarrier)
        return Palette.Get(Colors::Blank);
    else if (voxel == LevelPixel::Blank)
        return Palette.Get(Colors::Blank);
    else if (voxel == LevelPixel::EnergyLow)
        return Palette.Get(Colors::EnergyFieldLow);
    else if (voxel == LevelPixel::EnergyMedium)
        return Palette.Get(Colors::EnergyFieldMedium);
    else if (voxel == LevelPixel::EnergyHigh)
        return Palette.Get(Colors::EnergyFieldHigh);
    else if (Pixel::IsBase(voxel))
        return Palette.GetTank(static_cast<char>(voxel) - static_cast<char>(LevelPixel::BaseMin))[0];
    else
    {
        assert(!"Unknown voxel.");
        return {};
    }
}

/* Dumps a level into a BMP file: */
void Level::DumpBitmap(const char * filename) const
{
    //for (int i = 0; i < 20; ++i)
    {
        auto color_data = ColorBitmap{this->size};

        for (int i = 0; i < this->size.y * this->size.x; i++)
            color_data[i] = static_cast<Color>(GetVoxelColor(this->GetVoxelRaw(i)));

        {
            auto trace = MeasureFunction<5>("DumpBitmap");
            BmpFile::SaveToFile(color_data, filename);
        }
    }
}
