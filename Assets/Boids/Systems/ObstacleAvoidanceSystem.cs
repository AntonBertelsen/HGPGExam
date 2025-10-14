using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public partial struct ObstacleAvoidanceSystem : ISystem
{
    private NativeArray<float3> _directions;

    // Can't burst compile because BoidHelper has a static constructor using NativeArray
    // [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSettings>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        _directions = BoidHelper.Directions;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var job = new ObstacleAvoidanceJob
        {
            Config = SystemAPI.GetSingleton<BoidSettings>(),
            PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>(),
            Directions = _directions,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct ObstacleAvoidanceJob : IJobEntity
{
    [ReadOnly] public BoidSettings Config;

    [ReadOnly] public PhysicsWorldSingleton PhysicsWorld;
    [ReadOnly] public NativeArray<float3> Directions;

    private void Execute(in LocalTransform localTransform, ref ObstacleAvoidance obstacleAvoidance)
    {
        for (var i = 0; i < BoidHelper.NumViewDirections; i++)
        {
            var raycastInput = new RaycastInput
            {
                Start = localTransform.Position,
                End = BoidHelperMath.RelativeDirection(localTransform.Rotation, Directions[i]) *
                    Config.AvoidanceRadius + localTransform.Position,
                // TODO: Exclude other boids from collision
                Filter = CollisionFilter.Default,
            };

            if (PhysicsWorld.CastRay(raycastInput)) continue;

            // We found a direction with no obstacle
            obstacleAvoidance.DirectionIndex = i;
            return;
        }
    }
}