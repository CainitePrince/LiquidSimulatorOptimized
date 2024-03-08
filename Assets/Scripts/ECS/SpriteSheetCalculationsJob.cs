using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class SpriteSheetCalculationsJob : SystemBase//JobComponentSystem
{
    protected override /*JobHandle*/ void OnUpdate(/*JobHandle inputDeps*/)
    {
        UnityEngine.Vector2 gravity = CreateTileMap.GetInstance().GetGravityVector();
        var rotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(0, 0, 1), new UnityEngine.Vector3(-gravity.x, gravity.y, 0.0f));

        /*JobHandle jobHandle =*/ Entities.ForEach((ref CellComponent cc) =>
        {
            float uvWidth = 1f / 3;
            float uvHeight = 1f;
            float uvOffsetX;
            float uvOffsetY = -1f; // -1f = Full Size : 1f = Empty Cell

            //Make sure cells with liquid are rendered as water
            if (cc.Liquid > 0.05f && cc.SpriteSheetFrame != 1)
            {
                uvOffsetX = uvWidth * 1;
            }
            else
            {
                uvOffsetX = uvWidth * cc.SpriteSheetFrame;
            }

            //Fill Liquid Cells with Liquid above
            if (!cc.Solid && cc.Liquid != 0)
            {
                if(cc.IsDownFlowingLiquid)
                {
                    uvOffsetY = -1f;
                }
                else
                {
                    //Scale Water Cells based on amount of liquid contained
                    uvOffsetY = math.max(-1, -cc.Liquid);
                }
            }

            cc.UV = new UnityEngine.Vector4(uvWidth, uvHeight, uvOffsetX, uvOffsetY);

            cc.Matrix = UnityEngine.Matrix4x4.TRS(
                new UnityEngine.Vector3(cc.WorldPos.x, cc.WorldPos.y, 0),
                /*UnityEngine.Quaternion.identity*/rotation,
                new UnityEngine.Vector3(cc.CellSize, cc.CellSize, 0));

        }).Schedule(/*inputDeps*/);

        //return jobHandle;
    }

}
