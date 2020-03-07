#pragma once
typedef struct Projectile Projectile;
typedef struct PList PList;
typedef struct PListNode PListNode;


#include <level.h>
#include <tank.h>
#include <tanklist.h>
#include <containers.h>

PList* plist_new();
void plist_destroy(PList* pl);

void plist_push_bullet(PList* pl, Tank* t);
void plist_push_explosion(PList* pl, int x, int y, int count, int r, int ttl);

void plist_step(PList* pl, Level* b, TankList* tl);

void plist_clear(PList* pl, DrawBuffer* b);
void plist_draw(PList* pl, DrawBuffer* b);



