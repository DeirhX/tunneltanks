#include <cstdio>
#include <cstdlib>
#include <ctime>

#include <gamelib.h>
#include <levelgen.h>
#include <levelgenutil.h>
#include <level.h>
#include <memalloc.h>
#include <random.h>

#include <deque>
#include <atomic>
#include <thread>
#include <future>
#include <queue>

namespace levelgen::toast {

/* Configuration Constants: */
constexpr int BORDER = 30;
constexpr int FILTER = 70;
constexpr int ODDS = 300;
constexpr int FILLRATIO = 65;
constexpr int TREESIZE = 150;

typedef struct Pairing {
	int dist, a, b;
} Pairing;


using PositionQueue = circular_buffer_adaptor<Position>;
	
#ifdef _TESTING
static void level_draw_ascii(Level *lvl) {
	int x,y;
	
	for(y=0; y<lvl->height; y++) {
		for(x=0; x<lvl->width; x++)
			printf("%c", lvl->array[ y*lvl->width + x ]?'#':' ');
		printf("\n");
	}
}
#endif /* _TESTING */

/*----------------------------------------------------------------------------*
 * STAGE 1: Generate a random tree                                            *
 *----------------------------------------------------------------------------*/

static int pairing_cmp(const void *a, const void *b) {
	return ((Pairing *)a)->dist - ((Pairing *)b)->dist;
}

static void generate_tree(Level *lvl) {
	int *dsets, paircount;
	int i, j;
	int k;
	Position *points;
	Pairing *pairs;
	
	/* Get an array of disjoint set IDs: */
	dsets = static_cast<int*>(get_mem( sizeof(int) * TREESIZE ));
	for(i=0; i<TREESIZE; i++) dsets[i] = i;
	
	/* Randomly generate all points: */
	points = static_cast<Position*>(get_mem( sizeof(Position) * TREESIZE ));
	for(i=0; i<TREESIZE; i++) points[i] = pt_rand(lvl->GetSize(), BORDER);
	
	/* While we're here, copy in some of those points: */
	lvl->SetSpawn(0, points[0]);
	for(i=1,j=1; i<TREESIZE && j<MAX_TANKS; i++) {
		for(k=0; k<j; k++) {
			if(pt_dist(points[i],lvl->GetSpawn(k)) < MIN_SPAWN_DIST*MIN_SPAWN_DIST)
				break;
		}
		
		if(k!=j) continue;
		lvl->SetSpawn(j++, points[i]);
	}
	if(j!=MAX_TANKS) {
		/* TODO: More robust error handling. */
		gamelib_error("OH SHUCKS OH SHUCKS OH SHUCKS\n");
		exit(1);
	}
	/* Get an array of all point-point pairings: */
	paircount = TREESIZE*(TREESIZE-1) / 2;
	pairs = static_cast<Pairing*>(get_mem( sizeof(Pairing) * (paircount) ));

	/* Set up all the pairs, and sort them: */
	for(k=i=0; i<TREESIZE; i++)
		for(j=i+1; j<TREESIZE; j++, k++) {
			pairs[k].a = i; pairs[k].b = j;
			pairs[k].dist = pt_dist(points[i], points[j]);
		}
	qsort(pairs, paircount, sizeof(Pairing), pairing_cmp);
	for(i=j=0; i<paircount; i++) {
		int aset, bset;
		
		/* Trees only have |n|-1 edges, so call it quits if we've selected that
		 * many: */
		if(j>=TREESIZE-1) break;
		
		aset = dsets[pairs[i].a]; bset = dsets[pairs[i].b];
		if(aset == bset) continue;
		
		/* Else, these points are in different disjoint sets. "Join" them by
		 * drawing them, and merging the two sets: */
		j+=1;
		for(k=0; k<TREESIZE; k++) 
			if(dsets[k] == bset) 
				dsets[k] = aset;
		draw_line(lvl, points[pairs[i].a], points[pairs[i].b], 0, 0);
	}
	
	/* We don't need this data anymore: */
	free_mem(pairs);
	free_mem(points);
	free_mem(dsets);
}


/*----------------------------------------------------------------------------*
 * STAGE 2: Randomly expand upon the tree                                     *
 *----------------------------------------------------------------------------*/

/* Some cast-to-int tricks here may be fun... ;) */
//static int has_neighbor(Level *lvl, int x, int y) {
//	if (!lvl->GetVoxelRaw({ x - 1, y - 1})) return 1;
//	if (!lvl->GetVoxelRaw({ x,    y - 1 })) return 1;
//	if (!lvl->GetVoxelRaw({ x + 1, y - 1 })) return 1;
//	if (!lvl->GetVoxelRaw({ x - 1, y  })) return 1;
//	if (!lvl->GetVoxelRaw({ x + 1, y  })) return 1;
//	if (!lvl->GetVoxelRaw({ x - 1, y + 1 })) return 1;
//	if (!lvl->GetVoxelRaw({ x,     y + 1 })) return 1;
//	if (!lvl->GetVoxelRaw({ x + 1, y + 1 })) return 1;
//	return 0;
//}
//
//
// Much less instructions. Optimizer cannot see it through and fold it :(
static int has_neighbor(Level* lvl, int x, int y) {
	if (!lvl->GetVoxelRaw({ x - 1 + lvl->GetSize().x * (y - 1) })) return 1;
	if (!lvl->GetVoxelRaw({ x + lvl->GetSize().x * (y - 1) })) return 1;
	if (!lvl->GetVoxelRaw({ x + 1 + lvl->GetSize().x * (y - 1) })) return 1;
	if (!lvl->GetVoxelRaw({ x - 1 + lvl->GetSize().x * (y) })) return 1;
	if (!lvl->GetVoxelRaw({ x + 1 + lvl->GetSize().x * (y) })) return 1;
	if (!lvl->GetVoxelRaw({ x - 1 + lvl->GetSize().x * (y + 1) })) return 1;
	if (!lvl->GetVoxelRaw({ x + lvl->GetSize().x * (y + 1) })) return 1;
	if (!lvl->GetVoxelRaw({ x + 1 + lvl->GetSize().x * (y + 1) })) return 1;
	return 0;
}


static void set_outside(Level *lvl, char val) {
	int i;
	Size size = lvl->GetSize();
	
	for (i = 0; i < size.x;   i++) lvl->VoxelRaw({ i, 0 }) = val;
	for (i = 0; i < size.x;   i++) lvl->VoxelRaw({ i, size.y - 1 }) = val;
	for (i = 1; i < size.y-1; i++) lvl->VoxelRaw({ 0, i }) = val;
	for (i = 1; i < size.y-1; i++) lvl->VoxelRaw({ size.x - 1, i }) = val;
}

static void expand_init(Level *lvl, PositionQueue& q) {
	for(int y = 1; y<lvl->GetSize().y-1; y++)
		for (int x = 1; x < lvl->GetSize().x - 1; x++) {
			int offset = x + y * lvl->GetSize().x;
			if (lvl->GetVoxelRaw(offset) && has_neighbor(lvl, x, y)) {
				lvl->SetVoxelRaw(offset, 2);
				q.push({ x, y });
			}
		}
}

static int expand_once(Level *lvl, queue_adaptor<Position>& q) {
	Position temp;
	int j, count = 0;
	
	size_t total = q.size();
	for(size_t i=0; i<total; i++) {
		int xodds, yodds, odds;
		
		q.pop(temp);

		xodds = ODDS * std::min(lvl->GetSize().x - temp.x, temp.x) / FILTER;
		yodds = ODDS * std::min(lvl->GetSize().y - temp.y, temp.y) / FILTER;
		odds  = std::min(std::min(xodds, yodds), ODDS);
		
		if(Random::Bool(odds)) {
			lvl->SetVoxelRaw(temp, 0);
			count++;
			
			/* Now, queue up any neighbors that qualify: */
			for(j=0; j<9; j++) {
				char *c;
				int tx, ty;
				
				if(j==4) continue;
				
				tx = temp.x + (j%3) - 1; ty = temp.y + (j/3) - 1;
				c = &lvl->VoxelRaw({ tx, ty });
				if(*c == 1) {
					*c = 2;
					q.push({ tx, ty });
				}
			}
		} else
			q.push(temp);
	}
	return count;
}

static void expand_process(Level* lvl, PositionQueue& q) {
	std::atomic<int> cur = 0;
	int goal = lvl->GetSize().x * lvl->GetSize().y * FILLRATIO / 100;
	constexpr int Workers = 8;

	/* Split into one queue per worker */
	/* TODO: Split per position quadrants */
	auto workerQueues = std::vector<queue_adaptor<Position>>();
	for (int i = 0; i < Workers; ++i) {
		workerQueues.emplace_back(/* Queue constructor */); 
	}
	int worker = 0;
	while (q.size()) {
		Position pos;
		q.pop(pos);
		workerQueues[worker].push(pos);
		worker = (worker + 1) % Workers;
	}


	std::atomic<bool> done = false;
	std::mutex mutex_threads_waiting;
	std::condition_variable cv_threads_waiting;
	int threads_waiting = 0;
	//std::mutex mutex_continue_thread;
	std::condition_variable cv_continue_thread;
	int min_pass = -1;
	int max_pass = 0;

	auto expand_loop = [&](queue_adaptor<Position>* qq) {
		
		int curr_pass = 0;
		while (!done) {
			{
				std::unique_lock lock(mutex_threads_waiting);
				--threads_waiting;
				min_pass = std::max(min_pass, curr_pass);
				cv_threads_waiting.notify_all();
			}
			
			cur += expand_once(lvl, *qq);
			
			{
				std::unique_lock lock(mutex_threads_waiting);
				++threads_waiting;
				cv_threads_waiting.notify_all();
			//}
			//{
				//std::unique_lock lock(cv_threads_waiting);
				while (curr_pass >= max_pass)
					cv_continue_thread.wait(lock);
			}
			++curr_pass;
		}
	};

	
	/* Launch workers on their own queues */
	threads_waiting = Workers;
	auto workers = std::vector<std::thread>();
	for (int i = 0; i < Workers; ++i) {
		workers.push_back(std::thread(expand_loop, &workerQueues[i]));
	}

	while (cur < goal) {
		{
			std::unique_lock lock(mutex_threads_waiting);
			while (threads_waiting != Workers || /* Already started new passes */
				  (max_pass != min_pass)) { /* All are still waiting for start */ 
				cv_threads_waiting.wait(lock);
			}

			if (cur >= goal) {
				done = true;
			}

			max_pass = min_pass + 10;
			cv_continue_thread.notify_all();
		}
	}

	/* End threadses */
	for (int i = 0; i < Workers; ++i) {
		workers[i].join();
	}
}

static void expand_cleanup(Level *lvl) {

	lvl->ForEachVoxel([](LevelVoxel& voxel) { voxel = !!voxel; });
}

static void randomly_expand(Level *lvl) {
	
	/* Experimentally, the queue never grew to larger than 3/50ths of the level
	 * size, so we can use that to save quite a bit of memory: */
	auto queue = PositionQueue(50000);
	//queue.resize(lvl->GetSize().x * lvl->GetSize().y * 3 / 50);
	
	expand_init(lvl, queue);
	expand_process(lvl, queue);
	expand_cleanup(lvl);
}


/*----------------------------------------------------------------------------*
 * STAGE 3: Smooth out the graph with a cellular automaton                    *
 *----------------------------------------------------------------------------*/


static int count_neighbors(Level* lvl, int x, int y) {
	return lvl->GetVoxelRaw({ x - 1 + lvl->GetSize().x * (y - 1) }) +
		lvl->GetVoxelRaw({ x + lvl->GetSize().x * (y - 1) }) +
		lvl->GetVoxelRaw({ x + 1 + lvl->GetSize().x * (y - 1) }) +
		lvl->GetVoxelRaw({ x - 1 + lvl->GetSize().x * (y) }) +
		lvl->GetVoxelRaw({ x + 1 + lvl->GetSize().x * (y) }) +
		lvl->GetVoxelRaw({ x - 1 + lvl->GetSize().x * (y + 1) }) +
		lvl->GetVoxelRaw({ x + lvl->GetSize().x * (y + 1) }) +
		lvl->GetVoxelRaw({ x + 1 + lvl->GetSize().x * (y + 1) });
}

//static int count_neighbors(Level* lvl, int x, int y) {
//	return lvl->GetVoxelRaw({ x - 1, y - 1 }) +
//		lvl->GetVoxelRaw({ x,   y - 1 }) +
//		lvl->GetVoxelRaw({ x + 1, y - 1 }) +
//		lvl->GetVoxelRaw({ x - 1, y }) +
//		lvl->GetVoxelRaw({ x + 1, y }) +
//		lvl->GetVoxelRaw({ x - 1, y + 1 }) +
//		lvl->GetVoxelRaw({ x,   y + 1 }) +
//		lvl->GetVoxelRaw({ x + 1, y + 1 });
//}
//
	
#define MIN2(a,b)   ((a<b) ? a : b)
#define MIN3(a,b,c) ((a<b) ? a : (b<c) ? b : c)
static int smooth_once(Level *lvl) {

	/* Smooth surfaces. Require at least 3 neighbors to keep alive. Spawn new at 5 neighbors. */
	auto smooth_step = [lvl](int from_y, int until_y) {
		int count = 0;
		Size size = lvl->GetSize();
		for (int y = from_y; y <= until_y; y++)
			for (int x = 1; x < size.x - 1; x++) {
				int n;
				LevelVoxel oldbit = lvl->GetVoxelRaw({ x, y });

				n = count_neighbors(lvl, x, y);
				lvl->SetVoxelRaw({ x, y }, oldbit ? (n >= 3) : (n > 4));

				count += lvl->GetVoxelRaw({ x, y }) != oldbit;
			}
		return count;
	};

	/* Parallelize the process using std::async and slicing jobs vertically by [y] */
	constexpr int Tasks = 8;
	auto tasks = std::vector<std::future<int>>();
	tasks.reserve(Tasks);
	const int first = 1;
	const int last = lvl->GetSize().y - 2;
	int curr = first;
	for (int i = 0; i < Tasks; ++i) {
		if (curr <= last) {
			int until = curr + (last - first) / Tasks;
			tasks.emplace_back(std::async(std::launch::async, smooth_step, curr, std::min(last, until)));
			curr = until + 1;
		}
	}
	/* Wait for everything done and sum the results */
	int count = 0;
	for (auto& task : tasks) {
		count += task.get();
	}
	return count;
}

static void smooth_cavern(Level *lvl) {
	set_outside(lvl, 0);
	while(smooth_once(lvl));
	set_outside(lvl, 1);
}


/*----------------------------------------------------------------------------*
 * MAIN FUNCTIONS:                                                            *
 *----------------------------------------------------------------------------*/

void toast_generator(Level *lvl) {
	generate_tree(lvl);
	randomly_expand(lvl);
	smooth_cavern(lvl);
}

#ifdef _TESTING

int main() {
	clock_t t, all;
	Level lvl;
	int i;
	
	rand_seed();
	
	/* We don't need a full-fledged Level object, so let's just half-ass one: */
	lvl.width = 1000; lvl.height = 500;
	lvl.array = get_mem(sizeof(char) * lvl.width * lvl.height);
	for(i=0; i<lvl.width * lvl.height; i++) lvl.array[i] = 1;
	
	TIMER_START(all);
	TIMER_START(t);
	generate_tree(&lvl);
	TIMER_STOP(t);
	
	TIMER_START(t);
	randomly_expand(&lvl);
	TIMER_STOP(t);
	
	TIMER_START(t);
	smooth_cavern(&lvl);
	TIMER_STOP(t);
	
	printf("-----------\nTotal: ");
	TIMER_STOP(all);
	
	level_draw_ascii(&lvl);
	
	free_mem(lvl.array);
	
	print_mem_stats();
	return 0;
}
#endif /* _TESTING */

}
