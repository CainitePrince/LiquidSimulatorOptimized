using Unity.Entities;
using UnityEngine;

namespace WaterSimulation
{
    /// <summary>
    /// Component for rendering cells in the simulation
    /// </summary>
    public struct CellRenderComponent : IComponentData
    {
        public Matrix4x4 Matrix;
        public Vector4 UV;
    }
}
