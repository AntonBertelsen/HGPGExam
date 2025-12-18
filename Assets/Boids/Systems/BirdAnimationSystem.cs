using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(MoveSystem))]
[UpdateAfter(typeof(LanderSystem))]
public partial struct BirdAnimationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSettings>();
        state.RequireForUpdate<KdTree>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var time = (float)SystemAPI.Time.ElapsedTime;
        var deltaTime = SystemAPI.Time.DeltaTime;

        var animationJob = new BirdAnimationJob
        {
            Time = time,
            DeltaTime = deltaTime,
        };

        var perchJob = new BirdPerchAnimationJob
        {
            KdTree = SystemAPI.GetSingleton<KdTree>(),
            DeltaTime = deltaTime,
        };
        var config = SystemAPI.GetSingleton<BoidSettings>();
        if(config.UseParallel) state.Dependency = animationJob.ScheduleParallel(state.Dependency);
        else state.Dependency = animationJob.Schedule(state.Dependency);
        if(config.UseParallel) state.Dependency = perchJob.ScheduleParallel(state.Dependency);
        else state.Dependency = perchJob.Schedule(state.Dependency);
    }
}

[BurstCompile]
public partial struct BirdAnimationJob : IJobEntity
{
    public float Time;
    public float DeltaTime;

    private void Execute(
        [EntityIndexInQuery] int entityIndex,
        ref BirdAnimationFrameProperty frameProp,
        ref BirdScaleProperty scaleProp,
        in Velocity vel)
    {
        var random = Random.CreateFromIndex((uint)entityIndex);

        var freqOffset = random.NextFloat(0.85f, 1.15f);
        var glideThreshold = random.NextFloat(0.4f, 0.9f);
        var diveTolerance = random.NextFloat(-25.0f, -5.0f);
        var effortBias = random.NextFloat(-0.1f, 0.2f);

        var v = vel.Value;
        var vertSpeed = v.y;

        var speed = math.length(v) * 100.0f;

        var perceivedVertSpeed = vertSpeed + (effortBias * 5.0f);
        var urgency = math.smoothstep(-2.0f, 5.0f, perceivedVertSpeed);

        var cruiseFreq = 3.0f * freqOffset;
        var cruisePhase = (Time * cruiseFreq) + (entityIndex * 13.13f);
        var baseSine = math.sin(cruisePhase);
        var intermittentSignal = math.smoothstep(0.0f, glideThreshold, baseSine);

        var highEffortSignal = math.lerp(1.0f, 1.3f, urgency);
        var targetIntensity = math.max(intermittentSignal, urgency * highEffortSignal);

        var diveFactor = math.smoothstep(0.0f, diveTolerance, vertSpeed);
        targetIntensity = math.lerp(targetIntensity, 0.0f, diveFactor);
        var currentScale = scaleProp.Value;
        var dampSpeed = targetIntensity > currentScale ? 10f : 2f;
        var newScale = math.lerp(currentScale, targetIntensity, DeltaTime * dampSpeed);

        scaleProp.Value = newScale;

        const float baseSpeed = 1.0f;
        var flapRateMultiplier = 0.5f + (urgency * 1.0f) + (speed * 0.1f);
        flapRateMultiplier *= freqOffset;
        var effectivePlaybackSpeed = math.lerp(0.1f, flapRateMultiplier, math.saturate(newScale));

        frameProp.Value += baseSpeed * effectivePlaybackSpeed * DeltaTime;
    }
}

public partial struct BirdPerchAnimationJob : IJobEntity
{
    [ReadOnly] public KdTree KdTree;
    [ReadOnly] public float DeltaTime;

    private void Execute(
        in Lander lander,
        ref LocalTransform transform,
        ref BirdAnimationFrameProperty frameProp,
        ref BirdScaleProperty scaleProp,
        ref BirdPerchedProperty perchProp)
    {
        var normal = KdTree.GetNormal(lander.TargetIndex);
        var currentForward = math.rotate(transform.Rotation, math.forward());
        var projectedForward = math.normalizesafe(currentForward - normal * math.dot(currentForward, normal));

        if (math.lengthsq(projectedForward) < 0.01f)
        {
            projectedForward = math.forward();
        }

        var distSq = math.distancesq(transform.Position, lander.Target);
        var targetRot = quaternion.LookRotationSafe(projectedForward, normal);

        if (distSq < 0.005f && perchProp.Value > 0.95f)
        {
            transform.Position = lander.Target;
            transform.Rotation = targetRot;

            perchProp.Value = 1.0f;
            scaleProp.Value = 0.0f;

            return;
        }

        var lerpSpeed = 5.0f * DeltaTime;
        transform.Position = math.lerp(transform.Position, lander.Target, lerpSpeed);
        transform.Rotation = math.slerp(transform.Rotation, targetRot, lerpSpeed);

        if (distSq > 0.01f)
        {
            scaleProp.Value = math.lerp(scaleProp.Value, 1.0f, lerpSpeed * 2.0f);
            perchProp.Value = math.lerp(perchProp.Value, 0.0f, lerpSpeed * 2.0f);
            frameProp.Value += 60.0f * DeltaTime;
        }
        else
        {
            scaleProp.Value = math.lerp(scaleProp.Value, 0.0f, lerpSpeed * 5.0f);
            perchProp.Value = math.lerp(perchProp.Value, 1.0f, lerpSpeed * 3.0f);

            if (scaleProp.Value > 0.01f)
            {
                frameProp.Value += 30.0f * DeltaTime;
            }
        }
    }
}