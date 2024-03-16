using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using UnityEngine.U2D;

namespace WaterSimulation
{
    /*
     * This code is based on https://github.com/BroMayo/unity-dots-ca-watersim 
     * which in turn is based on https://github.com/jongallant/LiquidSimulator
     * 
     * I have added arbitrary gravity, implementing 8-connectivity to make diagonal gravity work.
     * This alters the behaviour of the simulation a bit, water will go through diagonal gaps in walls and will also flow diagonally.
     * I removed water flowing up through pressure. The rendering was also changed to support rotation of water.
     * 
     * I updated the code to a more recent version of Unity, as well as fixed a couple of bugs in the original code.
     * 
     * I have significantly improved the performance of the code by:
     * - removal of unused or unnecessary component data
     * - implementing look up tables for neighbours
     * - splitting up component data for simulation and rendering
     * - avoiding per frame memory allocations
     * - avoiding doing work every frame for data that doesn't change
     * - avoiding bounds checking for neighbours, this is not necessary when the outer border is always solid 
     * 
     * For a grid that is 80x40 the simulation is currently gpu bound, rather than the simulation being the bottleneck.
     * The original DOTS code (first link) was running with a 150x150 grid at roughly 16 ms per frame.
     * The current code can run with a 400x400 grid at roughly 16 ms per frame.
     * 
     * The simulation can probably be made faster by implementing it with compute shaders.
     */

    /// <summary>
    /// Direction of gravity
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
    /// This script can be put on a game object to control the simulation
    /// </summary>
    public class WaterSimulationGrid : MonoBehaviour
    {
        public int GridWidth = 80;
        public int GridHeight = 40;
        public Mesh QuadMesh;
        public Material WaterMaterial;

        // Look up tables for neighbours
        public NativeArray<int> TopIndices;
        public NativeArray<int> BottomIndices;
        public NativeArray<int> LeftIndices;
        public NativeArray<int> RightIndices;
        public NativeArray<int> BottomLeftIndices;
        public NativeArray<int> TopLeftIndices;
        public NativeArray<int> TopRightIndices;
        public NativeArray<int> BottomRightIndices;

        [SerializeField] private int _liquidPerClick = 5;
        //[SerializeField] private GravityEnum _gravity = GravityEnum.Down;
        [SerializeField] private Vector2 _gravity = new(0, 1);

        private Entity[] _cells;
        private bool _fill;
        private EntityManager _entityManager;
        private EntityArchetype _cellArchetype;
        private CellSimulationComponent _clickedCell;
        private readonly float _cellSize = 1.0f;

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

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            _cellArchetype = _entityManager.CreateArchetype(
                typeof(CellSimulationComponent),
                typeof(CellRenderComponent));

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
            FlowRatios.Dispose();
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

        public Vector2 GetGravityVector()
        {
            Vector2 gravity = _gravity;
            gravity.Normalize();
            return gravity;
        }

        /*
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
        */

        int ToOffset(float v)
        {
            if (Mathf.Abs(v) < 0.001f)
            {
                return 0;
            }

            return 1 * (int)Mathf.Sign(v);
        }

        /*
        public struct IntVector
        {
            public IntVector(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public int x;
            public int y;
        }
        */
        //public NativeArray<NativeArray<int>> DownFlowCells;
        //public NativeArray<NativeArray<float>> DownFlowRatios;
        //public NativeArray<NativeArray<int>> LeftFlowCells;
        //public NativeArray<NativeArray<float>> LeftFlowRatios;
        //public NativeArray<NativeArray<int>> RightFlowCells;
        //public NativeArray<NativeArray<float>> RightFlowRatios;

        public NativeArray<float> FlowRatios;

        private void CalculateCellFlowRatios()
        {
            Vector3[] directions = new Vector3[8];
            directions[0] = new Vector3( 0, -1, 0);
            directions[1] = new Vector3( 1, -1, 0);
            directions[2] = new Vector3( 1,  0, 0);
            directions[3] = new Vector3( 1,  1, 0);
            directions[4] = new Vector3( 0,  1, 0);
            directions[5] = new Vector3(-1,  1, 0);
            directions[6] = new Vector3(-1,  0, 0);
            directions[7] = new Vector3(-1, -1, 0);

            Vector2 normalizedGravity2D = GetGravityVector();
            Vector3 normalizedGravity3D = new(normalizedGravity2D.x, normalizedGravity2D.y, 0.0f);

            // World space down, left, and right
            Vector3 wsBottom = new Vector3(normalizedGravity3D.x, normalizedGravity3D.y, 0.0f);
            Vector3 wsRight = Vector3.Cross(wsBottom, new Vector3(0.0f, 0.0f, 1.0f));
            Vector3 wsLeft = -wsRight;

            // Determine general orientation of gravity with regards to the cells
            // We will control the flow more precisely with flow ratios
            Vector2 gravityCellOffset = Vector2.zero; 
            for (int i = 0; i < 8; ++i)
            {
                Vector3 normalizedDirection = Vector3.Normalize(directions[i]);
                float bottomAngle = Vector3.Dot(normalizedGravity3D, normalizedDirection);
                if (bottomAngle > 0.25f)
                {
                    gravityCellOffset = directions[i];
                    break;
                }
            }

            // Calculate cell neighbour offsets with regards to gravity 
            Vector3 csBottom = new(gravityCellOffset.x, gravityCellOffset.y, 0.0f);
            Vector3 csRight = Vector3.Cross(csBottom, new Vector3(0.0f, 0.0f, 1.0f));
            Vector3 csLeft = -csRight;
            Vector3 csBottomLeft = Vector3.Normalize(csBottom + csLeft);
            Vector3 csTopLeft = Vector3.Normalize(-csBottom + csLeft);

            int xLeft = ToOffset(csLeft.x);
            int yLeft = ToOffset(csLeft.y);
            int xBottom = ToOffset(csBottom.x);
            int yBottom = ToOffset(csBottom.y);
            int xBottomLeft = ToOffset(csBottomLeft.x);
            int yBottomLeft = ToOffset(csBottomLeft.y);
            int xTopLeft = ToOffset(csTopLeft.x);
            int yTopLeft = ToOffset(csTopLeft.y);

            //List<IntVector> downFlowCells = new();
            //List<float> downFlowRatios = new();
            //List<IntVector> leftFlowCells = new();
            //List<float> leftFlowRatios = new();
            //List<IntVector> rightFlowCells = new();
            //List<float> rightFlowRatios = new();

            FlowRatios = new NativeArray<float>(5, Allocator.Persistent);

            // Calculate flow ratio for each of the 5 cells that water can flow to

            float angle = Vector3.Dot(Vector3.Normalize(csLeft), wsLeft);
            float ratio = (0.25f - angle) / 0.25f;
            FlowRatios[0] = Mathf.Lerp(0.0f, 2.0f, ratio);
            FlowRatios[4] = 2.0f - FlowRatios[0];

            angle = Vector3.Dot(Vector3.Normalize(csBottom), wsBottom);
            ratio = (0.25f - angle) / 0.25f;
            FlowRatios[2] = Mathf.Lerp(0.33f, 0.66f, ratio);

            angle = Vector3.Dot(Vector3.Normalize(csBottomLeft), wsLeft);
            ratio = (0.25f - angle) / 0.25f;
            FlowRatios[1] = Mathf.Lerp(0.167f, 0.33f, ratio);
            FlowRatios[3] = FlowRatios[1] - 0.167f;

            /*
            for (int i = 0; i < 8; ++i)
            {
                Vector3 normalizedDirection = Vector3.Normalize(directions[i]);

                float bottomAngle = Vector2.Dot(gravity, normalizedDirection);

                if (bottomAngle >= 0.5f)
                {
                    IntVector offset = new((int)directions[i].x, (int)directions[i].y);
                    downFlowCells.Add(offset);
                    float ratio = (0.5f - bottomAngle) / 0.5f;//(fourtyFiveDegrees - angle) / fourtyFiveDegrees;
                    downFlowRatios.Add(Mathf.Lerp(0.125f, 0.75f, ratio));
                }

                float leftAngle = Vector2.Dot(left, normalizedDirection);

                if (leftAngle > 0.5f)
                {
                    if (bottomAngle > 0.0f)
                    {
                        IntVector offset = new((int)directions[i].x, (int)directions[i].y);
                        leftFlowCells.Add(offset);
                        float ratio = (0.5f - leftAngle) / 0.5f;
                        leftFlowRatios.Add(Mathf.Lerp(0.25f, 0.75f, ratio));
                    }
                }

                float rightAngle = Vector2.Dot(right, normalizedDirection);

                if (rightAngle > 0.5f)
                {
                    if (bottomAngle > 0.0f)
                    {
                        IntVector offset = new((int)directions[i].x, (int)directions[i].y);
                        rightFlowCells.Add(offset);
                        float ratio = (0.5f - leftAngle) / 0.5f;
                        rightFlowRatios.Add(Mathf.Lerp(0.25f, 0.75f, ratio));
                    }
                }
            }
            */


        }
        /*
        private float AngleBetweenVectors(ref Vector3 a, ref Vector3 b)
        {
            float dot = Vector2.Dot(a, b);
            float det = a.x * b.y - a.y * b.x;

            float angle = Mathf.Atan2(det, dot);

            if (angle < 0.0f) angle += Mathf.PI * 2.0f;
            
            return angle;
        }
        */
        private void CreateGrid()
        {
            //CalculateCellFlowRatios();

            Vector3[] directions = new Vector3[8];
            directions[0] = new Vector3(0, -1, 0);
            directions[1] = new Vector3(1, -1, 0);
            directions[2] = new Vector3(1, 0, 0);
            directions[3] = new Vector3(1, 1, 0);
            directions[4] = new Vector3(0, 1, 0);
            directions[5] = new Vector3(-1, 1, 0);
            directions[6] = new Vector3(-1, 0, 0);
            directions[7] = new Vector3(-1, -1, 0);

            Vector2 normalizedGravity2D = GetGravityVector();
            Vector3 normalizedGravity3D = new(normalizedGravity2D.x, normalizedGravity2D.y, 0.0f);

            // World space down, left, and right
            Vector3 wsBottom = new(normalizedGravity3D.x, normalizedGravity3D.y, 0.0f);
            Vector3 wsRight = Vector3.Cross(wsBottom, new Vector3(0.0f, 0.0f, 1.0f));
            Vector3 wsLeft = -wsRight;

            // Determine general orientation of gravity with regards to the cells
            // We will control the flow more precisely with flow ratios
            //Vector2 gravityCellOffset = Vector2.zero;
            float maxAngle = -1.0f;
            int indexWithMaxAngle = 0;
            for (int i = 0; i < 8; ++i)
            {
                Vector3 normalizedDirection = Vector3.Normalize(directions[i]);
                float bottomAngle = Vector3.Dot(normalizedGravity3D, normalizedDirection);
                
                if (bottomAngle > maxAngle)
                {
                    maxAngle = bottomAngle;
                    indexWithMaxAngle = i;
                }
                //if (bottomAngle >= 0.75f)
                //{
                //    gravityCellOffset = directions[i];
                //    break;
                //}
            }
            Vector2 gravityCellOffset = directions[indexWithMaxAngle];

            // Calculate cell neighbour offsets with regards to gravity 
            Vector3 csBottom = new (gravityCellOffset.x, gravityCellOffset.y, 0.0f);
            Vector3 csRight = Vector3.Cross(csBottom, new Vector3(0.0f, 0.0f, 1.0f));
            Vector3 csLeft = -csRight;
            Vector3 csBottomLeft = Vector3.Normalize(csBottom + csLeft);
            Vector3 csTopLeft = Vector3.Normalize(-csBottom + csLeft);

            int xLeft = ToOffset(csLeft.x);
            int yLeft = ToOffset(csLeft.y);
            int xBottom = ToOffset(csBottom.x);
            int yBottom = ToOffset(csBottom.y);
            int xBottomLeft = ToOffset(csBottomLeft.x);
            int yBottomLeft = ToOffset(csBottomLeft.y);
            int xTopLeft = ToOffset(csTopLeft.x);
            int yTopLeft = ToOffset(csTopLeft.y);

            //List<IntVector> downFlowCells = new();
            //List<float> downFlowRatios = new();
            //List<IntVector> leftFlowCells = new();
            //List<float> leftFlowRatios = new();
            //List<IntVector> rightFlowCells = new();
            //List<float> rightFlowRatios = new();

            FlowRatios = new NativeArray<float>(5, Allocator.Persistent);

            // Calculate flow ratio for each of the 5 cells that water can flow to

            // When angle is negative flow more to the left, when angle is positive flow more to the right
            float angle = Vector3.Dot(Vector3.Normalize(csBottom), wsLeft);
            
            // 0 is left, 1 is right, 0.5 is center
            float ratio = Mathf.Clamp01((angle + 0.25f) * 2.0f); 
            
            // Left cell flow ratio
            FlowRatios[0] = Mathf.Lerp(1.5f, 0.5f, ratio);
            
            // Right cell flow ratio
            FlowRatios[4] = Mathf.Lerp(0.5f, 1.5f, ratio);

            FlowRatios[1] = Mathf.Lerp(0.33f, 0.167f, ratio);
            FlowRatios[3] = Mathf.Lerp(0.167f, 0.33f, ratio);

            // Bottom cell flow ratio
            angle = Vector3.Dot(Vector3.Normalize(csBottom), wsBottom);
            ratio = (angle - 0.75f) * 4.0f;
            FlowRatios[2] = Mathf.Lerp(0.33f, 0.66f, ratio);

            //angle = Vector3.Dot(Vector3.Normalize(csBottomLeft), wsLeft);
            //ratio = (0.25f - angle) / 0.25f;

            /*
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
            */
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

                    // World position of cell
                    float xpos = offset.x + (float)(x * _cellSize);
                    float ypos = offset.y - (float)(y * _cellSize);

                    // Neighbour indices
                    TopIndices[index] = CalculateCellIndex(x - xBottom, y - yBottom);
                    LeftIndices[index] = CalculateCellIndex(x + xLeft, y + yLeft);
                    RightIndices[index] = CalculateCellIndex(x - xLeft, y - yLeft);
                    BottomIndices[index] = CalculateCellIndex(x + xBottom, y + yBottom);
                    BottomLeftIndices[index] = CalculateCellIndex(x + xBottomLeft, y + yBottomLeft);
                    TopLeftIndices[index] = CalculateCellIndex(x + xTopLeft, y + yTopLeft);
                    TopRightIndices[index] = CalculateCellIndex(x - xBottomLeft, y - yBottomLeft);
                    BottomRightIndices[index] = CalculateCellIndex(x - xTopLeft, y - yTopLeft);

                    //TopIndices[index] = CalculateCellIndex(x, y-1);
                    //LeftIndices[index] = CalculateCellIndex(x-1, y);
                    //RightIndices[index] = CalculateCellIndex(x+1, y);
                    //BottomIndices[index] = CalculateCellIndex(x, y+1);
                    //BottomLeftIndices[index] = CalculateCellIndex(x-1, y+1);
                    //TopLeftIndices[index] = CalculateCellIndex(x-1, y-1);
                    //TopRightIndices[index] = CalculateCellIndex(x+1, y-1);
                    //BottomRightIndices[index] = CalculateCellIndex(x+1, y+1);

                    _entityManager.SetComponentData(cell, new CellSimulationComponent
                    {
                        Solid = isWall,
                        Liquid = 0f,
                        Settled = false,
                    });

                    _entityManager.SetComponentData(cell, new CellRenderComponent 
                    {
                        Matrix = Matrix4x4.TRS(new Vector3(xpos, ypos, 0), Quaternion.identity, new Vector3(1.0f, 1.0f, 0.0f))
                    });

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
