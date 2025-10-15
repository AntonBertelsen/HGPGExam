using Unity.Entities;
using UnityEngine;

class BulletBaker : MonoBehaviour
{
    public float timeToExplode;
    public float timeLived;
}

class BulletAuthoringBaker : Baker<BulletBaker>
{
    public override void Bake(BulletBaker authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<Velocity>(entity);
        AddComponent<BulletTag>(entity);
        AddComponent(entity, new BulletComponent
        {
            timeLived = authoring.timeLived,
            timeToExplode = authoring.timeToExplode
        });
    }
}
