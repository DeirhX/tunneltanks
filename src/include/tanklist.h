#ifndef _TANK_LIST_H_
#define _TANK_LIST_H_
#include <vector>

typedef struct TankList TankList;

#include <tank.h>
#include <types.h>
#include <tweak.h>

struct TankList {
public:
	std::vector<Tank*> list;
	Level* lvl;
	PList* pl;

	TankList(Level* lvl, PList* pl);
	~TankList();
};

Tank *tanklist_add_tank(TankList *tl, unsigned id, Vector p) ;
int   tanklist_remove_tank(TankList *tl, unsigned id) ;

Tank *tanklist_check_point(TankList *tl, unsigned x, unsigned y, unsigned ignored) ;
int tanklist_check_collision(TankList *tl, Vector p, unsigned dir, unsigned ignored) ;

Tank *tanklist_get(TankList *tl, unsigned id) ;

template <typename TFunc>
inline void tanklist_map(TankList* tl, TFunc&& tank_func)
{
	do {
		unsigned i;
		for (i = 0; i < MAX_TANKS; i++) {
			Tank* t = tanklist_get(tl, i);
			if (!t) continue;
			tank_func(t);
		}
	} while (0);
}

#endif /* _TANK_LIST_H_ */

