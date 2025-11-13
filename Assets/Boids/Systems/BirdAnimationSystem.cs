using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public partial struct BirdAnimationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        var job = new BirdAnimationJob { DeltaTime = deltaTime };
        job.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct BirdAnimationJob : IJobEntity
{
    public float DeltaTime;

    void Execute(ref BirdAnimation anim, 
        ref BirdAnimationFrameProperty frameProp,
        ref BirdScaleProperty scaleProp, 
        in Velocity vel)
    {
        float3 v = vel.Value;
        float speed = math.length(v) * 100.0f;
        float up = math.saturate(math.unlerp(0f, 2f, v.y));

        // --- Modulation rules ---
        float targetScale = math.lerp(0.05f, 1.0f, up);           // glide <-> flap
        float animSpeed = anim.BaseSpeed * (0.5f + speed * 0.1f); // faster motion = faster wings

        // --- Integrate frame ---
        anim.AnimationFrame += animSpeed * DeltaTime;
        //anim.AnimationFrame %= 1f; // loop normalized frame 0..1

        //anim.Scale = math.clamp(math.lerp(anim.Scale, targetScale, DeltaTime * 3f), 0f, 1f);
        anim.Scale = 1.0f;

        // --- Write to material properties ---
        frameProp.Value = anim.AnimationFrame;
        scaleProp.Value = anim.Scale;
    }
}