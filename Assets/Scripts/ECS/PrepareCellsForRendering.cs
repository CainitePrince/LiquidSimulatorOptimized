using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace WaterSimulation
{
    /// <summary>
    /// Updates the UV information. This is used by the shader to draw the cell.
    /// </summary>
    public partial class PrepareCellsForRendering : SystemBase
    {
        protected override void OnUpdate()
        {
            Vector2 gravity = WaterSimulationGrid.GetInstance().GetGravityVector();

            Vector2 defaultGravity = new(0.0f, 1.0f);

            float dot = Vector2.Dot(gravity, defaultGravity);
            float det = gravity.x * defaultGravity.y - gravity.y * defaultGravity.x;

            // Angle for water rotation
            float angle = Mathf.Atan2(det, dot);

            // Scaling factor, because diagonal of a square is larger
            float a = Vector2.Dot(new Vector2(0.707f, 0.707f), new Vector2(Mathf.Abs(gravity.x), Mathf.Abs(gravity.y)));
            float ratio = Mathf.Lerp(1.0f, 1.414f, (a - 0.5f) * 2.0f);

            WaterSimulationGrid.GetInstance().WaterMaterial.SetFloat("_Ratio", ratio);

            Entities.ForEach((ref CellSimulationComponent cellSimulationComponent, ref CellRenderComponent cellRenderComponent) =>
            {
                cellRenderComponent.UV = new Vector4(cellSimulationComponent.Solid ? 2.0f : cellSimulationComponent.IsDownFlowingLiquid ? 1.0f : Mathf.Clamp01(cellSimulationComponent.Liquid), angle, 0.0f, 0.0f);
            }).Schedule();
        }
    }
}
