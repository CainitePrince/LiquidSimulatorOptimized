using System;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

namespace WaterSimulation
{
    /// <summary>
    /// 
    /// </summary>
    public enum GravityEnum
    {
        Down,
        Left,
        Up,
        Right,
        DownLeft,
        UpLeft,
        UpRight,
        DownRight
    }

    /// <summary>
    /// 
    /// </summary>
    public class WaterSimulationGrid : MonoBehaviour
    {
        public int GridWidth = 80;
        public int GridHeight = 40;
        public Mesh QuadMesh;
        public Material WaterMaterial;
        public NativeArray<int> TopIndices;
        public NativeArray<int> BottomIndices;
        public NativeArray<int> LeftIndices;
        public NativeArray<int> RightIndices;
        public NativeArray<int> BottomLeftIndices;
        public NativeArray<int> TopLeftIndices;
        public NativeArray<int> TopRightIndices;
        public NativeArray<int> BottomRightIndices;

        [SerializeField] private int _liquidPerClick = 5;
        [SerializeField] private GravityEnum _gravity = GravityEnum.Down;

        private Entity[] _cells;
        private bool _fill;
        private EntityManager _entityManager;
        private EntityArchetype _cellArchetype;
        private CellSimulationComponent _clickedCell;
        private float _cellSize = 1.0f;

        private static WaterSimulationGrid _instance;

        public static WaterSimulationGrid GetInstance()
        {
            return _instance;
        }

        void Awake()
        {
            _instance = this;

            float screenRatio = (float)Screen.width / (float)Screen.height;
            float targetRatio = ((float)GridWidth * _cellSize) / ((float)GridHeight * _cellSize);

            if (screenRatio >= targetRatio)
            {
                Camera.main.orthographicSize = ((float)GridHeight * _cellSize) / 2;
            }
            else
            {
                float differenceInSize = targetRatio / screenRatio;
                Camera.main.orthographicSize = ((float)GridHeight * _cellSize) / 2 * differenceInSize;
            }

            //Grab Entity Manager
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            //Cell ArchType
            _cellArchetype = _entityManager.CreateArchetype(
                typeof(LocalToWorld),
                typeof(CellSimulationComponent),
                typeof(CellRenderComponent));

            // Generate our grid
            CreateGrid();
        }

        private void OnDestroy()
        {
            TopIndices.Dispose();
            BottomIndices.Dispose();
            LeftIndices.Dispose();
            RightIndices.Dispose();
            BottomLeftIndices.Dispose();
            TopLeftIndices.Dispose();
            TopRightIndices.Dispose();
            BottomRightIndices.Dispose();
        }

        void Update()
        {
            // Convert mouse position to Grid Coordinates
            Vector2 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            int x = (int)((pos.x - (this.transform.position.x - (GridWidth * _cellSize / 2))));
            int y = -(int)((pos.y - (this.transform.position.y + (GridHeight * _cellSize / 2) + _cellSize)));

            // Check if we clicked outside of the grid
            if (x < 0 || y < 0 || x >= GridWidth || y >= GridHeight)
            {
                return;
            }

            // Check if we are filling or erasing walls
            if (Input.GetMouseButtonDown(0))
            {
                if ((x > 0 && x < GridWidth) && (y > 0 && y < GridHeight))
                {
                    //Click is inside grid, grab cell component data
                    _clickedCell = _entityManager.GetComponentData<CellSimulationComponent>(_cells[CalculateCellIndex(x, y)]);
                    if (!_clickedCell.Solid)
                    {
                        _fill = true;
                    }
                    else
                    {
                        _fill = false;
                    }
                }
            }

            // Left click draws/erases walls
            if (Input.GetMouseButton(0))
            {
                if (x != 0 && y != 0 && x != GridWidth - 1 && y != GridHeight - 1)
                {
                    if ((x > 0 && x < GridWidth) && (y > 0 && y < GridHeight))
                    {
                        _clickedCell = _entityManager.GetComponentData<CellSimulationComponent>(_cells[CalculateCellIndex(x, y)]);
                        if (_fill)
                        {
                            _clickedCell.Solid = true;
                            _clickedCell.Liquid = 0;
                            _entityManager.SetComponentData(_cells[CalculateCellIndex(x, y)], _clickedCell);
                        }
                        else
                        {
                            _clickedCell.Solid = false;
                            _clickedCell.Liquid = 0;
                            _entityManager.SetComponentData(_cells[CalculateCellIndex(x, y)], _clickedCell);
                        }
                    }
                }
            }

            // Right click places liquid
            if (Input.GetMouseButton(1))
            {
                _clickedCell = _entityManager.GetComponentData<CellSimulationComponent>(_cells[CalculateCellIndex(x, y)]);
                if ((x > 0 && x < GridWidth - 1) && (y > 0 && y < GridHeight - 1))
                {
                    _clickedCell.Solid = false;
                    _clickedCell.Liquid = _liquidPerClick;
                    _entityManager.SetComponentData(_cells[CalculateCellIndex(x, y)], _clickedCell);
                }
            }
        }

        public Vector2 GetGravityVector(out bool isDiagonal)
        {
            switch (_gravity)
            {
                case GravityEnum.Down:
                    isDiagonal = false;
                    return new Vector2(0.0f, 1.0f);

                case GravityEnum.Left:
                    isDiagonal = false;
                    return new Vector2(-1.0f, 0.0f);

                case GravityEnum.Right:
                    isDiagonal = false;
                    return new Vector2(1.0f, 0.0f);

                case GravityEnum.Up:
                    isDiagonal = false;
                    return new Vector2(0.0f, -1.0f);

                case GravityEnum.DownLeft:
                    isDiagonal = true;
                    return new Vector2(-1.0f, 1.0f);

                case GravityEnum.UpLeft:
                    isDiagonal = true;
                    return new Vector2(-1.0f, -1.0f);

                case GravityEnum.UpRight:
                    isDiagonal = true;
                    return new Vector2(1.0f, -1.0f);

                case GravityEnum.DownRight:
                    isDiagonal = true;
                    return new Vector2(1.0f, 1.0f);

                default:
                    throw new NotImplementedException("Unimplemented gravity direction.");
            }
        }

        int ToOffset(float v)
        {
            if (Mathf.Abs(v) < 0.001f)
            {
                return 0;
            }

            return 1 * (int)Mathf.Sign(v);
        }

        private void CreateGrid()
        {
            Vector2 gravity = GetGravityVector(out var _);

            Vector3 bottom = Vector3.Normalize(new Vector3(gravity.x, gravity.y, 0.0f));
            Vector3 left = -Vector3.Cross(bottom, new Vector3(0.0f, 0.0f, 1.0f));
            Vector3 bottomLeft = Vector3.Normalize(bottom + left);
            Vector3 topLeft = Vector3.Normalize(-bottom + left);

            int xLeft = ToOffset(left.x);
            int yLeft = ToOffset(left.y);
            int xBottom = ToOffset(bottom.x);
            int yBottom = ToOffset(bottom.y);
            int xBottomLeft = ToOffset(bottomLeft.x);
            int yBottomLeft = ToOffset(bottomLeft.y);
            int xTopLeft = ToOffset(topLeft.x);
            int yTopLeft = ToOffset(topLeft.y);

            int cellCount = GridWidth * GridHeight;

            TopIndices = new NativeArray<int>(cellCount, Allocator.Persistent);
            BottomIndices = new NativeArray<int>(cellCount, Allocator.Persistent);
            LeftIndices = new NativeArray<int>(cellCount, Allocator.Persistent);
            RightIndices = new NativeArray<int>(cellCount, Allocator.Persistent);
            BottomLeftIndices = new NativeArray<int>(cellCount, Allocator.Persistent);
            TopLeftIndices = new NativeArray<int>(cellCount, Allocator.Persistent);
            TopRightIndices = new NativeArray<int>(cellCount, Allocator.Persistent);
            BottomRightIndices = new NativeArray<int>(cellCount, Allocator.Persistent);

            //Create Entity TileMap
            _cells = new Entity[GridWidth * GridHeight];

            //Make this object transform center of map
            UnityEngine.Vector3 offset = new UnityEngine.Vector3(
                this.transform.position.x - (((((float)GridWidth * _cellSize)) / 2) - (_cellSize / 2)),
                this.transform.position.y + (((((float)GridHeight * _cellSize)) / 2) + (_cellSize / 2)), 0);

            // Create Tiles
            bool isWall;
            int index;
            for (int y = 0; y < GridHeight; ++y)
            {
                for (int x = 0; x < GridWidth; ++x)
                {
                    isWall = false;
                    index = CalculateCellIndex(x, y);

                    //Create Cell Entity
                    Entity cell = _entityManager.CreateEntity(_cellArchetype);

                    // Border Tiles
                    if (x == 0 || y == 0 || x == GridWidth - 1 || y == GridHeight - 1)
                    {
                        isWall = true;
                    }

                    //Calculate World Pos
                    float xpos = offset.x + (float)(x * _cellSize);
                    float ypos = offset.y - (float)(y * _cellSize);
                    //float3 pos = new float3(xpos, ypos, 0);

                    //Calc Neighbors Indexes
                    int topIndex = CalculateCellIndex(x - xBottom, y - yBottom);
                    TopIndices[index] = topIndex;

                    int leftIndex = CalculateCellIndex(x + xLeft, y + yLeft);
                    LeftIndices[index] = leftIndex;

                    int rightIndex = CalculateCellIndex(x - xLeft, y - yLeft);
                    RightIndices[index] = rightIndex;

                    int bottomIndex = CalculateCellIndex(x + xBottom, y + yBottom);
                    BottomIndices[index] = bottomIndex;

                    int bottomLeftIndex = CalculateCellIndex(x + xBottomLeft, y + yBottomLeft);
                    BottomLeftIndices[index] = bottomLeftIndex;

                    int topLeftIndex = CalculateCellIndex(x + xTopLeft, y + yTopLeft);
                    TopLeftIndices[index] = topLeftIndex;

                    int topRightIndex = CalculateCellIndex(x - xBottomLeft, y - yBottomLeft);
                    TopRightIndices[index] = topRightIndex;

                    int bottomRightIndex = CalculateCellIndex(x - xTopLeft, y - yTopLeft);
                    BottomRightIndices[index] = bottomRightIndex;

                    //Set CellComponent Data
                    _entityManager.SetComponentData(cell, new CellSimulationComponent
                    {
                        Solid = isWall,
                        Liquid = 0f,
                        Settled = false,
                    });

                    _entityManager.SetComponentData(cell, new CellRenderComponent 
                    {
                        WorldPos = new float2(xpos, ypos)
                    });

                    //Add Cell to Array
                    _cells[index] = cell;
                }
            }
        }

        private int CalculateCellIndex(int x, int y)
        {
            if (x < 0 || y < 0 || x >= GridWidth || y >= GridHeight)
            {
                return -1;
            }

            return x + y * GridWidth;
        }
    }
}
