using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace WaterSimulation
{
    public partial class PrepareCellsForRendering : SystemBase
    {
        protected override void OnUpdate()
        {
            Vector2 gravity = CreateTileMap.GetInstance().GetGravityVector(out bool isDiagonal);
            gravity.Normalize();

            Vector2 defaultGravity = new Vector2(0.0f, 1.0f);

            float dot = Vector2.Dot(gravity, defaultGravity);
            float det = gravity.x * defaultGravity.y - gravity.y * defaultGravity.x;

            float angle = Mathf.Atan2(det, dot);

            CreateTileMap.GetInstance().WaterMaterial.SetFloat("_Ratio", isDiagonal ? 1.414f : 1.0f);

            Entities.ForEach((ref CellComponent cell) =>
            {
                cell.UV = new Vector4(cell.Solid ? 2.0f : cell.IsDownFlowingLiquid ? 1.0f : Mathf.Clamp01(cell.Liquid), angle, 0.0f, 0.0f);

                cell.Matrix = Matrix4x4.TRS(
                    new Vector3(cell.WorldPos.x, cell.WorldPos.y, 0),
                    Quaternion.identity,
                    new Vector3(1.0f, 1.0f, 0.0f));
            }).Schedule();
        }
    }
}
