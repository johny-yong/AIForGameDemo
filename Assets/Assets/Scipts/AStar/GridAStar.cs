using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridAStar : MonoBehaviour
{
    public Tilemap tilemap;
    public TilemapCollider2D wallCollider;

    public static GridAStar Instance;

    private void Awake()
    {
        Instance = this;
    }

    public List<Vector3> FindPath(Vector3 startWorld, Vector3 targetWorld)
    {
        Vector3Int startCell = tilemap.WorldToCell(startWorld);
        Vector3Int endCell = tilemap.WorldToCell(targetWorld);

        GridNode startNode = CreateNode(startCell);
        GridNode endNode = CreateNode(endCell);

        if (!startNode.walkable || !endNode.walkable)
            return null;

        var openList = new List<GridNode>();
        var closedSet = new HashSet<Vector3Int>();

        openList.Add(startNode);

        while (openList.Count > 0)
        {
            openList.Sort((a, b) => a.fCost.CompareTo(b.fCost));
            GridNode current = openList[0];

            if (current.cellPos == endCell)
                return RetracePath(startNode, current);

            openList.RemoveAt(0);
            closedSet.Add(current.cellPos);

            foreach (GridNode neighbor in GetNeighbors(current))
            {
                if (!neighbor.walkable || closedSet.Contains(neighbor.cellPos))
                    continue;

                float tentativeG = current.gCost + Vector3Int.Distance(current.cellPos, neighbor.cellPos);

                bool inOpenList = openList.Exists(n => n.cellPos == neighbor.cellPos);
                if (!inOpenList || tentativeG < neighbor.gCost)
                {
                    neighbor.gCost = tentativeG;
                    neighbor.hCost = Vector3Int.Distance(neighbor.cellPos, endNode.cellPos);
                    neighbor.parent = current;

                    if (!inOpenList)
                        openList.Add(neighbor);
                }
            }
        }

        return null; // No path found
    }

    GridNode CreateNode(Vector3Int cellPos)
    {
        Vector3 world = tilemap.GetCellCenterWorld(cellPos);
        bool walkable = !wallCollider.OverlapPoint(world);
        return new GridNode(cellPos, world, walkable);
    }

    List<GridNode> GetNeighbors(GridNode node)
    {
        List<GridNode> neighbors = new List<GridNode>();
        Vector3Int[] directions = new Vector3Int[]
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
        };

        foreach (var dir in directions)
        {
            Vector3Int newCell = node.cellPos + dir;
            neighbors.Add(CreateNode(newCell));
        }

        return neighbors;
    }

    List<Vector3> RetracePath(GridNode start, GridNode end)
    {
        List<Vector3> path = new List<Vector3>();
        GridNode current = end;

        while (current != start)
        {
            path.Add(current.worldPos);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }
}
