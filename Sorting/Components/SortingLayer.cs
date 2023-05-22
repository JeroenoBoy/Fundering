using System;
using Unity.Entities;



namespace Fundering.Sorting.Components
{
    public struct SortingLayer : ISharedComponentData, IComparable<SortingLayer>
    {
        public int Index;

        public int CompareTo(SortingLayer other)
        {
            return Index.CompareTo(other.Index);
        }
    }
}