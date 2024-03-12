using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class SpriteSheetCalculationsJob : SystemBase//JobComponentSystem
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
            /*
            // Offset is 1 for empty cell.
            float offsetV = 1.0f;

            // Offset moves to zero when full
            if (cell.Liquid > 0.05f)
            {
                offsetV = Mathf.Clamp(1.0f - cell.Liquid, 0.0f, 1.0f);
            }

            if (cell.Solid)
            {
                offsetV = -2.0f;
            }

            if (cell.IsDownFlowingLiquid)
            {
                offsetV = 0.0f;
            }
            */
            //float angle = Mathf.PI;
            cell.UV = new UnityEngine.Vector4(cell.Solid? 2.0f : cell.IsDownFlowingLiquid? 1.0f : Mathf.Clamp01(cell.Liquid), angle, 0.0f, 0.0f);

            cell.Matrix = UnityEngine.Matrix4x4.TRS(
                new UnityEngine.Vector3(cell.WorldPos.x, cell.WorldPos.y, 0),
                Quaternion.identity,
                new UnityEngine.Vector3(cell.CellSize, cell.CellSize, 0));
        }).Schedule();
    }
    /*
    protected override void OnUpdate()
    {
        UnityEngine.Vector2 gravity = CreateTileMap.GetInstance().GetGravityVector();
        UnityEngine.Quaternion waterRotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(0, 0, 1), new UnityEngine.Vector3(-gravity.x, gravity.y, 0.0f));

        Entities.ForEach((ref CellComponent cell) =>
        {
            float uvWidth = 1f / 3;
            float uvHeight = 1f;
            float uvOffsetX;
            float uvOffsetY = -1f; // -1f = Full Size : 1f = Empty Cell

            //Make sure cells with liquid are rendered as water
            if (cell.Liquid > 0.05f && cell.SpriteSheetFrame != 1)
            {
                uvOffsetX = uvWidth * 1;
            }
            else
            {
                uvOffsetX = uvWidth * cell.SpriteSheetFrame;
            }

            //Fill Liquid Cells with Liquid above
            if (!cell.Solid && cell.Liquid != 0)
            {
                if(cell.IsDownFlowingLiquid)
                {
                    uvOffsetY = -1f;
                }
                else
                {
                    //Scale Water Cells based on amount of liquid contained
                    uvOffsetY = math.max(-1, -cell.Liquid);
                }
            }

            cell.UV = new UnityEngine.Vector4(uvWidth, uvHeight, uvOffsetX, uvOffsetY);

            var rotation = cell.Liquid > 0 ? waterRotation : UnityEngine.Quaternion.identity;

            cell.Matrix = UnityEngine.Matrix4x4.TRS(
                new UnityEngine.Vector3(cell.WorldPos.x, cell.WorldPos.y, 0),
                rotation,
                new UnityEngine.Vector3(cell.CellSize, cell.CellSize, 0));

        }).Schedule();

        //return jobHandle;
    }
    */

}
