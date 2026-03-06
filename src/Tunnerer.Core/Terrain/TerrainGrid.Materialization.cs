namespace Tunnerer.Core.Terrain;

public partial class TerrainGrid
{
    public void MaterializeTerrain(int? seed = null, bool parallel = false)
    {
        if (parallel)
        {
            MaterializeTerrainParallel(seed);
            return;
        }

        var rng = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        for (int i = 0; i < _data.Length; i++)
            _data[i] = MaterializePixel(_data[i], rng);
    }

    private void MaterializeTerrainParallel(int? seed)
    {
        const int chunkSize = 4096;
        int length = _data.Length;
        int chunks = (length + chunkSize - 1) / chunkSize;
        int baseSeed = seed ?? Environment.TickCount;

        Parallel.For(0, chunks, chunk =>
        {
            var rng = new Random(baseSeed + chunk);
            int from = chunk * chunkSize;
            int to = Math.Min(from + chunkSize, length);
            for (int i = from; i < to; i++)
                _data[i] = MaterializePixel(_data[i], rng);
        });
    }

    private static TerrainPixel MaterializePixel(TerrainPixel p, Random rng) => p switch
    {
        TerrainPixel.LevelGenDirt => TerrainPixel.DirtHigh,
        TerrainPixel.LevelGenRock => TerrainPixel.Rock,
        TerrainPixel.LevelGenMark => TerrainPixel.Rock,
        _ => p,
    };
}
