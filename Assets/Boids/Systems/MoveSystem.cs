using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct MoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var moveJob = new MoveJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };
        state.Dependency = moveJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    public float DeltaTime;

    // 'ref' allows us to write to the LocalTransform
    // 'in' means we only read from Velocity (a small optimization)
    public void Execute(ref LocalTransform transform, ref BoidTag boidTag, in Velocity velocity)
    {
        transform.Position += velocity.Value * DeltaTime;
        //transform.Rotation = Quaternion.LookRotation(velocity.Value);
        
        // Don't try to rotate if the boid is not moving
        if (math.lengthsq(velocity.Value) < 0.001f)
        {
            return;
        }

        // 1. Calculate the target rotation based on the velocity vector
        quaternion targetRotation = quaternion.LookRotationSafe(velocity.Value, math.up());
        
        //Todo: Precalculate and move out of job
        //quaternion modelCorrection = quaternion.Euler(math.radians(-90), 0, math.radians(-90));
        
        //targetRotation = math.mul(targetRotation, modelCorrection);
        
        // 2. Smoothly interpolate from the current rotation to the target rotation
        // math.slerp is used for spherical interpolation, which is correct for rotations.
        transform.Rotation = math.slerp(transform.Rotation, targetRotation, 5.0f * DeltaTime);
    }
}