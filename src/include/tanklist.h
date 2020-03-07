#pragma once
#include <vector>

typedef struct TankList TankList;

#include <iterators.h>
#include <tank.h>
#include <types.h>
#include <tweak.h>
#include <containers.h>

//struct TankList {
//public:
//	ValueContainer<Tank> list;
//	Level* lvl;
//	PList* pl;
//
//	TankList(Level* lvl, PList* pl);
//	~TankList();
//
//	Tank* AddTank(int id, Vector p);
//	void RemoveTank(int id);
//	Tank* GetTankAtPoint(int x, int y, int ignored);
//	bool CheckForCollision(Tank& tank, Position testPos, int testDirection);
//
//	// iterable
//	DereferenceIterator<decltype(list)::iterator> begin() { return dereference_iterator(list.begin()); }
//	DereferenceIterator<decltype(list)::iterator> end() { return dereference_iterator(list.end()); }
//	DereferenceIterator<decltype(list)::const_iterator> cbegin() const { return dereference_iterator(list.cbegin()); }
//	DereferenceIterator<decltype(list)::const_iterator> cend() const { return dereference_iterator(list.cend()); }
//};

struct TankList {
public:
	std::vector<std::unique_ptr<Tank>> list;
	Level* lvl;
	PList* pl;

	TankList(Level* lvl, PList* pl);
	~TankList();

	Tank* AddTank(int id, Vector p);
	void RemoveTank(int id);
	Tank* GetTankAtPoint(int x, int y, int ignored);
	bool CheckForCollision(Tank& tank, Position testPos, int testDirection);

	// iterable
	DereferenceIterator<decltype(list)::iterator> begin() { return dereference_iterator(list.begin()); }
	DereferenceIterator<decltype(list)::iterator> end() { return dereference_iterator(list.end()); }
	DereferenceIterator<decltype(list)::const_iterator> cbegin() const { return dereference_iterator(list.cbegin()); }
	DereferenceIterator<decltype(list)::const_iterator> cend() const { return dereference_iterator(list.cend()); }
};

template <typename TFunc>
inline void for_each_tank(TankList& tl, TFunc&& tank_func)
{
	for(auto& tank : tl)
		tank_func(&tank);
}


