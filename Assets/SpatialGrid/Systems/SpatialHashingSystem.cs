using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public struct SpatialGridData : IComponentData
{
    public NativeParallelMultiHashMap<int, int> CellMap;
    public SpatialHashGrid3D Grid;
    public int BoidCount;
}

[BurstCompile]
[UpdateBefore(typeof(BoidSystem))]
public partial struct SpatialHashingSystem : ISystem
{
    private EntityQuery boidQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSettings>();
        // 1. Require the boundary component so we can align the grid to it
        state.RequireForUpdate<BoundaryComponent>();
        
        boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform>()
            .Build(ref state);
        state.EntityManager.AddComponentData(state.SystemHandle, new SpatialGridData());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.TryGetSingletonRW<SpatialGridData>(out var gridData))
        {
            if (gridData.ValueRW.CellMap.IsCreated)
                gridData.ValueRW.CellMap.Dispose();
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BoidSettings>();
        // 2. Fetch the boundary
        var boundary = SystemAPI.GetSingleton<BoundaryComponent>();
        
        var boidCount = boidQuery.CalculateEntityCount();

        if (boidCount == 0)
            return;

        var transforms = boidQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var positions = new NativeArray<float3>(boidCount, Allocator.TempJob);
        for (int i = 0; i < boidCount; i++)
            positions[i] = transforms[i].Position;

        // 3. Define Grid Parameters based on BOUNDARY, not Config
        float cellSize = config.ViewRadius;
        
        // Calculate the bottom-left corner of the boundary box
        float3 boundsMin = boundary.Center - (boundary.Size * 0.5f);
        
        // Calculate dimensions based on the boundary size
        int3 gridDim = new int3(
            (int)math.ceil(boundary.Size.x / cellSize),
            (int)math.ceil(boundary.Size.y / cellSize),
            (int)math.ceil(boundary.Size.z / cellSize)
        );
        
        // Add a padding buffer to the grid dimension to prevent edge-case "out of bounds" errors 
        // if a boid slightly overshoots the boundary before being steered back.
        gridDim += new int3(2, 2, 2);
        boundsMin -= new float3(cellSize, cellSize, cellSize);

        var gridStruct = new SpatialHashGrid3D
        {
            CellSize = cellSize,
            GridDim = gridDim,
            Origin = boundsMin
        };

        var gridData = SystemAPI.GetSingletonRW<SpatialGridData>();
        ref var cellMap = ref gridData.ValueRW.CellMap;

        if (cellMap.IsCreated && cellMap.Capacity < boidCount)
        {
            cellMap.Dispose();
        }
        
        if (!cellMap.IsCreated)
        {
            cellMap = new NativeParallelMultiHashMap<int, int>(boidCount * 2, Allocator.Persistent); //allocate with double capacity so we have room to grow
        }
        else
        {
            // The map is big enough, just clear it for reuse
            cellMap.Clear();
        }

        var writer = cellMap.AsParallelWriter();

        var buildJob = new BuildGridJob
        {
            Positions = positions,
            GridWriter = writer,
            Grid = gridStruct
        };

        var handle = buildJob.Schedule(boidCount, 64, state.Dependency);
        handle.Complete();

        gridData.ValueRW.Grid = gridStruct;
        gridData.ValueRW.BoidCount = boidCount;

        positions.Dispose();
        transforms.Dispose();
    }

    [BurstCompile]
    private struct BuildGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter GridWriter;
        [ReadOnly] public SpatialHashGrid3D Grid;

        public void Execute(int i)
        {
            int3 cell = Grid.GetCellCoords(Positions[i]);
            int key = Grid.GetCellIndex(cell);
            GridWriter.Add(key, i);
        }
    }
}

[BurstCompile]
public struct SpatialHashGrid3D
{
    public float CellSize;
    public int3 GridDim;
    public float3 Origin;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int3 GetCellCoords(float3 pos)
    {
        float3 rel = pos - Origin;
        return new int3(
            (int)math.floor(rel.x / CellSize),
            (int)math.floor(rel.y / CellSize),
            (int)math.floor(rel.z / CellSize)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCellIndex(int3 c)
    {
        c = math.clamp(c, int3.zero, GridDim - 1);
        return c.x + c.y * GridDim.x + c.z * GridDim.x * GridDim.y;
    }
}

public static class SpatialQuery
{
    public static int FindNearest(
        float3 position, int selfIndex,
        in SpatialHashGrid3D grid,
        in NativeParallelMultiHashMap<int,int> cellMap,
        in NativeArray<float3> positions)
    {
        int3 center = grid.GetCellCoords(position);
        int3 dim = grid.GridDim;
        float bestDistSq = float.MaxValue;
        int bestIndex = -1;

        // Optimized search radius to just check immediate neighbors (3x3x3)
        // Checking the whole grid size is extremely expensive and defeats the purpose of the grid.
        int radius = 1; 

        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        for (int z = -radius; z <= radius; z++)
        {
            int3 cell = center + new int3(x,y,z);
            if (cell.x < 0 || cell.y < 0 || cell.z < 0 ||
                cell.x >= dim.x || cell.y >= dim.y || cell.z >= dim.z)
                continue;

            int key = grid.GetCellIndex(cell);
            if (cellMap.TryGetFirstValue(key, out int other, out var it))
            {
                do
                {
                    if (other == selfIndex) continue;
                    float distSq = math.distancesq(position, positions[other]);
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestIndex = other;
                    }
                } while (cellMap.TryGetNextValue(out other, ref it));
            }
        }

        return bestIndex;
    }
}