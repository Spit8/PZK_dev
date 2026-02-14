using System.Collections.Generic;
using UnityEngine;

public class Generator : MonoBehaviour
{
    [Header("House Settings")]
    public int width = 20;
    public int depth = 20;
    public int minRoomSize = 5;
    public int floorCount = 2;
    public float floorHeight = 3f;

    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject doorPrefab;
    public GameObject windowPrefab;
    public GameObject stairPrefab;

    private HouseData house;

    void Start()
    {
        house = GenerateHouse();
        BuildHouse(house);
    }

    #region ================== GENERATION ==================

    public HouseData GenerateHouse()
    {
        HouseData data = new HouseData();

        for (int i = 0; i < floorCount; i++)
        {
            FloorData floor = GenerateFloor(i);
            data.Floors.Add(floor);
        }

        PlaceStairs(data);

        return data;
    }

    private FloorData GenerateFloor(int level)
    {
        FloorData floor = new FloorData(width, depth, level);

        BSPNode root = new BSPNode(new RectInt(0, 0, width, depth));
        Split(root, minRoomSize);

        floor.Root = root;

        FillRooms(root, floor);
        GenerateWalls(floor);
        PlaceDoorsAndWindows(floor, level);

        return floor;
    }

    #endregion

    #region ================== BSP ==================

    void Split(BSPNode node, int minSize)
    {
        if (node.Bounds.width < minSize * 2 &&
            node.Bounds.height < minSize * 2)
            return;

        bool horizontal = Random.value > 0.5f;

        if (node.Bounds.width > node.Bounds.height)
            horizontal = false;
        if (node.Bounds.height > node.Bounds.width)
            horizontal = true;

        if (horizontal)
        {
            int split = Random.Range(minSize, node.Bounds.height - minSize);
            node.SplitLine = node.Bounds.y + split;
            node.SplitHorizontal = true;

            node.Left = new BSPNode(new RectInt(node.Bounds.x, node.Bounds.y, node.Bounds.width, split));
            node.Right = new BSPNode(new RectInt(node.Bounds.x, node.Bounds.y + split, node.Bounds.width, node.Bounds.height - split));
        }
        else
        {
            int split = Random.Range(minSize, node.Bounds.width - minSize);
            node.SplitLine = node.Bounds.x + split;
            node.SplitHorizontal = false;

            node.Left = new BSPNode(new RectInt(node.Bounds.x, node.Bounds.y, split, node.Bounds.height));
            node.Right = new BSPNode(new RectInt(node.Bounds.x + split, node.Bounds.y, node.Bounds.width - split, node.Bounds.height));
        }

        Split(node.Left, minSize);
        Split(node.Right, minSize);
    }

    void FillRooms(BSPNode node, FloorData floor)
    {
        if (node.IsLeaf)
        {
            node.Room = new RoomData(node.Bounds);
            floor.Rooms.Add(node.Room);

            for (int x = node.Bounds.x; x < node.Bounds.xMax; x++)
                for (int y = node.Bounds.y; y < node.Bounds.yMax; y++)
                    floor.Grid[x, y] = CellType.Floor;
        }
        else
        {
            FillRooms(node.Left, floor);
            FillRooms(node.Right, floor);
        }
    }

    #endregion

    #region ================== WALLS ==================

    void GenerateWalls(FloorData floor)
    {
        foreach (RoomData room in floor.Rooms)
            GenerateRoomWalls(floor, room);
    }

    void GenerateRoomWalls(FloorData floor, RoomData room)
    {
        for (int x = room.Bounds.x; x < room.Bounds.xMax; x++)
        {
            AddWall(floor, new Vector2Int(x, room.Bounds.yMax - 1), Direction.North, room);
            AddWall(floor, new Vector2Int(x, room.Bounds.y), Direction.South, room);
        }

        for (int y = room.Bounds.y; y < room.Bounds.yMax; y++)
        {
            AddWall(floor, new Vector2Int(room.Bounds.xMax - 1, y), Direction.East, room);
            AddWall(floor, new Vector2Int(room.Bounds.x, y), Direction.West, room);
        }
    }

    void AddWall(FloorData floor, Vector2Int cell, Direction dir, RoomData room)
    {
        var key = (cell, dir);

        if (floor.Walls.TryGetValue(key, out WallData existing))
        {
            if (existing.RoomA != room)
                existing.RoomB = room;
        }
        else
        {
            WallData wall = new WallData(cell, dir, room);
            floor.Walls.Add(key, wall);
        }
    }

    #endregion

    #region ================== DOORS & WINDOWS ==================

    void PlaceDoorsAndWindows(FloorData floor, int level)
    {
        PlaceInteriorDoors(floor);

        if (level == 0)
            PlaceExteriorDoors(floor);

        PlaceWindows(floor);
    }

    void PlaceInteriorDoors(FloorData floor)
    {
        foreach (RoomData room in floor.Rooms)
        {
            List<WallData> interior = new List<WallData>();

            foreach (var w in floor.Walls.Values)
                if (!w.IsExterior && (w.RoomA == room || w.RoomB == room) && w.Type == WallType.Normal)
                    interior.Add(w);

            if (interior.Count > 0)
                interior[Random.Range(0, interior.Count)].Type = WallType.Door;
        }
    }

    void PlaceExteriorDoors(FloorData floor)
    {
        List<WallData> exterior = new List<WallData>();

        foreach (var w in floor.Walls.Values)
            if (w.IsExterior && w.Type == WallType.Normal)
                exterior.Add(w);

        int count = Random.Range(1, 3);

        for (int i = 0; i < count && exterior.Count > 0; i++)
        {
            int idx = Random.Range(0, exterior.Count);
            exterior[idx].Type = WallType.Door;
            exterior.RemoveAt(idx);
        }
    }

    void PlaceWindows(FloorData floor)
    {
        foreach (RoomData room in floor.Rooms)
        {
            List<WallData> exterior = new List<WallData>();

            foreach (var w in floor.Walls.Values)
                if (w.IsExterior && (w.RoomA == room || w.RoomB == room) && w.Type == WallType.Normal)
                    exterior.Add(w);

            int count = Random.Range(1, Mathf.Min(3, exterior.Count + 1));

            for (int i = 0; i < count && exterior.Count > 0; i++)
            {
                int idx = Random.Range(0, exterior.Count);
                exterior[idx].Type = WallType.Window;
                exterior.RemoveAt(idx);
            }
        }
    }

    #endregion

    #region ================== STAIRS ==================

    void PlaceStairs(HouseData house)
    {
        if (house.Floors.Count < 2)
            return;

        for (int i = 0; i < house.Floors.Count - 1; i++)
        {
            FloorData lower = house.Floors[i];
            FloorData upper = house.Floors[i + 1];

            RoomData room = lower.Rooms[Random.Range(0, lower.Rooms.Count)];

            Vector2Int pos = new Vector2Int(
                Random.Range(room.Bounds.x, room.Bounds.xMax - 1),
                Random.Range(room.Bounds.y, room.Bounds.yMax - 1));

            for (int dx = 0; dx < 2; dx++)
                for (int dy = 0; dy < 2; dy++)
                {
                    int sx = pos.x + dx;
                    int sy = pos.y + dy;

                    if (lower.IsInside(sx, sy))
                        lower.Grid[sx, sy] = CellType.Stair;

                    if (upper.IsInside(sx, sy))
                        upper.Grid[sx, sy] = CellType.Empty;
                }

            house.Stairs.Add(new StairData(pos, i, i + 1));
        }
    }

    #endregion

    #region ================== BUILD ==================

    void BuildHouse(HouseData house)
    {
        foreach (FloorData floor in house.Floors)
        {
            float yLevel = floor.Level * floorHeight;

            foreach (var cell in floor.EnumerateCells())
            {
                Vector3 pos = new Vector3(cell.x * 2f, yLevel, cell.y * 2f);

                if (floor.Grid[cell.x, cell.y] == CellType.Floor)
                    Instantiate(floorPrefab, pos, Quaternion.identity, transform);

                if (floor.Grid[cell.x, cell.y] == CellType.Stair)
                    Instantiate(stairPrefab, pos, Quaternion.identity, transform);
            }

            foreach (WallData wall in floor.Walls.Values)
                BuildWall(wall, yLevel);
        }
    }

    void BuildWall(WallData wall, float yLevel)
    {
        Vector3 pos;
        Quaternion rot;

        int x = wall.Cell.x;
        int y = wall.Cell.y;

        float X = x * 2f;
        float Y = y * 2f;

        switch (wall.Dir)
        {
            case Direction.North:
                pos = new Vector3(X, yLevel, Y + 1);
                rot = Quaternion.Euler(0, 90, 0);
                break;

            case Direction.South:
                pos = new Vector3(X, yLevel, Y-1);
                rot = Quaternion.Euler(0, 90, 0);
                break;

            case Direction.East:
                pos = new Vector3(X+1, yLevel, Y);
                rot = Quaternion.identity;
                break;

            case Direction.West:
                pos = new Vector3(X-1, yLevel, Y);
                rot = Quaternion.identity;
                break;
            default:
                return;
        }


        GameObject prefab = wallPrefab;
        if (wall.Type == WallType.Door) prefab = doorPrefab;
        if (wall.Type == WallType.Window) prefab = windowPrefab;

        Instantiate(prefab, pos, rot, transform);
    }

    #endregion
}

#region ================== DATA ==================

public class HouseData
{
    public List<FloorData> Floors = new List<FloorData>();
    public List<StairData> Stairs = new List<StairData>();
}

public class FloorData
{
    public int Width;
    public int Depth;
    public int Level;

    public CellType[,] Grid;
    public BSPNode Root;
    public List<RoomData> Rooms = new List<RoomData>();

    public Dictionary<(Vector2Int, Direction), WallData> Walls =
        new Dictionary<(Vector2Int, Direction), WallData>();

    public FloorData(int width, int depth, int level)
    {
        Width = width;
        Depth = depth;
        Level = level;
        Grid = new CellType[width, depth];
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Depth;
    }

    public IEnumerable<Vector2Int> EnumerateCells()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Depth; y++)
                yield return new Vector2Int(x, y);
    }
}

public class BSPNode
{
    public RectInt Bounds;
    public BSPNode Left;
    public BSPNode Right;
    public RoomData Room;

    public int SplitLine;
    public bool SplitHorizontal;

    public bool IsLeaf => Left == null && Right == null;

    public BSPNode(RectInt bounds)
    {
        Bounds = bounds;
    }
}

public class RoomData
{
    public RectInt Bounds;

    public RoomData(RectInt bounds)
    {
        Bounds = bounds;
    }
}

public class StairData
{
    public Vector2Int Position;
    public int FromFloor;
    public int ToFloor;

    public StairData(Vector2Int pos, int from, int to)
    {
        Position = pos;
        FromFloor = from;
        ToFloor = to;
    }
}

public class WallData
{
    public Vector2Int Cell;
    public Direction Dir;
    public RoomData RoomA;
    public RoomData RoomB;
    public WallType Type = WallType.Normal;

    public bool IsExterior => RoomB == null;

    public WallData(Vector2Int cell, Direction dir, RoomData room)
    {
        Cell = cell;
        Dir = dir;
        RoomA = room;
    }
}

public enum CellType
{
    Empty,
    Floor,
    Stair
}

public enum WallType
{
    Normal,
    Door,
    Window
}

public enum Direction
{
    North,
    South,
    East,
    West
}

#endregion
