#pragma once
#include <vector>

struct TankList;

#include <iterators.h>
#include <tank.h>
#include <types.h>
#include <tweak.h>
#include <containers.h>

struct TankList {
public:
	ValueContainer<Tank> list;
	Level* lvl;
	ProjectileList* pl;

	TankList(Level* lvl, ProjectileList* pl);
	~TankList();

	Tank* AddTank(TankColor id, Vector p);
	void RemoveTank(TankColor id);
	Tank* GetTankAtPoint(int x, int y, int ignored);
	bool CheckForCollision(Tank& tank, Position testPos, int testDirection);

	// iterable
	decltype(list)::iterator begin() { return list.begin(); }
    decltype(list)::iterator end() { return list.end(); }
	/*DereferenceIterator<decltype(list)::iterator> begin() { return dereference_iterator(list.begin()); }
	DereferenceIterator<decltype(list)::iterator> end() { return dereference_iterator(list.end()); }
	DereferenceIterator<decltype(list)::const_iterator> cbegin() const { return dereference_iterator(list.cbegin()); }
	DereferenceIterator<decltype(list)::const_iterator> cend() const { return dereference_iterator(list.cend()); }*/


	template <typename TFunc>
	inline void for_each(TFunc&& tank_func)
	{
		for (auto& tank : *this)
			tank_func(&tank);
	}
};
