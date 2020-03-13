#pragma once
#include <level.h>

namespace levelgen::toast {

	struct ToastParams 
	{
		constexpr static int BorderWidth = 30;
		constexpr static int MaxDirtSpawnOdds = 300; /* Maximum chance (out of 1000) to spawn a dirt tile */
		constexpr static int DirtSpawnProgression = 70;  /* How much more likely it is to spawn at center compared to edges (formula = distance * MaxDirtSpawnOdds / MaxDirtSpawnOdds)    */
		constexpr static int DirtTargetPercent = 65; /* Target percentage of dirt in level */
		constexpr static int TreeSize = 150;
		constexpr static int SmoothingSteps = 2; /* How many iterations of smoothing edges. -1 = smooth completely.  */

		static int TargetDirtAmount(Level* lvl) { return lvl->GetSize().x * lvl->GetSize().y * DirtTargetPercent / 100; };
	};
	void toast_generator(Level* lvl);

}



