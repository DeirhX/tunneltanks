#include <cstdlib>

#include <gamelib.h>
#include <levelgen.h>
#include <levelgen_toast.h>
#include <levelgenutil.h>
#include <level.h>
#include <memalloc.h>
#include <random.h>
#include <trace.h>

#include <atomic>
#include <future>

#include "containers_queue.h"
#include "parallelism.h"

namespace levelgen::toast {

/* Configuration Constants: */

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
	auto perf = MeasureFunction<2>{ __FUNCTION__ };

	int *dsets, paircount;
	int i, j;
	int k;
	Position *points;
	Pairing *pairs;
	
	/* Get an array of disjoint set IDs: */
	dsets = static_cast<int*>(get_mem( sizeof(int) * ToastParams::TreeSize ));
	for(i=0; i< ToastParams::TreeSize; i++) 
		dsets[i] = i;
	
	/* Randomly generate all points: */
	points = static_cast<Position*>(get_mem( sizeof(Position) * ToastParams::TreeSize ));
	for(i=0; i< ToastParams::TreeSize; i++)
		points[i] = generate_inside(lvl->GetSize(), ToastParams::BorderWidth);
	
	/* While we're here, copy in some of those points: */
	lvl->SetSpawn(0, points[0]);
	for(i=1,j=1; i< ToastParams::TreeSize && j< tweak::world::MaxPlayers; i++) {
		for(k=0; k<j; k++) {
            if (pt_dist(points[i], lvl->GetSpawn(TankColor(k))->GetPosition()) <
                tweak::base::MinDistance * tweak::base::MinDistance)
				break;
		}
		
		if(k!=j) continue;
		lvl->SetSpawn(TankColor(j++), points[i]);
	}
	if(j!= tweak::world::MaxPlayers) {
		/* TODO: More robust error handling. */
		gamelib_error("OH SHUCKS OH SHUCKS OH SHUCKS\n");
		exit(1);
	}
	/* Get an array of all point-point pairings: */
	paircount = ToastParams::TreeSize*(ToastParams::TreeSize-1) / 2;
	pairs = static_cast<Pairing*>(get_mem( sizeof(Pairing) * (paircount) ));

	/* Set up all the pairs, and sort them: */
	for(k=i=0; i< ToastParams::TreeSize; i++)
		for(j=i+1; j< ToastParams::TreeSize; j++, k++) {
			pairs[k].a = i; pairs[k].b = j;
			pairs[k].dist = pt_dist(points[i], points[j]);
		}
	qsort(pairs, paircount, sizeof(Pairing), pairing_cmp);
	for(i=j=0; i<paircount; i++) {
		int aset, bset;
		
		/* Trees only have |n|-1 edges, so call it quits if we've selected that
		 * many: */
		if(j>= ToastParams::TreeSize-1) break;
		
		aset = dsets[pairs[i].a]; bset = dsets[pairs[i].b];
		if(aset == bset) continue;
		
		/* Else, these points are in different disjoint sets. "Join" them by
		 * drawing them, and merging the two sets: */
		j+=1;
		for(k=0; k< ToastParams::TreeSize; k++)
			if(dsets[k] == bset) 
				dsets[k] = aset;
		draw_line(lvl, points[pairs[i].a], points[pairs[i].b], LevelPixel::LevelGenDirt, 0);
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
	if (lvl->GetVoxelRaw(( x - 1 + lvl->GetSize().x * (y - 1) )) == LevelPixel::LevelGenDirt) return 1;
	if (lvl->GetVoxelRaw(( x     + lvl->GetSize().x * (y - 1) )) == LevelPixel::LevelGenDirt) return 1;
	if (lvl->GetVoxelRaw(( x + 1 + lvl->GetSize().x * (y - 1) )) == LevelPixel::LevelGenDirt) return 1;
	if (lvl->GetVoxelRaw(( x - 1 + lvl->GetSize().x * (y    ) )) == LevelPixel::LevelGenDirt) return 1;
	if (lvl->GetVoxelRaw(( x + 1 + lvl->GetSize().x * (y    ) )) == LevelPixel::LevelGenDirt) return 1;
	if (lvl->GetVoxelRaw(( x - 1 + lvl->GetSize().x * (y + 1) )) == LevelPixel::LevelGenDirt) return 1;
	if (lvl->GetVoxelRaw(( x     + lvl->GetSize().x * (y + 1) )) == LevelPixel::LevelGenDirt) return 1;
	if (lvl->GetVoxelRaw(( x + 1 + lvl->GetSize().x * (y + 1) )) == LevelPixel::LevelGenDirt) return 1;
	return 0;
}


static void set_outside(Level *lvl, LevelPixel val) {
	int i;
	Size size = lvl->GetSize();
	
	for (i = 0; i < size.x;   i++) lvl->SetVoxelRaw({ i, 0 }, val);
	for (i = 0; i < size.x;   i++) lvl->SetVoxelRaw({ i, size.y - 1 }, val);
	for (i = 1; i < size.y-1; i++) lvl->SetVoxelRaw({ 0, i }, val);
	for (i = 1; i < size.y-1; i++) lvl->SetVoxelRaw({ size.x - 1, i }, val);
}

static void expand_init(Level *lvl, PositionQueue& q) {
	auto perf = MeasureFunction<3>{ __FUNCTION__ };
	for(int y = 1; y<lvl->GetSize().y-1; y++)
		for (int x = 1; x < lvl->GetSize().x - 1; x++) {
			int offset = x + y * lvl->GetSize().x;
			if (lvl->GetVoxelRaw(offset) != LevelPixel::LevelGenDirt  && has_neighbor(lvl, x, y)) {
				lvl->SetVoxelRaw(offset, LevelPixel::LevelGenMark);
				q.push({ x, y });
			}
		}
}


struct ExpandResult {
	int dirt_generated = 0;
	int rocks_marked = 0;
};
	

ExpandResult expand_once(Level *lvl, circular_buffer_adaptor<Position>& q, RandomGenerator random) {

	//for (int i = 0; i < q.size() * 1000; ++i) {
	//	if (i % 10000 == 1)
	//		++count;
	//}
	//return count;
	
	ExpandResult result = {};
	size_t total = q.size();
	for(size_t i=0; i<total; i++) {
		int xodds, yodds, odds;
		
		Position temp;
		q.pop(temp);

		/* Odds based on proximity to edge of level */
		xodds = ToastParams::MaxDirtSpawnOdds * std::min(lvl->GetSize().x - temp.x, temp.x) / ToastParams::DirtSpawnProgression;
		yodds = ToastParams::MaxDirtSpawnOdds * std::min(lvl->GetSize().y - temp.y, temp.y) / ToastParams::DirtSpawnProgression;
		odds  = std::min(std::min(xodds, yodds), ToastParams::MaxDirtSpawnOdds);
		
		if(random.Bool(odds)) {
			lvl->SetVoxelRaw(temp, LevelPixel::LevelGenDirt);
			++result.dirt_generated;
			
			/* Now, queue up any neighbors that qualify: */
			for(int j=0; j<9; j++) {
				if(j==4) continue;
				
				int tx = temp.x + (j % 3) - 1;
				int ty = temp.y + (j / 3) - 1;
				LevelPixel v = lvl->GetVoxelRaw({ tx, ty });
				if(v == LevelPixel::LevelGenRock) {
				   // v  = LevelPixel::LevelGenMark; // this is never read
				   ++result.rocks_marked;
					q.push({ tx, ty });
				}
			}
		} else
			q.push(temp);
	}
	return result;
}

static void expand_process(Level* lvl, PositionQueue& q) {
	auto measure_function = MeasureFunction<3>{ __FUNCTION__ };
	/* Want to generate at least goal_generated */
	const int goal_generated = ToastParams::TargetDirtAmount(lvl);
	std::atomic<int> items_generated_global = 0;

	/* Prepare worker pool */
	const int worker_count = tweak::perf::parallelism_degree;
	auto workerQueues = std::vector<circular_buffer_adaptor<Position>>(); 	/* TODO: circular buffer can overflow */
	for (int i = 0; i < worker_count; ++i) {
		workerQueues.emplace_back(50000 / worker_count /* Queue constructor */ );
	}

	/* TODO: Split per position quadrants */
	/* Split into one queue per worker. This is relatively cheap. */
	int worker = 0;
	auto measure_queue_expand = MeasureFunction<4>{ "expand_process: prepare per-worker queues" };
	
	while (q.size()) {
		Position pos;
		q.pop(pos);
		workerQueues[worker].push(pos);
		worker = (worker + 1) % worker_count;
	}
	measure_queue_expand.Finish();

	/* Set up threading controls */
	std::mutex mutex_threads_waiting;
	std::condition_variable cv_threads_waiting;
	int threads_waiting = 0;
	std::condition_variable cv_continue_thread;
	std::atomic<bool> done = false;
	int reached_pass = -1; /* Current maximum iteration threads have reached */
	int max_pass = 0; /* Current maximum iteration threads should process */

	/* Performance statistics */
	std::atomic<int> waits_main = 0;
	std::atomic<int> threads_notify = 0;
	std::atomic<int> waits_continue = 0;
	std::atomic<std::int64_t> expand_pure_time_ms = 0;

	auto expand_loop = [&](circular_buffer_adaptor<Position>* qq, RandomGenerator random) {

		Stopwatch perf_thread_time;
		Stopwatch perf_wait_time;

		int curr_pass = 0;
		int dirt_generated = 0;
		int rocks_marked = 0;
		bool no_more_work = false;
		while (!done && !no_more_work) {
			{	/* Signal thread is entering work state */
				std::unique_lock lock(mutex_threads_waiting);
				--threads_waiting;
				reached_pass = std::max(reached_pass, curr_pass);
				cv_threads_waiting.notify_all();
				threads_notify.fetch_add(1, std::memory_order_relaxed);
			}

			perf_wait_time.Stop();

			/* Call the function doing actual work */
			Stopwatch expand_time;
			ExpandResult result = expand_once(lvl, *qq, random);
			if (!result.dirt_generated) {
				//no_more_work = true;  /* Do we want to quit early? */
			}
			dirt_generated += result.dirt_generated;
			rocks_marked += result.rocks_marked;
			items_generated_global.fetch_add(result.dirt_generated, std::memory_order_relaxed);
			expand_pure_time_ms.fetch_add(expand_time.GetElapsed().count(), std::memory_order_relaxed);

			perf_wait_time.Start();

			/* Early exit if enough results were generates */
			if (items_generated_global >= goal_generated) {
				done = true;
				cv_threads_waiting.notify_all();
				threads_notify.fetch_add(1, std::memory_order_relaxed);
			}

			{	/* Signal thread is leaving work state */
				std::unique_lock lock(mutex_threads_waiting);
				++threads_waiting;
				cv_threads_waiting.notify_all();
				threads_notify.fetch_add(1, std::memory_order_relaxed);
				while (curr_pass >= max_pass) {
					/* Wait if not allowed to do next pass yet */
					cv_continue_thread.wait(lock);
					waits_continue.fetch_add(1, std::memory_order_relaxed);
				}
			}
			++curr_pass;
		}
		auto elapsed = perf_thread_time.GetElapsed();
		auto waited = perf_wait_time.GetElapsed();

		DebugTrace<5>("thread took: %lld.%03lldms (%lld.%03lldms wait time) to add %d dirt and mark %d voxels \r\n",
			elapsed.count() / 1000, elapsed.count() % 1000, waited.count() / 1000, waited.count() % 1000, dirt_generated, rocks_marked);
	};

	Stopwatch time_thread_create;

	/* Launch workers on their own queues */
	threads_waiting = worker_count;
	auto workers = std::vector<std::future<void>>();
	for (int i = 0; i < worker_count; ++i) {
		workers.push_back(std::async(std::launch::async, expand_loop, &workerQueues[i], Random));
	}

	time_thread_create.Stop();
	Stopwatch time_workers;

	int prev_items_generated_global = 0;
	while (!done && items_generated_global < goal_generated) {
		{
			/* Wait for all threads being done and waiting for next pass */
			std::unique_lock lock(mutex_threads_waiting);
			while (!done && 
					(threads_waiting != worker_count || /* Already started new passes */
					(max_pass != reached_pass))) { /* All are still waiting for start */
				waits_main.fetch_add(1, std::memory_order_relaxed);
				cv_threads_waiting.wait(lock);
			}
			/* Signal exit if we got enough */
			if (items_generated_global >= goal_generated || prev_items_generated_global == items_generated_global) {
				done = true;
			}
			/* Advance pass boundary, resume thread */
			max_pass = reached_pass + 100; /* TODO: desynchronized passes */
			cv_continue_thread.notify_all();
            prev_items_generated_global = items_generated_global;
		}
	}
	time_workers.Stop();
	Stopwatch time_join;
	/* Wait for threads to end cleanly */
	for (int i = 0; i < worker_count; ++i) {
		workers[i].get();
	}
	time_join.Stop();
	measure_function.Finish();

	if (prev_items_generated_global < goal_generated)
        gamelib_print("Did generate only %d items out of %d", prev_items_generated_global,
                      items_generated_global.load());

	/* Emit diag info */
	DebugTrace<4>("  expand_process details: %lld.%03lld ms thread create, %lld.%03lld ms thread run, %lld.%03lld ms thread join, %d.%03d ms worker time (%u us per thread) \n",
		time_thread_create.GetElapsed().count() / 1000, time_thread_create.GetElapsed().count() % 1000,
		time_workers.GetElapsed().count() / 1000, time_workers.GetElapsed().count() % 1000,
		time_join.GetElapsed().count() / 1000, time_join.GetElapsed().count() % 1000,
		expand_pure_time_ms.load() / 1000, expand_pure_time_ms.load() % 1000,
		expand_pure_time_ms.load() / worker_count);
	DebugTrace<4>("waits_continue: %d waits_main: %d threads_notify: %d \r\n", waits_continue.load(), waits_main.load(), threads_notify.load());
}

static void expand_cleanup(Level *lvl) {
	auto perf = MeasureFunction<3>{ __FUNCTION__ };
	unmark_all(lvl);
}

static void randomly_expand(Level *lvl) {
	auto perf = MeasureFunction<2>{ __FUNCTION__ };
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
	
static int smooth_once(Level *lvl) {

	/* Smooth surfaces. Require at least 3 neighbors to keep alive. Spawn new at 5 neighbors. */
	auto smooth_step = [lvl](int from_y, int until_y, ThreadLocal*) {
		Stopwatch time_step;
		int count = 0;
		Size size = lvl->GetSize();
		for (int y = from_y; y <= until_y; y++)
			for (int x = 1; x < size.x - 1; x++) {
                LevelPixel oldbit = lvl->GetVoxelRaw({ x, y });

				int n = Queries::CountNeighborValues({x, y}, lvl);
				bool paintRock = (oldbit != LevelPixel::LevelGenDirt) ? (n >= 3) : (n > 4);
				lvl->SetVoxelRaw({ x, y }, paintRock ? LevelPixel::LevelGenRock : LevelPixel::LevelGenDirt);

				count += lvl->GetVoxelRaw({ x, y }) != oldbit;
			}

		time_step.Stop();
		DebugTrace<5>("    smooth_step thread took %lld.%03lld ms \n",
			time_step.GetElapsed().count() / 1000, time_step.GetElapsed().count() % 1000);
		return count;
	};

	Stopwatch time_whole;
	int count = parallel_for(smooth_step, 1, lvl->GetSize().y - 2, WorkerDivisor{4});
	
	time_whole.Stop();
	DebugTrace<4>("  smooth_once total took %lld.%03lld ms \n",
		time_whole.GetElapsed().count() / 1000, time_whole.GetElapsed().count() % 1000);

	return count;
}

static void smooth_cavern(Level *lvl) {
	auto perf = MeasureFunction<2>{ __FUNCTION__ };

	set_outside(lvl, LevelPixel::LevelGenDirt);
	auto steps_remain = ToastParams::SmoothingSteps;
	while(smooth_once(lvl) && --steps_remain != 0);
	set_outside(lvl, LevelPixel::LevelGenRock);
}


/*----------------------------------------------------------------------------*
 * MAIN FUNCTIONS:                                                            *
 *----------------------------------------------------------------------------*/

std::unique_ptr<Level> ToastLevelGenerator::Generate(Size size)
{
    std::unique_ptr<Level> level = std::make_unique<Level>(size);
    Level * lvl = level.get();

	auto perf = MeasureFunction<1>{ __FUNCTION__ };

	generate_tree(lvl);
	randomly_expand(lvl);
	smooth_cavern(lvl);

	return level;
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
