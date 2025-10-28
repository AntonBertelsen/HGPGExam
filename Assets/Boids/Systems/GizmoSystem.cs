using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial struct GizmoSystem : ISystem
{
    private BoidSettings _boidSettings;
    private EntityQuery _boidQuery;
    private EntityQuery _turretQuery;
    private EntityQuery _turretHeadQuery;
    private EntityQuery _turretCannonQuery;
    private EntityQuery _cannonQuery;
    

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSettings>();

        _boidQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BoidTag, LocalTransform, Velocity, ObstacleAvoidance>()
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

    public void DrawGizmos()
    {
        DrawBoidGizmos();
        DrawTurretGizmos();
        DrawTurretHeadGizmos();
        DrawTurretCannonGizmos();
        DrawCannonGizmos();
    }

    private void DrawBoidGizmos()
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

    private void DrawTurretGizmos()
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
    
    private void DrawTurretHeadGizmos()
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
    
    private void DrawTurretCannonGizmos()
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
    private void DrawCannonGizmos()
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