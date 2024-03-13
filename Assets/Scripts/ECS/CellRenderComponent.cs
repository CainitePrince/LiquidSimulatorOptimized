using Unity.Entities;
using UnityEngine;

namespace WaterSimulation
{
    public struct CellRenderComponent : IComponentData
    {
        public Matrix4x4 Matrix;
        public Vector4 UV;
        public Unity.Mathematics.float2 WorldPos;
    }
}
