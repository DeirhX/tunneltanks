#ifndef _TANK_LIST_H_
#define _TANK_LIST_H_
#include <vector>

typedef struct TankList TankList;

#include <iterators.h>
#include <tank.h>
#include <types.h>
#include <tweak.h>

struct TankList {
public:
	std::vector<std::unique_ptr<Tank>> list;
	Level* lvl;
	PList* pl;

	TankList(Level* lvl, PList* pl);
	~TankList();

	Tank* AddTank(int id, Vector p);
	void RemoveTank(int id);

	// iterable
	DereferenceIterator<decltype(list)::iterator> begin() { return dereference_iterator(list.begin()); }
	DereferenceIterator<decltype(list)::iterator> end() { return dereference_iterator(list.end()); }
	DereferenceIterator<decltype(list)::const_iterator> cbegin() const { return dereference_iterator(list.cbegin()); }
	DereferenceIterator<decltype(list)::const_iterator> cend() const { return dereference_iterator(list.cend()); }
};

Tank *tanklist_check_point(TankList *tl, int x, int y, int ignored) ;
int tanklist_check_collision(TankList *tl, Vector p, int dir, int ignored) ;

Tank *tanklist_get(TankList *tl, int id) ;

template <typename TFunc>
inline void tanklist_map(TankList& tl, TFunc&& tank_func)
{
	for(auto& tank : tl)
		tank_func(&tank);
}

#endif /* _TANK_LIST_H_ */
