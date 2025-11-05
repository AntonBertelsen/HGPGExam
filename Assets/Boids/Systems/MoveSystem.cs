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
        if (boidTag.dead)
        {
            transform.Position += new float3(0, -1, 0) * DeltaTime;
            transform.Rotation = Quaternion.LookRotation(velocity.Value);  
        }
        else
        {
            transform.Position += velocity.Value * DeltaTime;
            transform.Rotation = Quaternion.LookRotation(velocity.Value);  
        }

    }
}