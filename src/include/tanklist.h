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

	Tank* AddTank(int id, Vector p);
	void RemoveTank(int id);
};

Tank *tanklist_check_point(TankList *tl, int x, int y, int ignored) ;
int tanklist_check_collision(TankList *tl, Vector p, int dir, int ignored) ;

Tank *tanklist_get(TankList *tl, int id) ;

template <typename TFunc>
inline void tanklist_map(TankList* tl, TFunc&& tank_func)
{
	do {
		int i;
		for (i = 0; i < MAX_TANKS; i++) {
			Tank* t = tanklist_get(tl, i);
			if (!t) continue;
			tank_func(t);
		}
	} while (0);
}

#endif /* _TANK_LIST_H_ */

