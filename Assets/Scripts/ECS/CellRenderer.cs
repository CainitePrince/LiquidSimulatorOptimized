using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace WaterSimulation
{
    /// <summary>
    /// Unity DOTS system for rendering the cells.  
    /// </summary>
    [UpdateAfter(typeof(PrepareCellsForRendering))]
    public partial class CellRenderer : SystemBase
    {
        private EntityQuery _entityQuery;
        private int _valuesShaderProperty;
        private MaterialPropertyBlock _materialPropertyBlock;
        private readonly List<List<Matrix4x4>> _matrices = new List<List<Matrix4x4>>();
        private readonly List<List<Vector4>> _uvs = new List<List<Vector4>>();
        private bool _allocated = false;
        private bool _createdMatrixData = false;

        protected override void OnCreate()
        {
            base.OnCreate();

            _valuesShaderProperty = Shader.PropertyToID("_Values");
            _materialPropertyBlock = new MaterialPropertyBlock();
        }

        protected override void OnUpdate()
        {
            _entityQuery = GetEntityQuery(ComponentType.ReadOnly<CellRenderComponent>());

            NativeArray<CellRenderComponent> cellSpriteDataArray = _entityQuery.ToComponentDataArray<CellRenderComponent>(Allocator.TempJob);

            Material SpriteSheetMat = WaterSimulationGrid.GetInstance().WaterMaterial;
            Mesh mesh = WaterSimulationGrid.GetInstance().QuadMesh;

            int sliceCount = 1023;
            
            // Avoid allocating new dynamic arrays each frame.
            if (!_allocated)
            {
                int count = (cellSpriteDataArray.Length / sliceCount) + 1;

                for (int i = 0; i < count; ++i)
                {
                    _matrices.Add(new List<Matrix4x4>());
                    _uvs.Add(new List<Vector4>());
                }

                _allocated = true;
            }

            // The matrix data doesn't change, we can set this up once.
            int slice = 0;
            if (!_createdMatrixData)
            {
                for (int i = 0; i < cellSpriteDataArray.Length; i += sliceCount)
                {
                    int sliceSize = math.min(cellSpriteDataArray.Length - i, sliceCount);

                    for (int j = 0; j < sliceSize; j++)
                    {
                        CellRenderComponent cellComponentData = cellSpriteDataArray[i + j];
                        _matrices[slice].Add(cellComponentData.Matrix);
                    }

                    slice++;
                }
                _createdMatrixData = true;
            }

            slice = 0;
            //Account for limitations of DrawMeshInstanced
            for (int i = 0; i < cellSpriteDataArray.Length; i += sliceCount)
            {
                int sliceSize = math.min(cellSpriteDataArray.Length - i, sliceCount);

                _uvs[slice].Clear();
                
                for (int j = 0; j < sliceSize; j++)
                {
                    CellRenderComponent cellComponentData = cellSpriteDataArray[i + j];
                    _uvs[slice].Add(cellComponentData.UV);
                }

                _materialPropertyBlock.SetVectorArray(_valuesShaderProperty, _uvs[slice]);

                Graphics.DrawMeshInstanced(
                    mesh,
                    0,
                    SpriteSheetMat,
                    _matrices[slice],
                    _materialPropertyBlock);

                slice++;
            }

            cellSpriteDataArray.Dispose();
        }
    }
}
