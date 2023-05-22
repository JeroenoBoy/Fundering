using System.Collections.Generic;
using Fundering.Authoring;
using NSprites;
using Unity.Collections;
using Unity.Entities;



namespace Fundering.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.Default)]
    public partial class RegisterRenderersSystem : SystemBase
    {
        private EntityQuery renderArchetypeToRegisterQuery;
        private EntityQuery renderArchetypeIndexLessEntitiesQuery;
        private HashSet<int> registeredIDsSet = new();

        protected override void OnCreate()
        {
            base.OnCreate();
            renderArchetypeToRegisterQuery = GetEntityQuery
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
            renderArchetypeIndexLessEntitiesQuery = GetEntityQuery
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
            EntityManager.AddComponent<SpriteRenderID>(renderArchetypeIndexLessEntitiesQuery);

            void Register(in NativeArray<Entity> entities)
            {
                if (!SystemAPI.ManagedAPI.TryGetSingleton<RenderArchetypeStorage>(out RenderArchetypeStorage renderArchetypeStorage))
                    return;

                for(int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    SpriteRenderDataToRegister renderData = EntityManager.GetComponentObject<SpriteRenderDataToRegister>(entity);

                    if (!registeredIDsSet.Contains(renderData.Data.ID))
                    {
                        renderArchetypeStorage.RegisterRender
                        (
                            renderData.Data.ID,
                            renderData.Data.Material,
                            propertyDataSet: renderData.Data.PropertiesSet.PropertyData
                        );
                        _ = registeredIDsSet.Add(renderData.Data.ID);
                    }

                    EntityManager.SetSharedComponentManaged(entity, new SpriteRenderID { id = renderData.Data.ID });
                }
            }
            Register(renderArchetypeToRegisterQuery.ToEntityArray(Allocator.Temp));

            EntityManager.RemoveComponent<SpriteRenderDataToRegister>(renderArchetypeToRegisterQuery);
        }
    }
}
