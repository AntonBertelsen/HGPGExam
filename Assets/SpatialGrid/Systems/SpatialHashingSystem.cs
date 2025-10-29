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
        var boidCount = boidQuery.CalculateEntityCount();

        if (boidCount == 0)
            return;

        // Extract transforms
        var transforms = boidQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var positions = new NativeArray<float3>(boidCount, Allocator.TempJob);
        for (int i = 0; i < boidCount; i++)
            positions[i] = transforms[i].Position;

        // Define grid parameters
        float cellSize = config.ViewRadius;
        float3 origin = new float3(-config.BoundaryBounds);
        int dim = (int)math.ceil((config.BoundaryBounds * 2f) / cellSize);
        var gridStruct = new SpatialHashGrid3D
        {
            CellSize = cellSize,
            GridDim = new int3(dim, dim, dim),
            Origin = origin
        };

        // Prepare persistent grid map
        var gridData = SystemAPI.GetSingletonRW<SpatialGridData>();
        ref var cellMap = ref gridData.ValueRW.CellMap;

        if (!cellMap.IsCreated)
            cellMap = new NativeParallelMultiHashMap<int, int>(boidCount, Allocator.Persistent);
        else
            cellMap.Clear();

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