using Unity.Entities;
using UnityEngine;

class BulletAuthoring : MonoBehaviour
{
    public float timeToExplode;
    public float timeLived;
    public GameObject _explosionPrefab;   
}

class BulletBaker : Baker<BulletAuthoring>
{
    public override void Bake(BulletAuthoring authoring)
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
