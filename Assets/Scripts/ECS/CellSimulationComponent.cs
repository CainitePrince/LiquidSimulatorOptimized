using Unity.Entities;

namespace WaterSimulation
{
    /// <summary>
    /// Component for water simulation
    /// </summary>
    public struct CellSimulationComponent : IComponentData
    {
        public int SettleCount;
        public float Liquid;
        public float ModifySelf;
        public float ModifyBottom;
        public float ModifyTop;
        public float ModifyLeft;
        public float ModifyRight;
        public float ModifyBottomLeft;
        public float ModifyBottomRight;
        //public float ModifyTopRight;
        //public float ModifyTopLeft;
        public bool Solid;
        public bool Settled;
        public bool IsDownFlowingLiquid;
    }
}
