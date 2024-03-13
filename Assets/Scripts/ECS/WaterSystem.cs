using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;

namespace WaterSimulation
{
    public partial class LiquidSimulator : SystemBase
    {
        private EntityQuery entityQuery;

        protected override void OnUpdate()
        {
            //Get Entity Query of CellComponents
            entityQuery = GetEntityQuery(ComponentType.ReadOnly<CellSimulationComponent>());

            //Create Readable Current Array
            NativeArray<CellSimulationComponent> current = entityQuery.ToComponentDataArray<CellSimulationComponent>(Allocator.TempJob);

            //Create Writable next(Future) Array
            var next = new NativeArray<CellSimulationComponent>(current.Length, Allocator.TempJob);

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

            var tiles = WaterSimulationGrid.GetInstance();

            //Grid Width of Map
            int GridWidth = tiles.GridWidth;

            //Calculate Water Physics
            JobHandle calculateWaterPhysicsHandle = new CalculateWaterPhysics()
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
                TopIndices = tiles.TopIndices,
                LeftIndices = tiles.LeftIndices,
                RightIndices = tiles.RightIndices,
                BottomIndices = tiles.BottomIndices,
                BottomLeftIndices = tiles.BottomLeftIndices,
                TopLeftIndices = tiles.TopLeftIndices,
                TopRightIndices = tiles.TopRightIndices,
                BottomRightIndices = tiles.BottomRightIndices
            }.Schedule(current.Length, 32);

            //Complete Physics Job
            calculateWaterPhysicsHandle.Complete();

            //Make Current = Water Physics Job's Next array
            current.CopyFrom(next);

            //Apply Water Physics
            JobHandle applyWaterPhysicsHandle = new ApplyWaterPhysics()
            {
                current = current,
                next = next,
                TopIndices = tiles.TopIndices,
                LeftIndices = tiles.LeftIndices,
                RightIndices = tiles.RightIndices,
                BottomIndices = tiles.BottomIndices,
                TopLeftIndices = tiles.TopLeftIndices,
                TopRightIndices = tiles.TopRightIndices,
            }.Schedule(current.Length, 32);

            applyWaterPhysicsHandle.Complete();

            //Update Entities
            entityQuery.CopyFromComponentDataArray(next);

            //Clean Native Arrays
            current.Dispose();
            next.Dispose();
        }

        [BurstCompile]
        private struct CalculateWaterPhysics : IJobParallelFor
        {
            //Calculate water physics and then save them in the next array

            [ReadOnly]
            public NativeArray<CellSimulationComponent> current;

            [WriteOnly]
            public NativeArray<CellSimulationComponent> next;

            [ReadOnly] public NativeArray<int> TopIndices;
            [ReadOnly] public NativeArray<int> LeftIndices;
            [ReadOnly] public NativeArray<int> RightIndices;
            [ReadOnly] public NativeArray<int> BottomIndices;
            [ReadOnly] public NativeArray<int> BottomLeftIndices;
            [ReadOnly] public NativeArray<int> TopLeftIndices;
            [ReadOnly] public NativeArray<int> TopRightIndices;
            [ReadOnly] public NativeArray<int> BottomRightIndices;

            public float MaxLiquid;
            public float MinLiquid;
            public float MaxCompression;
            public float MinFlow;
            public float MaxFlow;
            public float FlowSpeed;
            public int GridWidth;

            public void Execute(int index)
            {
                // Validate cell
                if (current[index].Solid) { return; }
                if (current[index].Liquid == 0) { return; }
                if (current[index].Settled) { return; }

                //Not enough Water
                if (current[index].Liquid < MinLiquid) 
                {
                    //Set to completely Empty
                    next[index] = new CellSimulationComponent
                    {
                        Solid = current[index].Solid,
                        Settled = current[index].Settled,
                        SettleCount = current[index].SettleCount,
                        Liquid = 0,
                        ModifySelf = 0,
                        ModifyBottom = 0,
                        ModifyLeft = 0,
                        ModifyRight = 0,
                        ModifyBottomLeft = 0,
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
                float modifyBottomRight = 0;

                int bottomIndex = BottomIndices[index];

                // Flow to bottom cell
                //if (current[index].BottomIndex != -1) //Has bottom neighbor
                {
                    if (current[bottomIndex].Solid == false) //Bottom neighbor is not solid
                    {
                        // Determine rate of flow
                        flow = CalculateVerticalFlowValue(remainingLiquid, current[bottomIndex].Liquid) - current[bottomIndex].Liquid;
                        if (current[bottomIndex].Liquid > 0 && flow > MinFlow)
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
                    next[index] = new CellSimulationComponent
                    {
                        Solid = current[index].Solid,
                        Settled = current[index].Settled,
                        SettleCount = current[index].SettleCount,
                        Liquid = current[index].Liquid,
                        ModifySelf = modifySelf,
                        ModifyBottom = modifyBottom,
                        ModifyTop = modifyTop,
                        ModifyLeft = modifyLeft,
                        ModifyRight = modifyRight,
                        ModifyBottomLeft = modifyBottomLeft,
                        ModifyBottomRight = modifyBottomRight
                    };
                    return;
                }

                int bottomLeftIndex = BottomLeftIndices[index];
                // Flow to bottom left
                //if (current[index].BottomLeftIndex != -1) //Has bottom left neighbor
                {
                    if (current[bottomLeftIndex].Solid == false) //Bottom left neighbor is not solid
                    {
                        // Determine rate of flow
                        flow = CalculateVerticalFlowValue(remainingLiquid, current[bottomLeftIndex].Liquid) - current[bottomLeftIndex].Liquid;

                        flow *= 0.5f;

                        if (current[bottomLeftIndex].Liquid > 0 && flow > MinFlow)
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
                    next[index] = new CellSimulationComponent
                    {
                        Solid = current[index].Solid,
                        //UV = current[index].UV,
                        //Matrix = current[index].Matrix,
                        //WorldPos = current[index].WorldPos,
                        Settled = current[index].Settled,
                        SettleCount = current[index].SettleCount,
                        Liquid = current[index].Liquid,
                        ModifySelf = modifySelf,
                        ModifyBottom = modifyBottom,
                        ModifyLeft = modifyLeft,
                        ModifyRight = modifyRight,
                        ModifyBottomLeft = modifyBottomLeft,
                        ModifyBottomRight = modifyBottomRight
                    };
                    return;
                }

                int bottomRightIndex = BottomRightIndices[index];
                // Flow to bottom right
                //if (current[index].BottomRightIndex != -1) //Has bottom left neighbor
                {
                    if (current[bottomRightIndex].Solid == false) //Bottom left neighbor is not solid
                    {
                        // Determine rate of flow
                        flow = CalculateVerticalFlowValue(remainingLiquid, current[bottomRightIndex].Liquid) - current[bottomRightIndex].Liquid;

                        flow *= 0.5f;

                        if (current[bottomRightIndex].Liquid > 0 && flow > MinFlow)
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
                    next[index] = new CellSimulationComponent
                    {
                        Solid = current[index].Solid,
                        //UV = current[index].UV,
                        //Matrix = current[index].Matrix,
                        //WorldPos = current[index].WorldPos,
                        Settled = current[index].Settled,
                        SettleCount = current[index].SettleCount,
                        Liquid = current[index].Liquid,
                        ModifySelf = modifySelf,
                        ModifyBottom = modifyBottom,
                        ModifyLeft = modifyLeft,
                        ModifyRight = modifyRight,
                        ModifyBottomLeft = modifyBottomLeft,
                        ModifyBottomRight = modifyBottomRight
                    };
                    return;
                }

                int leftIndex = LeftIndices[index];
                // Flow to left cell
                //if (current[index].LeftIndex != -1)
                {
                    if (current[leftIndex].Solid == false)
                    {
                        // Calculate flow rate
                        flow = (remainingLiquid - current[leftIndex].Liquid) / 4f;
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
                    next[index] = new CellSimulationComponent
                    {
                        Solid = current[index].Solid,
                        //UV = current[index].UV,
                        //Matrix = current[index].Matrix,
                        //WorldPos = current[index].WorldPos,
                        Settled = current[index].Settled,
                        SettleCount = current[index].SettleCount,
                        Liquid = current[index].Liquid,
                        ModifySelf = modifySelf,
                        ModifyBottom = modifyBottom,
                        ModifyTop = modifyTop,
                        ModifyLeft = modifyLeft,
                        ModifyRight = modifyRight,
                        ModifyBottomLeft = modifyBottomLeft,
                        ModifyBottomRight = modifyBottomRight
                    };
                    return;
                }

                int rightIndex = RightIndices[index];
                //Flow to Right
                //if (current[index].RightIndex != -1)
                {
                    if (current[rightIndex].Solid == false)
                    {
                        // calc flow rate
                        flow = (remainingLiquid - current[rightIndex].Liquid) / 3f;
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
                    next[index] = new CellSimulationComponent
                    {
                        Solid = current[index].Solid,
                        //UV = current[index].UV,
                        //Matrix = current[index].Matrix,
                        //WorldPos = current[index].WorldPos,
                        Settled = current[index].Settled,
                        SettleCount = current[index].SettleCount,
                        Liquid = current[index].Liquid,
                        ModifySelf = modifySelf,
                        ModifyBottom = modifyBottom,
                        ModifyTop = modifyTop,
                        ModifyLeft = modifyLeft,
                        ModifyRight = modifyRight,
                        ModifyBottomLeft = modifyBottomLeft,
                        ModifyBottomRight = modifyBottomRight
                    };
                    return;
                }

                //Update Cell Changes
                next[index] = new CellSimulationComponent
                {
                    Solid = current[index].Solid,
                    //UV = current[index].UV,
                    //Matrix = current[index].Matrix,
                    //WorldPos = current[index].WorldPos,
                    Settled = current[index].Settled,
                    SettleCount = current[index].SettleCount,
                    Liquid = current[index].Liquid,
                    ModifySelf = modifySelf,
                    ModifyBottom = modifyBottom,
                    ModifyTop = modifyTop,
                    ModifyLeft = modifyLeft,
                    ModifyRight = modifyRight,
                    ModifyBottomLeft = modifyBottomLeft,
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
            public NativeArray<CellSimulationComponent> current; //Pre Mods
            [WriteOnly]
            public NativeArray<CellSimulationComponent> next; //Applied Mods

            [ReadOnly] public NativeArray<int> TopIndices;
            [ReadOnly] public NativeArray<int> LeftIndices;
            [ReadOnly] public NativeArray<int> RightIndices;
            [ReadOnly] public NativeArray<int> BottomIndices;
            [ReadOnly] public NativeArray<int> TopLeftIndices;
            [ReadOnly] public NativeArray<int> TopRightIndices;

            public void Execute(int index)
            {
                if (current[index].Solid) { return; }

                float modifiedLiquid = current[index].Liquid;
                int SettleCount = current[index].SettleCount;
                bool Settled = false;

                int topIndex = TopIndices[index];
                int bottomIndex = BottomIndices[index];
                int leftIndex = LeftIndices[index];
                int rightIndex = RightIndices[index];
                int topLeftIndex = TopLeftIndices[index];
                int topRightIndex = TopRightIndices[index];

                //Total Cell modifications
                modifiedLiquid += current[index].ModifySelf;
                modifiedLiquid += current[topIndex].ModifyBottom;
                modifiedLiquid += current[bottomIndex].ModifyTop;
                modifiedLiquid += current[leftIndex].ModifyRight;
                modifiedLiquid += current[rightIndex].ModifyLeft;
                modifiedLiquid += current[topLeftIndex].ModifyBottomRight;
                modifiedLiquid += current[topRightIndex].ModifyBottomLeft;

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
                bool isDownFlowing = false;
                if (current[index].Liquid > 0.005f)
                {
                    if (topIndex != -1 && current[topIndex].Liquid >= 0.005f)
                    {
                        isDownFlowing = true;
                    }
                    else
                    {
                        isDownFlowing = false;
                    }
                }

                //Assign all new values
                next[index] = new CellSimulationComponent
                {
                    Solid = current[index].Solid,
                    //UV = current[index].UV,
                    //Matrix = current[index].Matrix,
                    IsDownFlowingLiquid = isDownFlowing,
                    //WorldPos = current[index].WorldPos,
                    Settled = Settled,
                    SettleCount = SettleCount,
                    Liquid = modifiedLiquid,
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
}