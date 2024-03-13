using Unity.Entities;

namespace WaterSimulation
{
    public struct CellSimulationComponent : IComponentData
    {
        //Check is Water is settled
        public int SettleCount;

        public float Liquid;

        //Values stored for modifying self and neighbor
        public float ModifySelf;
        public float ModifyBottom;
        public float ModifyTop;
        public float ModifyLeft;
        public float ModifyRight;
        public float ModifyBottomLeft;
        public float ModifyBottomRight;

        public bool Solid;
        public bool Settled;
        public bool IsDownFlowingLiquid;
    }
}
