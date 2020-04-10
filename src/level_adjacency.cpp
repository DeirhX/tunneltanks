#include "level_adjacency.h"
#include "level.h"

DirtAdjacencyData::DirtAdjacencyData(Size size, Container2D<LevelPixel> * level_data)
    : LevelAdjacencyData<uint8_t>(size), level_data(level_data)
{
}

uint8_t DirtAdjacencyData::Get(Position pos)
{
    return Parent::Get(pos, [this](Position pos) {
        return uint8_t(Pixel::IsDirt(this->level_data->operator[](pos.x + pos.y * this->size.x)) ? 1 : 0);
    });
}
