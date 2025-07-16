using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Data per grid
public class GridNode
{
    public Vector3Int cellPos;    
    public Vector3 worldPos;     
    public bool walkable;

    public float gCost;
    public float hCost;
    public float fCost => gCost + hCost;
    public GridNode parent;

    public GridNode(Vector3Int cellPos, Vector3 worldPos, bool walkable)
    {
        this.cellPos = cellPos;
        this.worldPos = worldPos;
        this.walkable = walkable;
    }
}
