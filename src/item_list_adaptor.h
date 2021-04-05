#pragma once
#include "containers.h"

template <typename... ItemType>
class ItemListAdaptor
{
  protected:
    using ItemContainer = MultiTypeContainer<ItemType...>;
    ItemContainer items;

  public:
    ItemListAdaptor() = default;

    template <typename TMachine, typename... TConstructArgs>
    TMachine & Emplace(TConstructArgs &&... args)
    {
        return this->items.ConstructElement<TMachine>(std::forward<TConstructArgs>(args)...);
    }
    template <typename TItem>
    TItem & Add(TItem && item)
    {
        return this->items.Add(std::forward<TItem>(item));
    }
    template <typename TItem>
    void Add(std::vector<TItem> && other_array)
    {
        for (auto & item : other_array)
            this->Add(item);
    }

    template <typename TItem>
    void Remove(TItem & item)
    {
        item.Invalidate();
    }
    void RemoveAll() { items.RemoveAll(); };
    void Shrink() { this->items.Shrink(); }
};
