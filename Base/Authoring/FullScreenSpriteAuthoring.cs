using Fundering.Components.Regular;
using Unity.Entities;
using UnityEngine;



namespace Fundering.Authoring
{
    public class FullScreenSpriteAuthoring : MonoBehaviour
    {
        [SerializeField] private SpriteRendererAuthoring _spriteAuthoring;
        
        private partial class Baker : Baker<FullScreenSpriteAuthoring>
        {
            public override void Bake(FullScreenSpriteAuthoring authoring)
            {
                if(authoring._spriteAuthoring == null)
                    return;
                
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent<FullScreenSpriteTag>(entity);
                AddComponent(entity, new NativeSpriteSize{ Value = authoring._spriteAuthoring.NativeSpriteSize });

                DependsOn(authoring._spriteAuthoring);
            }
        }
    }
}