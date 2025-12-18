using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(BoidSystem))]
public partial struct MoveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSettings>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<BoidSettings>();
        var moveJob = new MoveJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };
        state.Dependency = config.UseParallel
            ? moveJob.ScheduleParallel(state.Dependency)
            : moveJob.Schedule(state.Dependency);
    }
}

[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    public float DeltaTime;

    // 'ref' allows us to write to the LocalTransform
    // 'in' means we only read from Velocity (a small optimization)
    public void Execute(ref LocalTransform transform, in BoidTag boidTag, in Velocity velocity)
    {
        transform.Position += velocity.Value * DeltaTime;

        // Don't try to rotate if the boid is not moving
        if (math.lengthsq(velocity.Value) < 0.001f)
        {
            return;
        }
        
        quaternion targetRotation = quaternion.LookRotationSafe(velocity.Value, math.up());
        transform.Rotation = math.slerp(transform.Rotation, targetRotation, 5.0f * DeltaTime);
    }
}