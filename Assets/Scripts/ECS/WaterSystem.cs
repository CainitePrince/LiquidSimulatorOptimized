using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;

public class LiquidSimulator : JobComponentSystem
{
    private EntityQuery entityQuery;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    //protected override void OnUpdate()
    {

        //Get Entity Query of CellComponents
        entityQuery = GetEntityQuery(ComponentType.ReadOnly<CellComponent>());

        //Create Readable Current Array
        NativeArray<CellComponent> current = entityQuery.ToComponentDataArray<CellComponent>(Allocator.TempJob);

        //Create Writable next(Future) Array
        var next = new NativeArray<CellComponent>(current.Length, Allocator.TempJob);

        //Set next = current to preserve data
        next.CopyFrom(current);

        // Max and min cell liquid values
        float MaxLiquid = 1.0f;
        float MinLiquid = 0.005f;

        // Extra liquid a cell can store than the cell above it
        float MaxCompression = 0.25f;

        // Lowest and highest amount of liquids allowed to flow per iteration
        float MinFlow = 0.005f;
        float MaxFlow = 4f;

        // Adjusts flow speed (0.0f - 1.0f)
        float FlowSpeed = 1f;

        //Grid Width of Map
        int GridWidth = CreateTileMap.GetInstance().GridWidth;

        //Calculate Water Physics
        inputDeps = new CalculateWaterPhysics()
        {
            current = current,
            next = next,
            MaxLiquid = MaxLiquid,
            MinLiquid = MinLiquid,
            MaxCompression = MaxCompression,
            MinFlow = MinFlow,
            MaxFlow = MaxFlow,
            FlowSpeed = FlowSpeed,
            GridWidth = GridWidth,
            //deltaTime = Time.DeltaTime //Delta Time is applied to Flow
        }.Schedule(current.Length, 32);

        //Complete Physics Job
        inputDeps.Complete();

        //Make Current = Water Physics Job's Next array
        current.CopyFrom(next);

        //Apply Water Physics
        inputDeps = new ApplyWaterPhysics()
        {
            current = current,
            next = next
        }.Schedule(current.Length, 32);

        inputDeps.Complete();

        //Update Entities
        entityQuery.CopyFromComponentDataArray(next);

        //Clean Native Arrays
        current.Dispose();
        next.Dispose();

        return inputDeps;
    }

    [BurstCompile]
    private struct CalculateWaterPhysics : IJobParallelFor
    { 
        //Calculate water physics and then save them in the next array

        [ReadOnly]
        public NativeArray<CellComponent> current;

        [WriteOnly]
        public NativeArray<CellComponent> next;

        public float MaxLiquid;
        public float MinLiquid;
        //public float deltaTime;
        public float MaxCompression;
        public float MinFlow;
        public float MaxFlow;
        public float FlowSpeed;
        public int GridWidth;

        public void Execute(int index)
        {
            // Validate cell
            if (current[index].Solid) { return; } //Is Solid
            if (current[index].Liquid == 0) { return; } //Empty
            if (current[index].Settled) { return; } //Settled

            if (current[index].Liquid < MinLiquid) //Not enough Water
            {   
                //Set to completely Empty
                next[index] = new CellComponent
                {
                    Solid = current[index].Solid,
                    //SpriteSheetFrame = 0, //Empty Sprite
                    UV = current[index].UV,
                    Matrix = current[index].Matrix,
                    CellSize = current[index].CellSize,
                    xGrid = current[index].xGrid,
                    yGrid = current[index].yGrid,
                    index = current[index].index,
                    WorldPos = current[index].WorldPos,
                    Settled = current[index].Settled,
                    SettleCount = current[index].SettleCount,
                    Liquid = 0,
                    BottomIndex = current[index].BottomIndex,
                    TopIndex = current[index].TopIndex,
                    LeftIndex = current[index].LeftIndex,
                    RightIndex = current[index].RightIndex,
                    BottomLeftIndex = current[index].BottomLeftIndex,
                    TopLeftIndex = current[index].TopLeftIndex,
                    TopRightIndex = current[index].TopRightIndex,
                    BottomRightIndex = current[index].BottomRightIndex,
                    ModifySelf = 0,
                    ModifyBottom = 0,
                    //ModifyTop = 0,
                    ModifyLeft = 0,
                    ModifyRight = 0,
                    ModifyBottomLeft = 0,
                    //ModifyUpLeft = 0,
                    //ModifyUpRight = 0,
                    ModifyBottomRight = 0
                };
                return;
            }

            // Keep track of how much liquid this cell started off with
            float remainingLiquid = current[index].Liquid;
            float flow = 0;
            float modifySelf = 0;
            float modifyBottom = 0;
            float modifyTop = 0;
            float modifyLeft = 0;
            float modifyRight = 0;
            float modifyBottomLeft = 0;
            float modifyTopLeft = 0;
            float modifyTopRight = 0;
            float modifyBottomRight = 0;
            
            // Flow to bottom cell
            if (current[index].BottomIndex != -1) //Has bottom neighbor
            {
                if (current[current[index].BottomIndex].Solid == false) //Bottom neighbor is not solid
                {
                    // Determine rate of flow
                    flow = CalculateVerticalFlowValue(remainingLiquid, current[current[index].BottomIndex].Liquid) - current[current[index].BottomIndex].Liquid;
                    if (current[current[index].BottomIndex].Liquid > 0 && flow > MinFlow)
                        flow *= FlowSpeed;

                    // Constrain flow
                    flow = Mathf.Max(flow, 0);
                    if (flow > Mathf.Min(MaxFlow, current[index].Liquid))
                        flow = Mathf.Min(MaxFlow, current[index].Liquid);

                    // Update temp values
                    if (flow != 0)
                    {
                        remainingLiquid -= flow;
                        modifySelf -= flow;
                        modifyBottom += flow;
                    }
                }
            }

            // Check to ensure we still have liquid in this cell
            if (remainingLiquid < MinLiquid)
            { 
                //Not enough Liquid
                modifySelf -= remainingLiquid;
                next[index] = new CellComponent
                {
                    Solid = current[index].Solid,
                    //SpriteSheetFrame = 0, //Empty
                    UV = current[index].UV,
                    Matrix = current[index].Matrix,
                    CellSize = current[index].CellSize,
                    xGrid = current[index].xGrid,
                    yGrid = current[index].yGrid,
                    index = current[index].index,
                    WorldPos = current[index].WorldPos,
                    Settled = current[index].Settled,
                    SettleCount = current[index].SettleCount,
                    Liquid = current[index].Liquid,
                    BottomIndex = current[index].BottomIndex,
                    TopIndex = current[index].TopIndex,
                    LeftIndex = current[index].LeftIndex,
                    RightIndex = current[index].RightIndex,
                    BottomLeftIndex = current[index].BottomLeftIndex,
                    TopLeftIndex = current[index].TopLeftIndex,
                    TopRightIndex = current[index].TopRightIndex,
                    BottomRightIndex = current[index].BottomRightIndex,
                    ModifySelf = modifySelf,
                    ModifyBottom = modifyBottom,
                    ModifyTop = modifyTop,
                    ModifyLeft = modifyLeft,
                    ModifyRight = modifyRight,
                    ModifyBottomLeft = modifyBottomLeft,
                    //ModifyUpLeft = modifyTopLeft,
                    //ModifyUpRight = modifyTopRight,
                    ModifyBottomRight = modifyBottomRight
                };
                return;
            }
            
            // Flow to bottom left
            if (current[index].BottomLeftIndex != -1) //Has bottom left neighbor
            {
                //Debug.Log($"{index}");

                if (current[current[index].BottomLeftIndex].Solid == false) //Bottom left neighbor is not solid
                {
                    // Determine rate of flow
                    flow = CalculateVerticalFlowValue(remainingLiquid, current[current[index].BottomLeftIndex].Liquid) - current[current[index].BottomLeftIndex].Liquid;

                    flow *= 0.5f;

                    if (current[current[index].BottomLeftIndex].Liquid > 0 && flow > MinFlow)
                        flow *= FlowSpeed;

                    // Constrain flow
                    flow = Mathf.Max(flow, 0);
                    if (flow > Mathf.Min(MaxFlow, current[index].Liquid))
                        flow = Mathf.Min(MaxFlow, current[index].Liquid);

                    // Update temp values
                    if (flow != 0)
                    {
                        remainingLiquid -= flow;
                        modifySelf -= flow;
                        modifyBottomLeft += flow;
                    }
                }
            }

            // Check to ensure we still have liquid in this cell
            if (remainingLiquid < MinLiquid)
            {
                //Not enough Liquid
                modifySelf -= remainingLiquid;
                next[index] = new CellComponent
                {
                    Solid = current[index].Solid,
                    //SpriteSheetFrame = 0, //Empty
                    UV = current[index].UV,
                    Matrix = current[index].Matrix,
                    CellSize = current[index].CellSize,
                    xGrid = current[index].xGrid,
                    yGrid = current[index].yGrid,
                    index = current[index].index,
                    WorldPos = current[index].WorldPos,
                    Settled = current[index].Settled,
                    SettleCount = current[index].SettleCount,
                    Liquid = current[index].Liquid,
                    BottomIndex = current[index].BottomIndex,
                    TopIndex = current[index].TopIndex,
                    LeftIndex = current[index].LeftIndex,
                    RightIndex = current[index].RightIndex,
                    BottomLeftIndex = current[index].BottomLeftIndex,
                    TopLeftIndex = current[index].TopLeftIndex,
                    TopRightIndex = current[index].TopRightIndex,
                    BottomRightIndex = current[index].BottomRightIndex,
                    ModifySelf = modifySelf,
                    ModifyBottom = modifyBottom,
                    //ModifyTop = modifyTop,
                    ModifyLeft = modifyLeft,
                    ModifyRight = modifyRight,
                    ModifyBottomLeft = modifyBottomLeft,
                    //ModifyUpLeft = modifyTopLeft,
                    //ModifyUpRight = modifyTopRight,
                    ModifyBottomRight = modifyBottomRight
                };
                return;
            }
            

            // Flow to bottom right
            if (current[index].BottomRightIndex != -1) //Has bottom left neighbor
            {
                if (current[current[index].BottomRightIndex].Solid == false) //Bottom left neighbor is not solid
                {
                    // Determine rate of flow
                    flow = CalculateVerticalFlowValue(remainingLiquid, current[current[index].BottomRightIndex].Liquid) - current[current[index].BottomRightIndex].Liquid;

                    flow *= 0.5f;

                    if (current[current[index].BottomRightIndex].Liquid > 0 && flow > MinFlow)
                        flow *= FlowSpeed;

                    // Constrain flow
                    flow = Mathf.Max(flow, 0);
                    if (flow > Mathf.Min(MaxFlow, current[index].Liquid))
                        flow = Mathf.Min(MaxFlow, current[index].Liquid);

                    // Update temp values
                    if (flow != 0)
                    {
                        remainingLiquid -= flow;
                        modifySelf -= flow;
                        modifyBottomRight += flow;
                    }
                }
            }

            // Check to ensure we still have liquid in this cell
            if (remainingLiquid < MinLiquid)
            {
                //Not enough Liquid
                modifySelf -= remainingLiquid;
                next[index] = new CellComponent
                {
                    Solid = current[index].Solid,
                    //SpriteSheetFrame = 0, //Empty
                    UV = current[index].UV,
                    Matrix = current[index].Matrix,
                    CellSize = current[index].CellSize,
                    xGrid = current[index].xGrid,
                    yGrid = current[index].yGrid,
                    index = current[index].index,
                    WorldPos = current[index].WorldPos,
                    Settled = current[index].Settled,
                    SettleCount = current[index].SettleCount,
                    Liquid = current[index].Liquid,
                    BottomIndex = current[index].BottomIndex,
                    TopIndex = current[index].TopIndex,
                    LeftIndex = current[index].LeftIndex,
                    RightIndex = current[index].RightIndex,
                    BottomLeftIndex = current[index].BottomLeftIndex,
                    TopLeftIndex = current[index].TopLeftIndex,
                    TopRightIndex = current[index].TopRightIndex,
                    BottomRightIndex = current[index].BottomRightIndex,
                    ModifySelf = modifySelf,
                    ModifyBottom = modifyBottom,
                    //ModifyTop = modifyTop,
                    ModifyLeft = modifyLeft,
                    ModifyRight = modifyRight,
                    ModifyBottomLeft = modifyBottomLeft,
                    //ModifyUpLeft = modifyTopLeft,
                    //ModifyUpRight = modifyTopRight,
                    ModifyBottomRight = modifyBottomRight
                };
                return;
            }
            
            // Flow to left cell
            if (current[index].LeftIndex != -1)
            {
                if (current[current[index].LeftIndex].Solid == false)
                {
                    // Calculate flow rate
                    flow = (remainingLiquid - current[current[index].LeftIndex].Liquid) / 4f;
                    if (flow > MinFlow)
                        flow *= FlowSpeed;

                    // constrain flow
                    flow = Mathf.Max(flow, 0);
                    if (flow > Mathf.Min(MaxFlow, remainingLiquid))
                        flow = Mathf.Min(MaxFlow, remainingLiquid);

                    // Adjust temp values
                    if (flow != 0)
                    {
                        remainingLiquid -= flow;
                        modifySelf -= flow;
                        modifyLeft += flow;
                    }
                }
            }

            // Check to ensure we still have liquid in this cell
            if (remainingLiquid < MinLiquid)
            {
                modifySelf -= remainingLiquid;
                next[index] = new CellComponent
                {
                    Solid = current[index].Solid,
                    //SpriteSheetFrame = 0, //Empty
                    UV = current[index].UV,
                    Matrix = current[index].Matrix,
                    CellSize = current[index].CellSize,
                    xGrid = current[index].xGrid,
                    yGrid = current[index].yGrid,
                    index = current[index].index,
                    WorldPos = current[index].WorldPos,
                    Settled = current[index].Settled,
                    SettleCount = current[index].SettleCount,
                    Liquid = current[index].Liquid,
                    BottomIndex = current[index].BottomIndex,
                    TopIndex = current[index].TopIndex,
                    LeftIndex = current[index].LeftIndex,
                    RightIndex = current[index].RightIndex,
                    BottomLeftIndex = current[index].BottomLeftIndex,
                    TopLeftIndex = current[index].TopLeftIndex,
                    TopRightIndex = current[index].TopRightIndex,
                    BottomRightIndex = current[index].BottomRightIndex,
                    ModifySelf = modifySelf,
                    ModifyBottom = modifyBottom,
                    ModifyTop = modifyTop,
                    ModifyLeft = modifyLeft,
                    ModifyRight = modifyRight,
                    ModifyBottomLeft = modifyBottomLeft,
                    //ModifyUpLeft = modifyTopLeft,
                    //ModifyUpRight = modifyTopRight,
                    ModifyBottomRight = modifyBottomRight
                };
                return;
            }

            //Flow to Right
            if (current[index].RightIndex != -1)
            {
                if (current[current[index].RightIndex].Solid == false)
                {

                    // calc flow rate
                    flow = (remainingLiquid - current[current[index].RightIndex].Liquid) / 3f;
                    if (flow > MinFlow)
                        flow *= FlowSpeed;

                    // constrain flow
                    flow = Mathf.Max(flow, 0);
                    if (flow > Mathf.Min(MaxFlow, remainingLiquid))
                        flow = Mathf.Min(MaxFlow, remainingLiquid);

                    // Adjust temp values
                    if (flow != 0)
                    {
                        remainingLiquid -= flow;
                        modifySelf -= flow;
                        modifyRight += flow;
                    }
                }
            }

            // Check to ensure we still have liquid in this cell
            if (remainingLiquid < MinLiquid)
            {
                modifySelf -= remainingLiquid;
                next[index] = new CellComponent
                {
                    Solid = current[index].Solid,
                    //SpriteSheetFrame = 0, //Empty
                    UV = current[index].UV,
                    Matrix = current[index].Matrix,
                    CellSize = current[index].CellSize,
                    xGrid = current[index].xGrid,
                    yGrid = current[index].yGrid,
                    index = current[index].index,
                    WorldPos = current[index].WorldPos,
                    Settled = current[index].Settled,
                    SettleCount = current[index].SettleCount,
                    Liquid = current[index].Liquid,
                    BottomIndex = current[index].BottomIndex,
                    TopIndex = current[index].TopIndex,
                    LeftIndex = current[index].LeftIndex,
                    RightIndex = current[index].RightIndex,
                    BottomLeftIndex = current[index].BottomLeftIndex,
                    TopLeftIndex = current[index].TopLeftIndex,
                    TopRightIndex = current[index].TopRightIndex,
                    BottomRightIndex = current[index].BottomRightIndex,
                    ModifySelf = modifySelf,
                    ModifyBottom = modifyBottom,
                    ModifyTop = modifyTop,
                    ModifyLeft = modifyLeft,
                    ModifyRight = modifyRight,
                    ModifyBottomLeft = modifyBottomLeft,
                    //ModifyUpLeft = modifyTopLeft,
                    //ModifyUpRight = modifyTopRight,
                    ModifyBottomRight = modifyBottomRight
                };
                return;
            }

            /*
            //Flow to Top Cell
            if (current[index].TopIndex != -1)
            {
                if (current[current[index].TopIndex].Solid == false)
                {

                    flow = remainingLiquid - CalculateVerticalFlowValue(remainingLiquid, current[current[index].TopIndex].Liquid);
                    if (flow > MinFlow)
                        flow *= FlowSpeed;

                    // constrain flow
                    flow = Mathf.Max(flow, 0);
                    if (flow > Mathf.Min(MaxFlow, remainingLiquid))
                        flow = Mathf.Min(MaxFlow, remainingLiquid);

                    // Adjust values
                    if (flow != 0)
                    {
                        remainingLiquid -= flow;
                        modifySelf -= flow;
                        modifyTop += flow;
                    }
                }
            }
            

            // Check to ensure we still have liquid in this cell
            if (remainingLiquid < MinLiquid)
            {
                modifySelf -= remainingLiquid;
                next[index] = new CellComponent
                {
                    Solid = current[index].Solid,
                    SpriteSheetFrame = 0, //Empty
                    UV = current[index].UV,
                    Matrix = current[index].Matrix,
                    CellSize = current[index].CellSize,
                    xGrid = current[index].xGrid,
                    yGrid = current[index].yGrid,
                    index = current[index].index,
                    WorldPos = current[index].WorldPos,
                    Settled = current[index].Settled,
                    SettleCount = current[index].SettleCount,
                    Liquid = current[index].Liquid,
                    BottomIndex = current[index].BottomIndex,
                    TopIndex = current[index].TopIndex,
                    LeftIndex = current[index].LeftIndex,
                    RightIndex = current[index].RightIndex,
                    BottomLeftIndex = current[index].BottomLeftIndex,
                    TopLeftIndex = current[index].TopLeftIndex,
                    TopRightIndex = current[index].TopRightIndex,
                    BottomRightIndex = current[index].BottomRightIndex,
                    ModifySelf = modifySelf,
                    ModifyBottom = modifyBottom,
                    ModifyTop = modifyTop,
                    ModifyLeft = modifyLeft,
                    ModifyRight = modifyRight
                };
                return;
            }
            */
            //Update Cell Changes
            next[index] = new CellComponent
            {
                Solid = current[index].Solid,
                //SpriteSheetFrame = 1, //Water
                UV = current[index].UV,
                Matrix = current[index].Matrix,
                CellSize = current[index].CellSize,
                xGrid = current[index].xGrid,
                yGrid = current[index].yGrid,
                index = current[index].index,
                WorldPos = current[index].WorldPos,
                Settled = current[index].Settled,
                SettleCount = current[index].SettleCount,
                Liquid = current[index].Liquid,
                BottomIndex = current[index].BottomIndex,
                TopIndex = current[index].TopIndex,
                LeftIndex = current[index].LeftIndex,
                RightIndex = current[index].RightIndex,
                BottomLeftIndex = current[index].BottomLeftIndex,
                TopLeftIndex = current[index].TopLeftIndex,
                TopRightIndex = current[index].TopRightIndex,
                BottomRightIndex = current[index].BottomRightIndex,
                ModifySelf = modifySelf,
                ModifyBottom = modifyBottom,
                ModifyTop = modifyTop,
                ModifyLeft = modifyLeft,
                ModifyRight = modifyRight,
                ModifyBottomLeft = modifyBottomLeft,
                //ModifyUpLeft = modifyTopLeft,
                //ModifyUpRight = modifyTopRight,
                ModifyBottomRight = modifyBottomRight
            };
        }

        // Calculate how much liquid should flow to destination with pressure
        float CalculateVerticalFlowValue(float remainingLiquid, float destination)
        {
            float sum = remainingLiquid + destination;
            float value = 0;

            if (sum <= MaxLiquid)
            {
                value = MaxLiquid;
            }
            else if (sum < 2 * MaxLiquid + MaxCompression)
            {
                value = (MaxLiquid * MaxLiquid + sum * MaxCompression) / (MaxLiquid + MaxCompression);
            }
            else
            {
                value = (sum + MaxCompression) / 2f;
            }

            return value;
        }

    }

    [BurstCompile]
    private struct ApplyWaterPhysics : IJobParallelFor
    { //Apply modify values from calculatewaterphysics job

        [ReadOnly]
        public NativeArray<CellComponent> current; //Pre Mods
        [WriteOnly]
        public NativeArray<CellComponent> next; //Applied Mods

        public void Execute(int index)
        {
            if (current[index].Solid) { return; } //Is Solid

            float modifiedLiquid = current[index].Liquid;
            int SettleCount = current[index].SettleCount;
            bool Settled = false;

            //Total Cell modifications
            modifiedLiquid += current[index].ModifySelf; //Take self mods
            //if (current[index].TopIndex != -1) //Add top cell's modify bottom
            //{
                modifiedLiquid += current[current[index].TopIndex].ModifyBottom;
            //}
            //if (current[index].BottomIndex != -1) //Add bottom cell's modify top
            //{
                modifiedLiquid += current[current[index].BottomIndex].ModifyTop;
            //}
            //if (current[index].LeftIndex != -1) //Add left cell's modify right
            //{
                modifiedLiquid += current[current[index].LeftIndex].ModifyRight;
            //}
            //if (current[index].RightIndex != -1) //Add right cell's modify left
            //{
                modifiedLiquid += current[current[index].RightIndex].ModifyLeft;
            //}

            modifiedLiquid += current[current[index].TopLeftIndex].ModifyBottomRight;
            modifiedLiquid += current[current[index].TopRightIndex].ModifyBottomLeft;

            // Check if cell is settled (avoid settling empty cells)
            if (modifiedLiquid == current[index].Liquid && current[index].Liquid != 0)
            { 
                //No liquid changes increment settle counter
                SettleCount++;
                if (SettleCount >= 10)
                { 
                    //Cell has settled
                    Settled = true;
                }
            }
            else
            {
                Settled = false;
                SettleCount = 0;
            }

            //Fill out water cells with cells above them
            //Calculated here and implemented in SpriteSheetCalculations with offsetY
            bool isDownFlowing = false;
            if (current[index].Liquid > 0.005f)
            {
                if (current[index].TopIndex != -1 && current[current[index].TopIndex].Liquid >= 0.005f)
                {
                    isDownFlowing = true;
                }
                else
                {
                    isDownFlowing = false;
                }
            }

            //Assign all new values
            next[index] = new CellComponent
            {
                Solid = current[index].Solid,
                //SpriteSheetFrame = current[index].SpriteSheetFrame,
                UV = current[index].UV,
                Matrix = current[index].Matrix,
                IsDownFlowingLiquid = isDownFlowing,
                CellSize = current[index].CellSize,
                xGrid = current[index].xGrid,
                yGrid = current[index].yGrid,
                index = current[index].index,
                WorldPos = current[index].WorldPos,
                Settled = Settled, //
                SettleCount = SettleCount, //
                Liquid = modifiedLiquid, //
                BottomIndex = current[index].BottomIndex,
                TopIndex = current[index].TopIndex,
                LeftIndex = current[index].LeftIndex,
                RightIndex = current[index].RightIndex,
                BottomLeftIndex = current[index].BottomLeftIndex,
                TopLeftIndex = current[index].TopLeftIndex,
                TopRightIndex = current[index].TopRightIndex,
                BottomRightIndex = current[index].BottomRightIndex,
                ModifySelf = current[index].ModifySelf,
                ModifyBottom = current[index].ModifyBottom,
                ModifyTop = current[index].ModifyTop,
                ModifyLeft = current[index].ModifyLeft,
                ModifyRight = current[index].ModifyRight,
                ModifyBottomLeft = current[index].ModifyBottomLeft,
                ModifyBottomRight = current[index].ModifyBottomRight,
            };
        }
    }
}