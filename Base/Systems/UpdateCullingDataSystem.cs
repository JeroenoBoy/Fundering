using Fundering.Base.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;



namespace Fundering.Base.Systems
{
    [UpdateBefore(typeof(SpriteFrustumCullingSystem))]
    public partial struct UpdateCullingDataSystem : ISystem
    {
        private class SystemData : IComponentData
        {
            private Camera _camera;

            public Camera Camera
            {
                get
                {
                    if(_camera == null)
                        _camera = Camera.main;
                    return _camera;
                }
            }
        }

        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.AddComponentObject(state.SystemHandle, new SystemData());
        }

        public void OnUpdate(ref SystemState state)
        {
            Camera camera = state.EntityManager.GetComponentObject<SystemData>(state.SystemHandle).Camera;
            Vector3 cameraPos = camera.transform.position;
            Vector3 leftBottomPoint = camera.ScreenToWorldPoint(new Vector3(0f, 0f, 0f));
            Vector3 rightUpPoint = camera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0f));
            Bounds2D cameraViewBounds2D = new Bounds2D(new float2x2(new float2(leftBottomPoint.x, leftBottomPoint.y), new float2(rightUpPoint.x, rightUpPoint.y)));
            SystemAPI.SetSingleton(new SpriteFrustumCullingSystem.CameraData
            {
                Position = new float2(cameraPos.x, cameraPos.y),
                CullingBounds2D = cameraViewBounds2D
            });
        }
    }
}