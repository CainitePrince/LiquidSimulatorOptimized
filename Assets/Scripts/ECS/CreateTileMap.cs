using System;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// 
/// </summary>
public enum GravityEnum
{
    Down,
    DownLeft,
    Up,
    Left,
    Right,

}

/// <summary>
/// 
/// </summary>
public class CreateTileMap : MonoBehaviour
{
    public int GridWidth = 80;
    public int GridHeight = 40;
    public Mesh QuadMesh;
    public Material SpriteSheetMat;

    [SerializeField] private int _liquidPerClick = 5; //Liquid placed when clicked
    [SerializeField] private GravityEnum _gravity = GravityEnum.Down;
    [SerializeField] private float _cellSize = 1;

    private Entity[] _cells;
    private bool _fill;
    private EntityManager _entityManager;
    private EntityArchetype _cellArchetype;
    private CellComponent _clickedCell;

    private static CreateTileMap _instance;

    public static CreateTileMap GetInstance()
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
            typeof(Translation),
            typeof(Rotation),
            typeof(NonUniformScale),
            typeof(CellComponent));

        // Generate our grid
        CreateGrid();
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
                _clickedCell = _entityManager.GetComponentData<CellComponent>(_cells[CalculateCellIndex(x, y, GridWidth)]);
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
                    _clickedCell = _entityManager.GetComponentData<CellComponent>(_cells[CalculateCellIndex(x, y, GridWidth)]);
                    if (_fill)
                    {
                        _clickedCell.Solid = true;
                        _clickedCell.SpriteSheetFrame = 2;
                        _clickedCell.Liquid = 0;
                        _entityManager.SetComponentData(_cells[CalculateCellIndex(x, y, GridWidth)], _clickedCell);
                    }
                    else
                    {
                        _clickedCell.Solid = false;
                        _clickedCell.Liquid = 0;
                        _clickedCell.SpriteSheetFrame = 0;
                        _entityManager.SetComponentData(_cells[CalculateCellIndex(x, y, GridWidth)], _clickedCell);
                    }
                }
            }
        }

        // Right click places liquid
        if (Input.GetMouseButton(1))
        {
            _clickedCell = _entityManager.GetComponentData<CellComponent>(_cells[CalculateCellIndex(x, y, GridWidth)]);
            if ((x > 0 && x < GridWidth - 1) && (y > 0 && y < GridHeight - 1))
            {
                _clickedCell.Solid = false;
                _clickedCell.Liquid = _liquidPerClick;
                _clickedCell.SpriteSheetFrame = 1;
                _entityManager.SetComponentData(_cells[CalculateCellIndex(x, y, GridWidth)], _clickedCell);
            }
        }
    }

    public Vector2 GetGravityVector()
    {
        switch (_gravity)
        {
            case GravityEnum.Down:
                return new Vector2(0.0f, 1.0f);

            case GravityEnum.DownLeft:
                return new Vector2(-1.0f, 1.0f);

            case GravityEnum.Left:
                return new Vector2(-1.0f, 0.0f);

            case GravityEnum.Right:
                return new Vector2(1.0f, 0.0f);

            case GravityEnum.Up:
                return new Vector2(0.0f, -1.0f);

            default:
                throw new NotImplementedException("Unimplemented gravity direction.");
        }
    }

    private void CreateGrid()
    {
        Vector2 gravity = GetGravityVector();
        
        Vector3 bottom = new Vector3(gravity.x, gravity.y, 0.0f);
        Vector3 left = -Vector3.Cross(bottom, new Vector3(0.0f, 0.0f, 1.0f));

        int xLeft = (int)left.x;
        int yLeft = (int)left.y;
        int xBottom = (int)bottom.x;
        int yBottom = (int)bottom.y;

        //Create Entity TileMap
        _cells = new Entity[GridWidth * GridHeight];

        //Make this object transform center of map
        UnityEngine.Vector3 offset = new UnityEngine.Vector3(
            this.transform.position.x - (((((float)GridWidth * _cellSize)) / 2) - (_cellSize/2)),
            this.transform.position.y + (((((float)GridHeight * _cellSize)) / 2) + (_cellSize / 2)), 0);

        // Create Tiles
        bool isWall;
        int index;
        for (int y = 0; y < GridHeight; ++y)
        {
            for (int x = 0; x < GridWidth; ++x)
            {
                isWall = false;
                index = CalculateCellIndex(x, y, GridWidth);

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
                float3 pos = new float3(xpos, ypos, 0);

                //Fill Position Data
                _entityManager.SetComponentData(cell, new Translation
                {
                    Value = pos
                });

                //Calc Neighbors Indexes
                int bottomIndex = -1;
                int topIndex = -1;
                int leftIndex = -1;
                int rightIndex = -1;

                if (index - GridWidth >= 0)
                {
                    topIndex = CalculateCellIndex(x - xBottom, y - yBottom, GridWidth);//(index - GridWidth);  // north
                }

                if (index % GridWidth != 0)
                {
                    leftIndex = CalculateCellIndex(x + xLeft, y + yLeft, GridWidth);//(index - 1);  // west
                }

                if (((index + 1) % GridWidth) != 0)
                {
                    rightIndex = CalculateCellIndex(x -xLeft, y - yLeft, GridWidth);//index + 1;  // east
                }

                if (index + GridWidth < _cells.Length)
                {
                    bottomIndex = CalculateCellIndex(x + xBottom, y + yBottom, GridWidth);//(index + GridWidth);  // south
                }

                if (isWall)
                {
                    //Set CellComponent Data
                    _entityManager.SetComponentData(cell, new CellComponent
                    {
                        xGrid = x,
                        yGrid = y,
                        Solid = true, //Solid
                        SpriteSheetFrame = 2, //Wall Frame
                        WorldPos = new float2(xpos, ypos),
                        CellSize = _cellSize,
                        Liquid = 0f,
                        Settled = false,
                        index = index,
                        LeftIndex = leftIndex,
                        RightIndex = rightIndex,
                        BottomIndex = bottomIndex,
                        TopIndex = topIndex
                    });
                }
                else
                {
                    //Set Empty Cell Data
                    _entityManager.SetComponentData(cell, new CellComponent
                    {
                        xGrid = x,
                        yGrid = y,
                        Solid = false,//NOT Solid
                        SpriteSheetFrame = 0, //Empty Frame
                        WorldPos = new float2(xpos, ypos),
                        CellSize = _cellSize,
                        Liquid = 0f, //Empty
                        Settled = false,
                        index = index,
                        LeftIndex = leftIndex,
                        RightIndex = rightIndex,
                        BottomIndex = bottomIndex,
                        TopIndex = topIndex
                    });
                }

                //Add Cell to Array
                _cells[index] = cell;
            }
        }
    }

    private static int CalculateCellIndex(int x, int y, int gridWidth)
    {
        return x + y * gridWidth;
    }
}
