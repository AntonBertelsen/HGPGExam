using Unity.Entities;
using UnityEngine;

class BulletBaker : MonoBehaviour
{
    public float timeToExplode;
    public float timeLived;
    public GameObject _explosionPrefab;   
}

class BulletAuthoringBaker : Baker<BulletBaker>
{
    public override void Bake(BulletBaker authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new BulletVelocity
            {
                Value = 10
            });
        AddComponent<BulletTag>(entity);
        AddComponent(entity, new BulletComponent
        {
            timeLived = authoring.timeLived,
            timeToExplode = authoring.timeToExplode,
            explosion = GetEntity(authoring._explosionPrefab, TransformUsageFlags.Dynamic)
        });
    }
}
