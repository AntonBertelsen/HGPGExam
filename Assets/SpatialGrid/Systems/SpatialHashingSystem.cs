using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


public struct BoidData
{
    public Entity Entity;
    public float3 Position;
    public float3 Velocity;
}

public struct SpatialGridData : IComponentData
{
    public NativeParallelMultiHashMap<int, BoidData> CellMap;
    
    // Cell Index -> Boid Count
    public NativeParallelHashMap<int, int> ClusterMap;
    
    public NativeParallelHashMap<int, bool> ActiveCellsMap; // Used for deduplication during ActiveCellsList duplication
    public NativeList<int> ActiveCellsList;
    
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
        state.RequireForUpdate<BoundaryComponent>();
        
        boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform, Velocity>()
            .Build(ref state);
        
        state.EntityManager.AddComponentData(state.SystemHandle, new SpatialGridData 
        {
            CellMap = new NativeParallelMultiHashMap<int, BoidData>(0, Allocator.Persistent),
            ClusterMap = new NativeParallelHashMap<int, int>(0, Allocator.Persistent),
            ActiveCellsMap = new NativeParallelHashMap<int, bool>(0, Allocator.Persistent),
            ActiveCellsList = new NativeList<int>(0, Allocator.Persistent)
        }); // We initialize with persistent empty containers so we dont have to allocate temp buffers every frame which was causing stalls on main thread adn causing all workers to sit idle
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.TryGetSingletonRW<SpatialGridData>(out var gridData))
        {
            if (gridData.ValueRW.CellMap.IsCreated)
                gridData.ValueRW.CellMap.Dispose();
            if (gridData.ValueRW.ClusterMap.IsCreated) 
                gridData.ValueRW.ClusterMap.Dispose();
            if (gridData.ValueRW.ActiveCellsMap.IsCreated) 
                gridData.ValueRW.ActiveCellsMap.Dispose();
            if (gridData.ValueRW.ActiveCellsList.IsCreated) 
                gridData.ValueRW.ActiveCellsList.Dispose();
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BoidSettings>();
        var boundary = SystemAPI.GetSingleton<BoundaryComponent>();
        
        var boidCount = boidQuery.CalculateEntityCount();

        if (boidCount == 0)
            return;
        
        float cellSize = config.ViewRadius;

        float3 boundsMin = boundary.Center - (boundary.Size * 0.5f);
        
        // Calculate dimensions based on the boundary size
        int3 gridDim = new int3(
            (int)math.ceil(boundary.Size.x / cellSize),
            (int)math.ceil(boundary.Size.y / cellSize),
            (int)math.ceil(boundary.Size.z / cellSize)
        );
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
            cellMap = new NativeParallelMultiHashMap<int, BoidData>(boidCount * 2, Allocator.Persistent); //allocate with double capacity so we have room to grow
        }
        else
        {
            // The map is big enough, just clear it for reuse
            cellMap.Clear();
        }
        
        ref var clusterMap = ref gridData.ValueRW.ClusterMap;
        
        if (clusterMap.IsCreated && clusterMap.Capacity < boidCount)
        {
            clusterMap.Dispose();
        }
        
        if (!clusterMap.IsCreated)
        {
            clusterMap = new NativeParallelHashMap<int, int>(boidCount, Allocator.Persistent);
        }
        else
        {
            // The map is big enough, just clear it for reuse
            clusterMap.Clear();
        }
        
        ref var activeCellsMap = ref gridData.ValueRW.ActiveCellsMap;
        
        if (activeCellsMap.IsCreated && activeCellsMap.Capacity < boidCount)
        {
            activeCellsMap.Dispose();
        }
        
        if (!activeCellsMap.IsCreated)
        {
            activeCellsMap = new NativeParallelHashMap<int, bool>(boidCount, Allocator.Persistent);
        }
        else
        {
            // The map is big enough, just clear it for reuse
            activeCellsMap.Clear();
        }
        
        
        if (gridData.ValueRW.ActiveCellsList.Capacity < boidCount) 
            gridData.ValueRW.ActiveCellsList.Capacity = boidCount;
        gridData.ValueRW.ActiveCellsList.Clear();

        var buildJob = new BuildGridJob
        {
            GridWriter = gridData.ValueRW.CellMap.AsParallelWriter(),
            ActiveCellsMapWriter = gridData.ValueRW.ActiveCellsMap.AsParallelWriter(),
            ActiveCellsListWriter = gridData.ValueRW.ActiveCellsList.AsParallelWriter(),
            Grid = gridStruct
        };
        
        var gridHandle = buildJob.ScheduleParallel(boidQuery, state.Dependency);

        var clusterJob = new BuildClusterJob
        {
            Keys = gridData.ValueRW.ActiveCellsList.AsDeferredJobArray(), // "Deferred" means "Wait for the previous job to fill this"
            CellMap = gridData.ValueRW.CellMap,
            ClusterMapWriter = gridData.ValueRW.ClusterMap.AsParallelWriter()
        };
        
        state.Dependency = clusterJob.Schedule(gridData.ValueRW.ActiveCellsList, 64, gridHandle);;

        gridData.ValueRW.Grid = gridStruct;
        gridData.ValueRW.BoidCount = boidCount;
    }

    [BurstCompile]
    private partial struct BuildGridJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int, BoidData>.ParallelWriter GridWriter;
        
        public NativeParallelHashMap<int, bool>.ParallelWriter ActiveCellsMapWriter;
        public NativeList<int>.ParallelWriter ActiveCellsListWriter;
        
        [ReadOnly] public SpatialHashGrid3D Grid;

        private void Execute(Entity entity, in LocalTransform transform, in Velocity velocity)
        {
            int3 cell = Grid.GetCellCoords(transform.Position);
            int key = Grid.GetCellIndex(cell);

            // Store boid data with position and velocity which will allow the boid system to access this information very quickly later
            GridWriter.Add(key, new BoidData
            {
                Entity = entity,
                Position = transform.Position,
                Velocity = velocity.Value
            });
            
            if (ActiveCellsMapWriter.TryAdd(key, true))
            {
                // We are the first thread to claim this cell, so we add it to the list.
                ActiveCellsListWriter.AddNoResize(key);
            }
        }
    }

    
    // This is used to count the number of boids per cell. We do this once while building the spatial data structure in order to significantly speed up turret targeting later.
    [BurstCompile]
    private struct BuildClusterJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int> Keys;
        [ReadOnly] public NativeParallelMultiHashMap<int, BoidData> CellMap;
        public NativeParallelHashMap<int, int>.ParallelWriter ClusterMapWriter;

        public void Execute(int i)
        {
            int key = Keys[i];
            int count = 0;

            if (CellMap.TryGetFirstValue(key, out BoidData _, out var it))
            {
                do
                {
                    count++;
                } while (CellMap.TryGetNextValue(out _, ref it));
            }

            ClusterMapWriter.TryAdd(key, count);
        }
    }
    
    private void CheckAndResize<TKey, TValue>(ref NativeParallelMultiHashMap<TKey, TValue> map, int capacity) 
        where TKey : unmanaged, System.IEquatable<TKey> where TValue : unmanaged
    {
        if (!map.IsCreated || map.Capacity < capacity)
        {
            if (map.IsCreated) map.Dispose();
            map = new NativeParallelMultiHashMap<TKey, TValue>(capacity, Allocator.Persistent);
        }
        else map.Clear();
    }
    
    private void CheckAndResize<TKey, TValue>(ref NativeParallelHashMap<TKey, TValue> map, int capacity) 
        where TKey : unmanaged, System.IEquatable<TKey> where TValue : unmanaged
    {
        if (!map.IsCreated || map.Capacity < capacity)
        {
            if (map.IsCreated) map.Dispose();
            map = new NativeParallelHashMap<TKey, TValue>(capacity, Allocator.Persistent);
        }
        else map.Clear();
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int3 GetCoordsFromIndex(int index)
    {
        int dimXY = GridDim.x * GridDim.y;

        int z = index / dimXY;
        int rem = index % dimXY;
        int y = rem / GridDim.x;
        int x = rem % GridDim.x;
        return new int3(x, y, z);
    }
}