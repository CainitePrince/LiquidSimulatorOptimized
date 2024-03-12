using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

[UpdateAfter(typeof(SpriteSheetCalculationsJob))]
public class SpriteSheetRenderer : SystemBase//ComponentSystem
{
    private EntityQuery _entityQuery;
    private int _uvShaderProperty;
    //private int _rotationShaderProperty;
    private MaterialPropertyBlock _materialPropertyBlock;
    //private Matrix4x4 _identityMatrix;
    //private Matrix4x4 _waterMatrix;

    protected override void OnCreate()
    {
        base.OnCreate();

        _uvShaderProperty = Shader.PropertyToID(/*"_MainTex_UV"*/"_Values");
        //_rotationShaderProperty = Shader.PropertyToID("_Rotation");
        _materialPropertyBlock = new MaterialPropertyBlock();
        //_identityMatrix = Matrix4x4.identity;
        //_waterMatrix = Matrix4x4.identity;
    }

    protected override void OnUpdate()
    {
        //UnityEngine.Vector2 gravity = CreateTileMap.GetInstance().GetGravityVector();
        //UnityEngine.Quaternion waterRotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(0, 0, 1), new UnityEngine.Vector3(-gravity.x, gravity.y, 0.0f));
        //_waterMatrix = Matrix4x4.TRS(Vector3.zero, waterRotation, Vector3.one);

        _entityQuery = GetEntityQuery(ComponentType.ReadOnly<CellComponent>());

        NativeArray<CellComponent> cellSpriteDataArray = _entityQuery.ToComponentDataArray<CellComponent>(Allocator.TempJob);

        //MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
        Material SpriteSheetMat = CreateTileMap.GetInstance().WaterMaterial;
        Mesh mesh = CreateTileMap.GetInstance().QuadMesh;
        

        //Account for limitations of DrawMeshInstanced
        int sliceCount = 1023;
        for (int i = 0; i < cellSpriteDataArray.Length; i += sliceCount)
        {
            int sliceSize = math.min(cellSpriteDataArray.Length - i, sliceCount);

            List<Matrix4x4> matrixList = new List<Matrix4x4>();
            List<Vector4> uvList = new List<Vector4>();
            //List<Matrix4x4> uvRotation = new List<Matrix4x4>();
            for (int j = 0; j < sliceSize; j++)
            {
                CellComponent cellComponentData = cellSpriteDataArray[i + j];
                matrixList.Add(cellComponentData.Matrix);
                uvList.Add(cellComponentData.UV);
                /*
                if (cellComponentData.Liquid > 0)
                {
                    uvRotation.Add(_waterMatrix);
                }
                else
                {
                    uvRotation.Add(_identityMatrix);
                }
                */
            }

            _materialPropertyBlock.SetVectorArray(_uvShaderProperty, uvList);
            //_materialPropertyBlock.SetMatrixArray(_rotationShaderProperty, uvRotation);

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
