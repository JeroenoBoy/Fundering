using Fundering.Animation.Components;
using Fundering.Animation.Data;
using Fundering.Components.Properties;
using Fundering.Components.Regular;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;



namespace Fundering.Animation.Systems
{
    // TODO: check animation system can work with different frame size animations 
    
    /// Compare <see cref="AnimationTimer"/> with global time and switch <see cref="FrameIndex"/> when timer expired.
    /// Perform only not-culled entities. Restore <see cref="FrameIndex"/> and duration time for entities which be culled for some time.
    /// Somehow calculations goes a bit wrong and unculled entities gets synchronized, don't know how to fix
    public partial struct SpriteUVAnimationSystem : ISystem
    {
        [BurstCompile]
        [WithNone(typeof(CullSpriteTag))]
        private partial struct AnimationJob : IJobEntity
        {
            public double Time;

            private void Execute(ref AnimationTimer animationTimer,
                                    ref FrameIndex frameIndex,
                                    ref UVAtlas uvAtlas,
                                    in AnimationSetLink animationSet,
                                    in AnimationIndex animationIndex)
            {
                double timerDelta = Time - animationTimer.Value;

                if (timerDelta >= 0f)
                {
                    ref SpriteAnimationBlobData animData = ref animationSet.Value.Value[animationIndex.Value];
                    int frameCount = animData.GridSize.x * animData.GridSize.y;
                    frameIndex.Value = (frameIndex.Value + 1) % frameCount;
                    float nextFrameDuration = animData.FrameDurations[frameIndex.Value];

                    if (timerDelta >= animData.AnimationDuration)
                    {
                        float extraTime = (float)(timerDelta % animData.AnimationDuration);
                        while (extraTime > nextFrameDuration)
                        {
                            extraTime -= nextFrameDuration;
                            frameIndex.Value = (frameIndex.Value + 1) % frameCount;
                            nextFrameDuration = animData.FrameDurations[frameIndex.Value];
                        }
                        nextFrameDuration -= extraTime;
                    }

                    animationTimer.Value = Time + nextFrameDuration;

                    float2 frameSize = new float2(animData.UVAtlas.xy / animData.GridSize);
                    int2 framePosition = new int2(frameIndex.Value % animData.GridSize.x, frameIndex.Value / animData.GridSize.x);
                    uvAtlas = new UVAtlas { Value = new float4(frameSize, animData.UVAtlas.zw + frameSize * framePosition) };
                }
            }
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            AnimationJob animationJob = new AnimationJob { Time = SystemAPI.Time.ElapsedTime };
            state.Dependency = animationJob.ScheduleParallelByRef(state.Dependency);
        }
    }
}