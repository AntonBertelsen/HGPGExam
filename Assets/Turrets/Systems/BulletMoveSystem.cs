using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateAfter(typeof(TurretSystem))]
public partial struct BulletMoveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSettings>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var moveJob = new BulletMoveJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };
        var config = SystemAPI.GetSingleton<BoidSettings>();
        if(config.UseParallel) state.Dependency = moveJob.ScheduleParallel(state.Dependency);
        else state.Dependency = moveJob.Schedule(state.Dependency);
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