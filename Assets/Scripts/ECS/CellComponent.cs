using UnityEngine;
using Unity.Entities;

namespace WaterSimulation
{
    public struct CellComponent : IComponentData
    {
        public Matrix4x4 Matrix;
        public Vector4 UV;

        //World Pos
        public Unity.Mathematics.float2 WorldPos;

        //CellSize
        public float CellSize;

        //Grid Pos & index
        public int xGrid;
        public int yGrid;
        public int index;

        //Check is Water is settled
        public int SettleCount;

        public float Liquid;

        //Neighbor Cells
        public int BottomIndex;
        public int TopIndex;
        public int LeftIndex;
        public int RightIndex;
        public int BottomLeftIndex;
        public int TopLeftIndex;
        public int TopRightIndex;
        public int BottomRightIndex;

        //Values stored for modifying self and neighbor
        public float ModifySelf;
        public float ModifyBottom;
        public float ModifyTop;
        public float ModifyLeft;
        public float ModifyRight;
        public float ModifyBottomLeft;
        //public float ModifyUpLeft;
        //public float ModifyUpRight;
        public float ModifyBottomRight;

        //Empty(0) or Solid(1)
        public bool Solid;
        public bool Settled;
        public bool IsDownFlowingLiquid;
    }
}
