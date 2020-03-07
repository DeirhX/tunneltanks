#pragma once
struct Projectile;
struct PList;
struct PListNode;

PList* plist_new();
void plist_destroy(PList* pl);

void plist_push_bullet(PList* pl, struct Tank* t);
void plist_push_explosion(PList* pl, int x, int y, int count, int r, int ttl);

void plist_step(PList* pl, struct Level* b, struct TankList* tl);

void plist_clear(PList* pl, struct DrawBuffer* b);
void plist_draw(PList* pl, struct DrawBuffer* b);



