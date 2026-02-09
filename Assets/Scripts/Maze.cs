using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Dit is een simpele structuur om X en Z coördinaten te onthouden
public class MapLocation
{
    public int x;
    public int z;

    public MapLocation(int _x, int _z)
    {
        x = _x;
        z = _z;
    }

    public Vector2 ToVector()
    {
        return new Vector2(x, z);
    }

    public static MapLocation operator +(MapLocation a, MapLocation b)
        => new MapLocation(a.x + b.x, a.z + b.z);

    public override bool Equals(object obj)
    {
        if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            return false;
        else
            return x == ((MapLocation)obj).x && z == ((MapLocation)obj).z;
    }

    public override int GetHashCode()
    {
        return 0;
    }
}

public class Maze : MonoBehaviour
{
    public List<MapLocation> directions = new List<MapLocation>() {
        new MapLocation(1,0),
        new MapLocation(0,1),
        new MapLocation(-1,0),
        new MapLocation(0,-1) };

    public int width = 10; 
    public int depth = 10; 
    public byte[,] map;
    public int scale = 1;

    void Start()
    {
        InitialiseMap();
        Generate();
        DrawMap();
    }

    void InitialiseMap()
    {
        map = new byte[width,depth];
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            map[x, z] = 0; // 0 = gang
        }
    }

    public virtual void Generate()
    {
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
           // Hier maken we een muurtje op rij 3
           if(z == 3 && x > 2 && x < 8)
             map[x, z] = 1; // 1 = muur
        }
    }

    void DrawMap()
    {
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            if (map[x, z] == 1) // Als het een muur is, teken een blokje
            {
                Vector3 pos = new Vector3(x * scale, 0, z * scale);
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.localScale = new Vector3(scale, scale, scale);
                wall.transform.position = pos;
            }
        }
    }
}
