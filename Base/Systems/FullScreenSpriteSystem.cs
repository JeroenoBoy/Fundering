using Fundering.Common;
using Fundering.Components.Properties;
using Fundering.Components.Regular;
using Fundering.Transform2D;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;



namespace Fundering.Systems
{
    [UpdateAfter(typeof(UpdateCullingDataSystem))]
    public partial struct FullScreenSpriteSystem : ISystem
    {
        [BurstCompile]
        [WithAll(typeof(FullScreenSpriteTag))]
        private partial struct RecalculateSpritesJob : IJobEntity
        {
            public float2 CameraPosition;
            public float2 ScreenSize;
            
            private void Execute(ref Scale2D size, ref LocalTransform2D transform, ref UVTilingAndOffset uvTilingAndOffset, in NativeSpriteSize nativeSpriteSize)
            {
                size.Value = ScreenSize;
                transform.Position = CameraPosition;
                uvTilingAndOffset.Value = new float4(size.Value / nativeSpriteSize.Value, CameraPosition / nativeSpriteSize.Value - size.Value / nativeSpriteSize.Value / 2f);
            }
        }
        
        private struct SystemData : IComponentData
        {
            public float2 LastCameraPosition;
            public Bounds2D LastCameraBounds;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ = state.EntityManager.AddComponent<SystemData>(state.SystemHandle);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if(!SystemAPI.TryGetSingleton(out SpriteFrustumCullingSystem.CameraData cameraData))
                return;
            
            RefRW<SystemData> sysData = SystemAPI.GetComponentRW<SystemData>(state.SystemHandle);

            if(cameraData.CullingBounds2D != sysData.ValueRO.LastCameraBounds)
            {
                sysData.ValueRW.LastCameraBounds = cameraData.CullingBounds2D;
                
                RecalculateSpritesJob recalculateSpriteJob = new RecalculateSpritesJob
                {
                    CameraPosition = cameraData.Position,
                    ScreenSize = cameraData.CullingBounds2D.Size
                };
                state.Dependency = recalculateSpriteJob.ScheduleByRef(state.Dependency);
            }
        }
    }
}