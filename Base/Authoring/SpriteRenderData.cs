using System;
using Fundering.Data;
using UnityEngine;



namespace Fundering.Authoring
{
    [Serializable]
    public struct SpriteRenderData
    {
        public int ID => Material.GetHashCode();
        public Material Material;
        public PropertiesSet PropertiesSet;
    }
}
