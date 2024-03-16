using Unity.Entities;
using UnityEngine;

namespace WaterSimulation
{
    /// <summary>
    /// Component for rendering cells in the simulation
    /// </summary>
    public struct CellRenderComponent : IComponentData
    {
        public Vector4 UV; 
        public Vector2 WorldPosition;
    }
}
