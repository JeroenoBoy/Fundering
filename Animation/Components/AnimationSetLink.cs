using Fundering.Animation.Data;
using Unity.Entities;



namespace Fundering.Animation.Components
{
    public struct AnimationSetLink : IComponentData
    {
        public BlobAssetReference<BlobArray<SpriteAnimationBlobData>> Value;
    }
}