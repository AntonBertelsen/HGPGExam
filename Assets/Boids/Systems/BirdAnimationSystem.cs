using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct BirdAnimationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Pass global time for the wave functions
        float time = (float)SystemAPI.Time.ElapsedTime;
        float deltaTime = SystemAPI.Time.DeltaTime;

        var job = new BirdAnimationJob
        {
            Time = time,
            DeltaTime = deltaTime
        };
        
        job.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct BirdAnimationJob : IJobEntity
{
    public float Time;
    public float DeltaTime;

    void Execute(
        [EntityIndexInQuery] int entityIndex,
        // Removed 'ref BirdAnimation anim' as requested. 
        // We now use the properties themselves to persist state.
        ref BirdAnimationFrameProperty frameProp,
        ref BirdScaleProperty scaleProp,
        in Velocity vel)
    {
        // 0. Generate "Personality"
        // Create a deterministic random seed based on the bird's index.
        // This ensures the same bird behaves consistently, but different birds behave uniquely.
        var random = Unity.Mathematics.Random.CreateFromIndex((uint)entityIndex);

        // Personality Factors:
        // freqOffset: Variance in wing beat frequency (+/- 15%)
        float freqOffset = random.NextFloat(0.85f, 1.15f); 
        // glideThreshold: How "wide" the sine wave flap bursts are. 
        // Low val = flaps often (nervous). High val = long glides (calm).
        float glideThreshold = random.NextFloat(0.4f, 0.9f); 
        // diveTolerance: At what negative speed do they tuck wings?
        // -5 (tucks early) to -25 (flaps even while diving)
        float diveTolerance = random.NextFloat(-25.0f, -5.0f);
        // effortBias: Offsets the urgency. 
        // Positive = always feels urgency (flaps more). Negative = lazy.
        float effortBias = random.NextFloat(-0.1f, 0.2f);


        // 1. Analyze Velocity
        float3 v = vel.Value;
        float vertSpeed = v.y; 
        
        // RESTORED: Original scale factor of 100.0f. 
        float speed = math.length(v) * 100.0f;

        // 2. Calculate "Urgency" (0 to 1)
        // We add effortBias here. A bird with high bias feels urgency even when flying level.
        float perceivedVertSpeed = vertSpeed + (effortBias * 5.0f);
        float urgency = math.smoothstep(-2.0f, 5.0f, perceivedVertSpeed);

        // 3. Generate the "Intermittent Cruise" Pattern
        // Flap... Flap... Glide...
        float cruiseFreq = 3.0f * freqOffset; // Apply frequency personality
        // Add a large random offset to phase so they are desynchronized
        float cruisePhase = (Time * cruiseFreq) + (entityIndex * 13.13f);
        float baseSine = math.sin(cruisePhase);
        
        // Sharpen sine: The 'glideThreshold' now determines how much of the wave is "flap" vs "coast".
        float intermittentSignal = math.smoothstep(0.0f, glideThreshold, baseSine);

        // 4. Calculate Target Intensity
        float highEffortSignal = math.lerp(1.0f, 1.3f, urgency);
        
        // Blend: If urgency is high, override the intermittent signal.
        float targetIntensity = math.max(intermittentSignal, urgency * highEffortSignal);

        // 5. Dive Suppression
        // Uses individual diveTolerance. 
        // Some birds will stop flapping at -5 speed, others will keep going until -25.
        float diveFactor = math.smoothstep(0.0f, diveTolerance, vertSpeed);
        targetIntensity = math.lerp(targetIntensity, 0.0f, diveFactor);

        // 6. Smooth the Scale (Animation Damping)
        float currentScale = scaleProp.Value;
        
        // Flapping starts snappy (10f), stopping/coasting acts floaty (2f)
        float dampSpeed = targetIntensity > currentScale ? 10f : 2f; 
        float newScale = math.lerp(currentScale, targetIntensity, DeltaTime * dampSpeed);
        
        scaleProp.Value = newScale;

        // 7. Calculate Animation Playback Speed
        // RESTORED: Base speed 1.0f as requested.
        float baseSpeed = 1.0f; 
        
        // Apply freqOffset to the multiplier as well, so smaller/faster flapping birds animate faster.
        float flapRateMultiplier = 0.5f + (urgency * 1.0f) + (speed * 0.1f);
        flapRateMultiplier *= freqOffset;

        // Don't freeze completely (0.1) to keep wings "alive" even when coasting
        float effectivePlaybackSpeed = math.lerp(0.1f, flapRateMultiplier, math.saturate(newScale));
        
        // Integrate frame
        frameProp.Value += baseSpeed * effectivePlaybackSpeed * DeltaTime;
    }
}