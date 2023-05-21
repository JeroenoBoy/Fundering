using System.Collections.Generic;
using Fundering.Base.Authoring;
using NSprites;
using Unity.Collections;
using Unity.Entities;



namespace Fundering.Base.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.Default)]
    public partial class RegisterRenderersSystem : SystemBase
    {
        private EntityQuery _renderArchetypeToRegisterQuery;
        private EntityQuery _renderArchetypeIndexLessEntitiesQuery;
        private HashSet<int> _registeredIDsSet = new();

        protected override void OnCreate()
        {
            base.OnCreate();
            _renderArchetypeToRegisterQuery = GetEntityQuery
            (
                new EntityQueryDesc
                {
                    All = new []
                    {
                        ComponentType.ReadOnly<SpriteRenderDataToRegister>(),
                        ComponentType.ReadOnly<SpriteRenderID>()
                    },
                    Options = EntityQueryOptions.IncludePrefab
                }
            );
            _renderArchetypeIndexLessEntitiesQuery = GetEntityQuery
            (
                 new EntityQueryDesc
                 {
                    All = new []
                    {
                        ComponentType.ReadOnly<SpriteRenderDataToRegister>()
                    },
                    None = new []
                    {
                        ComponentType.ReadOnly<SpriteRenderID>()
                    },
                    Options = EntityQueryOptions.IncludePrefab
                 }
            );
    }
        protected override void OnUpdate()
        {
            EntityManager.AddComponent<SpriteRenderID>(_renderArchetypeIndexLessEntitiesQuery);

            void Register(in NativeArray<Entity> entities)
            {
                if (!SystemAPI.ManagedAPI.TryGetSingleton<RenderArchetypeStorage>(out RenderArchetypeStorage renderArchetypeStorage))
                    return;

                for(int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    SpriteRenderDataToRegister renderData = EntityManager.GetComponentObject<SpriteRenderDataToRegister>(entity);

                    if (!_registeredIDsSet.Contains(renderData.data.ID))
                    {
                        renderArchetypeStorage.RegisterRender
                        (
                            renderData.data.ID,
                            renderData.data.Material,
                            propertyDataSet: renderData.data.PropertiesSet.PropertyData
                        );
                        _ = _registeredIDsSet.Add(renderData.data.ID);
                    }

                    EntityManager.SetSharedComponentManaged(entity, new SpriteRenderID { id = renderData.data.ID });
                }
            }
            Register(_renderArchetypeToRegisterQuery.ToEntityArray(Allocator.Temp));

            EntityManager.RemoveComponent<SpriteRenderDataToRegister>(_renderArchetypeToRegisterQuery);
        }
    }
}
