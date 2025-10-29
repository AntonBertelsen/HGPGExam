using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct BulletMoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var moveJob = new BulletMoveJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };
        state.Dependency = moveJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct BulletMoveJob : IJobEntity
{
    public float DeltaTime;

    // 'ref' allows us to write to the LocalTransform
    // 'in' means we only read from Velocity (a small optimization)
    public void Execute(ref LocalTransform transform, in BulletVelocity velocity)
    {
        transform.Position += velocity.Value * DeltaTime;
        transform.Rotation = Quaternion.LookRotation(velocity.Value);
    }
}