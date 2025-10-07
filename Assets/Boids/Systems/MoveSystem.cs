using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

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
        moveJob.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    public float DeltaTime;

    // 'ref' allows us to write to the LocalTransform
    // 'in' means we only read from Velocity (a small optimization)
    public void Execute(ref LocalTransform transform, in Velocity velocity)
    {
        transform.Position += velocity.Value * DeltaTime;
    }
}