using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct GizmoSystem : ISystem
{
    private BoidSettings _boidSettings;
    private EntityQuery _boidQuery;
    private EntityQuery _landingAreaQuery;
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

        _boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform, Velocity, ObstacleAvoidance>()
            .Build(ref state);
        _landingAreaQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LandingArea, LocalTransform>()
            .Build(ref state);
        _landerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Lander, LocalTransform>()
            .Build(ref state);
        _turretQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalToWorld, TurretComponent>()
            .Build(ref state);
        _turretHeadQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalToWorld, TurretHeadComponent>()
            .Build(ref state);
        _turretCannonQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalToWorld, TurretCannonComponent>()
            .Build(ref state);
        _cannonQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalToWorld, CannonComponent>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _boidSettings = SystemAPI.GetSingleton<BoidSettings>();
    }

    public void DrawBoidGizmos()
    {
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

        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var transform = transforms[entityIndex];
            var turret = turrets[entityIndex];

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.Position, turret.targetingDirection);
        }

        entities.Dispose();
        transforms.Dispose();
        turrets.Dispose();
    }

    public void DrawTurretHeadGizmos()
    {
        var entities = _turretHeadQuery.ToEntityArray(Allocator.TempJob);
        var transforms = _turretHeadQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
        var turretHeads = _turretHeadQuery.ToComponentDataArray<TurretHeadComponent>(Allocator.TempJob);

        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var transform = transforms[entityIndex];
            var turret = turretHeads[entityIndex];

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.Position, turret.targetingDirection);
        }


        entities.Dispose();
        transforms.Dispose();
        turretHeads.Dispose();
    }

    public void DrawTurretCannonGizmos()
    {
        var entities = _turretCannonQuery.ToEntityArray(Allocator.TempJob);
        var transforms = _turretCannonQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
        var turretCannons = _turretCannonQuery.ToComponentDataArray<TurretCannonComponent>(Allocator.TempJob);

        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var transform = transforms[entityIndex];
            var turret = turretCannons[entityIndex];

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.Position, turret.targetingDirection);
        }

        entities.Dispose();
        transforms.Dispose();
        turretCannons.Dispose();
    }

    public void DrawCannonGizmos()
    {
        var entities = _cannonQuery.ToEntityArray(Allocator.TempJob);
        var transforms = _cannonQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
        var cannons = _cannonQuery.ToComponentDataArray<CannonComponent>(Allocator.TempJob);

        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var transform = transforms[entityIndex];
            var turret = cannons[entityIndex];

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.Position, turret.targetingDirection);
        }

        entities.Dispose();
        transforms.Dispose();
        cannons.Dispose();
    }
}