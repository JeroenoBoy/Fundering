using Fundering.Animation.Components;
using Fundering.Animation.Data;
using NSprites;
using Unity.Entities;



namespace Fundering.Animation.Aspects
{
    public readonly partial struct AnimatorAspect : IAspect
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly Entity entity;
#endif
        private readonly RefRW<AnimationIndex> animationIndex;
        private readonly RefRW<AnimationTimer> animationTimer;
        private readonly RefRW<FrameIndex> frameIndex;
        private readonly RefRO<AnimationSetLink> animationSetLink;

        public void SetAnimation(int toAnimationIndex, in double worldTime)
        {
            // find animation by animation ID
            ref BlobArray<SpriteAnimationBlobData> animSet        = ref animationSetLink.ValueRO.Value.Value;
            int             setToAnimIndex = -1;
            for (int i = 0; i < animSet.Length; i++)
                if (animSet[i].ID == toAnimationIndex)
                {
                    setToAnimIndex = i;
                    break;
                }

            if (setToAnimIndex == -1)
                throw new NSpritesException($"{nameof(AnimatorAspect)}.{nameof(SetAnimation)}: incorrect {nameof(toAnimationIndex)} was passed. {entity} has no animation with such ID ({toAnimationIndex}) was found");

            if (animationIndex.ValueRO.Value != setToAnimIndex)
            {
                ref SpriteAnimationBlobData animData = ref animSet[setToAnimIndex];
                animationIndex.ValueRW.Value = setToAnimIndex;
                // here we want to set last frame and timer to 0 (equal to current time) to force animation system instantly switch
                // animation to 1st frame after we've modified it
                frameIndex.ValueRW.Value = animData.FrameDurations.Length - 1;
                animationTimer.ValueRW.Value = worldTime;
            }
        }

        public void SetToFrame(int frameIndex, in double worldTime)
        {
            ref SpriteAnimationBlobData animData = ref animationSetLink.ValueRO.Value.Value[animationIndex.ValueRO.Value];
            this.frameIndex.ValueRW.Value = frameIndex;
            animationTimer.ValueRW.Value = worldTime + animData.FrameDurations[frameIndex];
        }

        public void ResetAnimation(in double worldTime) =>
            SetToFrame(0, worldTime);
    }
}