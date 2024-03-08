using UnityEngine;
using Unity.Entities;

public struct CellComponent : IComponentData
{
    public Matrix4x4 Matrix;
    public Vector4 UV;

    //World Pos
    public Unity.Mathematics.float2 WorldPos;

    //Rendering Data
    //0 = Empty //1 = Water //2 == Wall
    public int SpriteSheetFrame;

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

    //Values stored for modifying self and neighbor
    public float modifySelf;
    public float modifyBottom;
    public float modifyTop;
    public float modifyLeft;
    public float modifyRight;

    //Empty(0) or Solid(1)
    public bool Solid;
    public bool Settled;
    public bool IsDownFlowingLiquid;
}
