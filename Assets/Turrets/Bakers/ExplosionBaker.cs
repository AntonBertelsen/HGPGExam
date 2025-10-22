using Unity.Entities;
using UnityEngine;

class ExplosionBaker : MonoBehaviour
{
    public float timeLived;
    public float lifeExpetancy;
}

class ExplosionAuthoringBaker : Baker<ExplosionBaker>
{
    public override void Bake(ExplosionBaker authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<ExplosionTag>(entity);
        AddComponent(entity, new ExplosionComponent
        {
            lifeExpetancy = authoring.lifeExpetancy,
            timeLived = authoring.timeLived,
        });
    }
}
