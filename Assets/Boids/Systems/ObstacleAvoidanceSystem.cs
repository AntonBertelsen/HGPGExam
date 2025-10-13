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
        state.RequireForUpdate<PhysicsWorldSingleton>();
        _directions = BoidHelper.Directions;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var job = new ObstacleAvoidanceJob
        {
            physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>(),
            directions = _directions,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct ObstacleAvoidanceJob : IJobEntity
{
    [ReadOnly] public PhysicsWorldSingleton physicsWorld;
    [ReadOnly] public NativeArray<float3> directions;
    
    private void Execute(in LocalTransform localTransform, ref ObstacleAvoidance obstacleAvoidance)
    {
        for (var i = 0; i < BoidHelper.NumViewDirections; i++)
        {
            var raycastInput = new RaycastInput
            {
                Start = localTransform.Position,
                End = BoidHelperMath.RelativeDirection(localTransform.Rotation, directions[i]) + localTransform.Position,
                Filter = CollisionFilter.Default,
            };

            if (physicsWorld.CastRay(raycastInput)) continue;
            
            // We found a direction with no obstacle
            obstacleAvoidance.DirectionIndex = i;
            return;
        }
    }
}