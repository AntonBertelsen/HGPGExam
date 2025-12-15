using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // We need this for drawing text labels
#endif

public partial struct GizmoSystem : ISystem
{
    private BoidSettings _boidSettings;
    private EntityQuery _boidQuery;
    private EntityQuery _landerQuery;
    private EntityQuery _turretQuery;
    private EntityQuery _turretHeadQuery;
    private EntityQuery _turretCannonQuery;
    private EntityQuery _cannonQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<KdTree>();
        state.RequireForUpdate<BoidSettings>();
        state.RequireForUpdate<SpatialGridData>();

        _boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform, Velocity, ObstacleAvoidance>()
            .Build(ref state);
        _landerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Lander, LocalTransform>()
            .Build(ref state);
        _turretQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalToWorld, TurretComponent>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _boidSettings = SystemAPI.GetSingleton<BoidSettings>();
    }

    public void DrawBoidGizmos()
    {
        if (_boidSettings.AvoidanceWeight < 0.001f)
            return;
        
        var entities = _boidQuery.ToEntityArray(Allocator.TempJob);
        var transforms = _boidQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var velocities = _boidQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);
        var avoidances = _boidQuery.ToComponentDataArray<ObstacleAvoidance>(Allocator.TempJob);

        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var transform = transforms[entityIndex];
            var velocity = velocities[entityIndex];
            var avoidance = avoidances[entityIndex];

            // Draw colliding rays
            Gizmos.color = Color.red;
            for (var i = 0; i < avoidance.DirectionIndex; i++)
            {
                Gizmos.DrawRay(transform.Position,
                    BoidHelper.RelativeDirection(transform.Rotation, i) * _boidSettings.AvoidanceRadius);
            }

            // Draw the chosen avoidance ray
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.Position,
                BoidHelper.RelativeDirection(transform.Rotation, avoidance.DirectionIndex) *
                _boidSettings.AvoidanceRadius);

            // Draw non-colliding rays
            //Gizmos.color = Color.gray;
            //for (var i = avoidance.DirectionIndex + 1; i < BoidHelper.NumViewDirections; i++)
            //{
            //    Gizmos.DrawRay(transform.Position,
            //        BoidHelper.RelativeDirection(transform.Rotation, i) * _boidSettings.AvoidanceRadius);
            //}

            // Draw velocity
            //Gizmos.color = Color.blue;
            //Gizmos.DrawRay(transform.Position, velocity.Value);
        }

        entities.Dispose();
        transforms.Dispose();
        velocities.Dispose();
        avoidances.Dispose();
    }

    public void DrawLandingAreaGizmos()
    {
        var tree = SystemAPI.GetSingleton<KdTree>();
        if (!tree.Data.IsCreated)
        {
            return;
        }

        for (var i = 0; i < tree.Data.Length; i++)
        {
            var position = tree.Data[i];
            Gizmos.color = tree.IsOccupied(i) ? Color.red : Color.yellow;
            Gizmos.DrawSphere(position, 0.25f);
        }
    }

    public void DrawLanderStateGizmos()
    {
        var entities = _landerQuery.ToEntityArray(Allocator.TempJob);
        var landers = _landerQuery.ToComponentDataArray<Lander>(Allocator.TempJob);
        var transforms = _landerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var lander = landers[entityIndex];
            var transform = transforms[entityIndex];

            Gizmos.color = lander.State switch
            {
                LanderState.Flying => Color.green,
                LanderState.Landing => Color.magenta,
                LanderState.Landed => Color.cyan,
                _ => Color.white
            };

            Gizmos.DrawSphere(transform.Position, 0.3f);
        }
    }

    public void DrawKdTreeGizmos()
    {
        var tree = SystemAPI.GetSingleton<KdTree>();
        if (!tree.Data.IsCreated)
        {
            return;
        }

        // Compute the bounds of all points for visualization
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (var i = 0; i < tree.Data.Length; i++)
        {
            var p = tree.Data[i];
            if (!tree.IsValid(i)) continue;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        DrawKdTreeNode(tree, 0, 0, min, max);
    }

    private void DrawKdTreeNode(KdTree tree, int index, int depth, Vector3 min, Vector3 max)
    {
        if (index >= tree.Data.Length) return;
        var p = tree.Data[index];
        if (!tree.IsValid(index)) return;

        var axis = depth % 3;
        var from = p;
        var to = p;
        from[axis] = min[axis];
        to[axis] = max[axis];

        Gizmos.color = axis switch
        {
            0 => Color.red,
            1 => Color.green,
            _ => Color.blue
        };
        Gizmos.DrawLine(from, to);
        Gizmos.DrawSphere(p, 0.1f);

        var leftMax = max;
        leftMax[axis] = p[axis];
        DrawKdTreeNode(tree, index * 2 + 1, depth + 1, min, leftMax);

        var rightMin = min;
        rightMin[axis] = p[axis];
        DrawKdTreeNode(tree, index * 2 + 2, depth + 1, rightMin, max);
    }

    public void DrawTurretGizmos()
    {
        var entities = _turretQuery.ToEntityArray(Allocator.TempJob);
        var transforms = _turretQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
        var turrets = _turretQuery.ToComponentDataArray<TurretComponent>(Allocator.TempJob);
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var transform = transforms[entityIndex];
            var turret = turrets[entityIndex];

            var turret_UL_Transform = entityManager.GetComponentData<LocalToWorld>(turret.turret_UL);
            var turret_DL_Transform = entityManager.GetComponentData<LocalToWorld>(turret.turret_DL);
            var turret_UR_Transform = entityManager.GetComponentData<LocalToWorld>(turret.turret_UR);
            var turret_DR_Transform = entityManager.GetComponentData<LocalToWorld>(turret.turret_DR);

            var cannon_UL_Transform = entityManager.GetComponentData<LocalToWorld>(turret.cannon_UL);
            var cannon_DL_Transform = entityManager.GetComponentData<LocalToWorld>(turret.cannon_DL);
            var cannon_UR_Transform = entityManager.GetComponentData<LocalToWorld>(turret.cannon_UR);
            var cannon_DR_Transform = entityManager.GetComponentData<LocalToWorld>(turret.cannon_DR);

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.Position, turret.targetingDirection);
            
            Gizmos.color = Color.red;
            Gizmos.DrawRay(turret_UL_Transform.Position, turret.turret_UL_targetingDirection);
            Gizmos.DrawRay(turret_DL_Transform.Position, turret.turret_DL_targetingDirection);
            Gizmos.DrawRay(turret_UR_Transform.Position, turret.turret_UR_targetingDirection);
            Gizmos.DrawRay(turret_DR_Transform.Position, turret.turret_DR_targetingDirection);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(cannon_UL_Transform.Position, turret.cannon_UL_targetingDirection);
            Gizmos.DrawRay(cannon_DL_Transform.Position, turret.cannon_DL_targetingDirection);
            Gizmos.DrawRay(cannon_UR_Transform.Position, turret.cannon_UR_targetingDirection);
            Gizmos.DrawRay(cannon_DR_Transform.Position, turret.cannon_DR_targetingDirection);
        }

        entities.Dispose();
        transforms.Dispose();
        turrets.Dispose();
    }
    
    public void DrawBoidGridGizmos()
    {
        var gridData = SystemAPI.GetSingleton<SpatialGridData>();
        var grid = gridData.Grid;
        var map = gridData.CellMap;

        if (!map.IsCreated)
            return;
            
        // This value controls how many boids it takes for a cell to reach max opacity.
        // Adjust this to suit the density of your simulation.
        const float maxBoidsForMaxOpacity = 10f; 

        var keys = map.GetKeyArray(Allocator.Temp);
        var drawn = new NativeParallelHashSet<int>(keys.Length, Allocator.Temp);

        foreach (var key in keys)
        {
            if (!drawn.Add(key))
                continue;

            // --- Core Logic Change: Get the boid count for this cell ---
            int boidCount = map.CountValuesForKey(key);
            if (boidCount == 0) continue;

            // --- Calculate Opacity ---
            // Normalize the count to a 0-1 range based on our max value.
            float normalizedCount = math.saturate(boidCount / maxBoidsForMaxOpacity);

            // --- Define Colors ---
            Color wireColorBase = new Color(0.0f,0.6f,0.7f);
            Color fillColorBase = new Color(0.0f,0.5f,0.6f);
            Color fillColor = new Color(wireColorBase.r, wireColorBase.g, wireColorBase.b, normalizedCount * 0.2f); // Very transparent
            Color wireColor = new Color(fillColorBase.r, fillColorBase.g, fillColorBase.b, normalizedCount * 0.65f); // Mostly opaque

            // --- Calculate Position and Size (same as before) ---
            GetCellCoordsFromIndex(key, grid, out var cell);
            float3 worldPos = grid.Origin + (new float3(cell) + 0.5f) * grid.CellSize;
            float3 size = new float3(grid.CellSize);

            // --- Draw the Gizmos ---
            // 1. Draw the transparent filled cube
            Gizmos.color = fillColor;
            Gizmos.DrawCube(worldPos, size);

            // 2. Draw the more opaque wireframe cube
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(worldPos, size);
        }

        drawn.Dispose();
        keys.Dispose();
    }
    
    private static void GetCellCoordsFromIndex(int index, SpatialHashGrid3D grid, out int3 cell)
    {
        int x = index % grid.GridDim.x;
        int y = (index / grid.GridDim.x) % grid.GridDim.y;
        int z = index / (grid.GridDim.x * grid.GridDim.y);
        cell = new int3(x, y, z);
    }
}