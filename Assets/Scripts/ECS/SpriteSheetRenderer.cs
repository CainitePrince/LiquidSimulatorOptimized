using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

[UpdateAfter(typeof(SpriteSheetCalculationsJob))]
public class SpriteSheetRenderer : SystemBase
{
    private EntityQuery _entityQuery;
    private int _valuesShaderProperty;
    private MaterialPropertyBlock _materialPropertyBlock;

    protected override void OnCreate()
    {
        base.OnCreate();

        _valuesShaderProperty = Shader.PropertyToID("_Values");
        _materialPropertyBlock = new MaterialPropertyBlock();
    }

    protected override void OnUpdate()
    {
        _entityQuery = GetEntityQuery(ComponentType.ReadOnly<CellComponent>());

        NativeArray<CellComponent> cellSpriteDataArray = _entityQuery.ToComponentDataArray<CellComponent>(Allocator.TempJob);

        Material SpriteSheetMat = CreateTileMap.GetInstance().WaterMaterial;
        Mesh mesh = CreateTileMap.GetInstance().QuadMesh;
        
        //Account for limitations of DrawMeshInstanced
        int sliceCount = 1023;
        for (int i = 0; i < cellSpriteDataArray.Length; i += sliceCount)
        {
            int sliceSize = math.min(cellSpriteDataArray.Length - i, sliceCount);

            List<Matrix4x4> matrixList = new List<Matrix4x4>();
            List<Vector4> uvList = new List<Vector4>();
            for (int j = 0; j < sliceSize; j++)
            {
                CellComponent cellComponentData = cellSpriteDataArray[i + j];
                matrixList.Add(cellComponentData.Matrix);
                uvList.Add(cellComponentData.UV);
            }

            _materialPropertyBlock.SetVectorArray(_valuesShaderProperty, uvList);

            Graphics.DrawMeshInstanced(
                mesh,
                0,
                SpriteSheetMat,
                matrixList,
                _materialPropertyBlock);
         }

        cellSpriteDataArray.Dispose();
    }
}
