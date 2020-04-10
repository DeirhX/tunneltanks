#pragma once
#include <cstdint>
#include <vector>


#include "containers.h"
#include "level_pixel.h"
#include "types.h"

/*
 * Adjacency Data
 * Caches possibly expensive lookups into level_data, invalidated on each write to level_data
 *   Note: the invalidates can get expensive if we use too many maps
 * -1 is a reserved value for marking invalid / needing refresh
 */
template <typename ValueType>
class LevelAdjacencyData
{
  protected:
    using Container = std::vector<ValueType>;
    Container array;
    Size size;

    /* Take care not to interfere with this value */
    constexpr static ValueType Invalid = std::numeric_limits<ValueType>::max();

  protected:
    LevelAdjacencyData(Size size) : size(size)
    {
        array.resize(size.x * size.y, Invalid);
    }

  public:
    template <typename AccumulationFuncType>
    ValueType Get(Position pos, AccumulationFuncType neighbor_accum_func);
    void Set(int i, ValueType value) { this->array[i] = value; }
    void Set(Position pos, ValueType value) { this->array[pos.x + pos.y * this->size.x] = value; }
    void Invalidate(int i) { Invalidate(Position{i % size.x, i / size.x}); };
    void Invalidate(Position pos);

    template <typename AccumulationFuncType>
    static ValueType AccumulateFromNeighbors(Position pos, Size size, AccumulationFuncType accumulation_func);
};

class DirtAdjacencyData : public LevelAdjacencyData<uint8_t>
{
    using Parent = LevelAdjacencyData<uint8_t>;
    Container2D<LevelPixel> * level_data;

  public:
    DirtAdjacencyData(Size size, Container2D<LevelPixel> * level_data);
    uint8_t Get(Position pos);
};

template <typename ValueType>
template <typename AccumulationFuncType>
ValueType LevelAdjacencyData<ValueType>::Get(Position pos, AccumulationFuncType neighbor_accum_func)
{
    uint8_t & ret_val = this->array[pos.x + pos.y * this->size.x];
    if (ret_val == Invalid)
    {
        ret_val = AccumulateFromNeighbors(pos, this->size, neighbor_accum_func);
    }
    return ret_val;
}

template <typename ValueType>
void LevelAdjacencyData<ValueType>::Invalidate(Position pos)
{
    /* This might invalidate even pixels not really touching. But I think it's better to have as few conditional statements as possible. */
    Set(std::max(0, pos.x - 1 + this->size.x * (pos.y - 1)), Invalid);
    Set(std::max(0, pos.x + this->size.x * (pos.y - 1)), Invalid);
    Set(std::max(0, pos.x + 1 + this->size.x * (pos.y - 1)), Invalid);
    Set(std::max(0, pos.x - 1 + this->size.x * (pos.y)), Invalid);
    Set(pos.x + this->size.x * (pos.y), Invalid);
    Set(std::min(int(this->array.size()) - 1, pos.x + 1 + this->size.x * (pos.y)), Invalid);
    Set(std::min(int(this->array.size()) - 1, pos.x - 1 + this->size.x * (pos.y + 1)), Invalid);
    Set(std::min(int(this->array.size()) - 1, pos.x + this->size.x * (pos.y + 1)), Invalid);
    Set(std::min(int(this->array.size()) - 1, pos.x + 1 + this->size.x * (pos.y + 1)), Invalid);
}

template <typename ValueType>
template <typename AccumulationFuncType>
ValueType LevelAdjacencyData<ValueType>::AccumulateFromNeighbors(Position pos, Size size,
                                                                 AccumulationFuncType accumulation_func)
{
    ValueType value_sum = {};
    value_sum += accumulation_func(Position{std::max(pos.x - 1, 0),          std::max(pos.y - 1, 0)});
    value_sum += accumulation_func(Position{pos.x,                                       std::max(pos.y - 1, 0)});
    value_sum += accumulation_func(Position{std::min(pos.x + 1, size.x - 1), std::max(pos.y - 1, 0)});
    value_sum += accumulation_func(Position{std::max(pos.x - 1, 0),          pos.y});
    /* Center */
    value_sum += accumulation_func(Position{std::min(pos.x + 1, size.x - 1), pos.y});
    value_sum += accumulation_func(Position{std::max(pos.x - 1, 0),          std::min(pos.y + 1, size.y - 1)});
    value_sum += accumulation_func(Position{pos.x,                                       std::min(pos.y + 1, size.y - 1)});
    value_sum += accumulation_func(Position{std::min(pos.x + 1, size.x - 1), std::min(pos.y + 1, size.y - 1)});
    return value_sum;

    /*
    Position neighbor;
    auto max_x = std::min(pos.x + 1, size.x - 1);
    auto max_y = std::min(pos.y + 1, size.y - 1);
    for (neighbor.x = std::max(0, pos.x - 1); neighbor.x <= max_x; ++neighbor.x)
        for (neighbor.y = std::max(0, pos.y - 1); neighbor.y <= max_y; ++neighbor.y)
            value_sum += accumulation_func(neighbor);

    return value_sum;
    */
}



//template <typename ComputeFunc>
//uint8_t LevelAdjacencyDataCompressed::operator[](int i) const
//{
//    uint8_t val = this->array[i / 2];
//    if (i % 2 == 1)
//        return val >> 4;
//    return val & 0xF;
//}
//
//template <typename ComputeFunc>
//void LevelAdjacencyDataCompressed::Set(int i, uint8_t value)
//{
//    assert(value <= 0xF);
//    uint8_t & val = this->array[i / 2];
//    if (i % 2 == 1)
//        val = (val & 0xF) | (value << 4);
//    val = val & (0xF0 | (value & 0xF));
//}