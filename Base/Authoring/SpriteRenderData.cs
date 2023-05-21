using System;
using Fundering.Base.Data;
using UnityEngine;



namespace Fundering.Base.Authoring
{
    [Serializable]
    public struct SpriteRenderData
    {
        public int ID => Material.GetHashCode();
        public Material Material;
        public PropertiesSet PropertiesSet;
    }
}
