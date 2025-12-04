using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(BoidSystem))]
[UpdateBefore(typeof(MoveSystem))]
public partial struct DeathSystem : ISystem
{
    private int frameCount;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSettings>();

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        frameCount++;
        if (frameCount % 60 == 0)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (boidTag, transform,entity) in SystemAPI
                         .Query<RefRW<BoidTag>, RefRO<LocalToWorld>>().WithAll<BoidTag>().WithEntityAccess())
            {
                if (boidTag.ValueRW.dead && transform.ValueRO.Position.y < -10) ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
