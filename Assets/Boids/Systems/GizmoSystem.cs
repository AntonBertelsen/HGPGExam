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
    private EntityQuery _landingAreaQuery;
    private EntityQuery _landerQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
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
        var entities = _landingAreaQuery.ToEntityArray(Allocator.TempJob);
        var landingAreas = _landingAreaQuery.ToComponentDataArray<LandingArea>(Allocator.TempJob);

        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var landingArea = landingAreas[entityIndex];
            var blob = landingArea.SurfaceBlob;

            if (!blob.IsCreated) continue;

            ref var positions = ref blob.Value.Positions;

            for (var i = 0; i < positions.Length; i++)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(positions[i], 0.25f);
            }
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
}