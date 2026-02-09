using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PathMarker {
    public MapLocation location;
    public float G, H, F;
    public GameObject marker;
    public PathMarker parent;

    public PathMarker(MapLocation l, float g, float h, float f, GameObject m, PathMarker p) {
        location = l;
        G = g;
        H = h;
        F = f;
        marker = m;
        parent = p;
    }

    public override bool Equals(object obj) {
        if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            return false;
        else
            return location.Equals(((PathMarker)obj).location);
    }

    public override int GetHashCode() {
        return 0;
    }
}

public class FindPathAStar : MonoBehaviour {

    public Maze maze;
    
    // Prefabs assigned in Inspector
    public GameObject start;
    public GameObject end;
    public GameObject pathP;

    PathMarker startNode;
    PathMarker goalNode;
    PathMarker lastPos;
    bool done = false;
    
    List<PathMarker> open = new List<PathMarker>();
    List<PathMarker> closed = new List<PathMarker>();
    List<PathMarker> finalPath = new List<PathMarker>();

    void RemoveAllMarkers() {
        // Try to remove known tags first (safer and faster). If the tag doesn't
        // exist in the Tag Manager, FindGameObjectsWithTag will throw, so wrap
        // calls in try/catch and fall back to scanning all objects.
        string[] tags = new string[] { "marker", "Player", "Goal" };
        bool didFindByTag = false;

        foreach (string tag in tags) {
            try {
                GameObject[] found = GameObject.FindGameObjectsWithTag(tag);
                if (found != null && found.Length > 0) {
                    didFindByTag = true;
                    for (int i = 0; i < found.Length; i++) {
                        Destroy(found[i]);
                    }
                }
            } catch {
                // Tag not defined: we'll handle in the fallback below.
            }
        }

        if (!didFindByTag) {
            // Fallback: iterate all active GameObjects and check tag string safely.
            GameObject[] all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++) {
                GameObject go = all[i];
                string t = go.tag; // safe to read even if tag isn't configured
                if (t == "marker" || t == "Player" || t == "Goal") {
                    Destroy(go);
                }
            }
        }
    }

    void BeginSearch() {
        // BELANGRIJK: Stop alles wat nog bezig is!
        StopAllCoroutines();
        
        done = false;
        RemoveAllMarkers();

        List<MapLocation> locations = new List<MapLocation>();
        for (int z = 1; z < maze.depth - 1; ++z) {
            for (int x = 1; x < maze.width - 1; ++x) {
                if (maze.map[x, z] != 1) { 
                    locations.Add(new MapLocation(x, z));
                }
            }
        }
        
        locations.Shuffle(); 

        // Make sure there are at least two available locations (start & goal).
        if (locations.Count < 2) {
            Debug.LogError("Not enough free tiles to place Start and Goal.");
            return;
        }

        // Use the first and the last entry from the shuffled list to guarantee
        // they're distinct (very unlikely to be the same when list has >1 items).
        Vector3 startLoc = new Vector3(locations[0].x * maze.scale, 0.5f, locations[0].z * maze.scale);
        startNode = new PathMarker(locations[0], 0, 0, 0,
            Instantiate(start, startLoc, Quaternion.identity), null);
        // Ensure runtime instance carries the expected tag. Wrap in try/catch because
        // assigning an undefined tag will throw an exception in Unity.
        try { startNode.marker.tag = "Player"; } catch { Debug.LogWarning("Tag 'Player' not defined in Tag Manager. Please add it to avoid issues."); }

        // --- FIX: Zorg dat Doel niet te dicht bij Start ligt ---
        // We pakken nu locations[locations.Count - 1] (de allerlaatste in de lijst) als doel.
        // Omdat de lijst geshuffeld is, is dit ook random, maar de kans dat het 'index 0' is, is nul.
        MapLocation endLocData = locations[locations.Count - 1]; 

        Vector3 goalLoc = new Vector3(endLocData.x * maze.scale, 0.5f, endLocData.z * maze.scale);
        goalNode = new PathMarker(endLocData, 0, 0, 0,
            Instantiate(end, goalLoc, Quaternion.identity), null);
        try { goalNode.marker.tag = "Goal"; } catch { Debug.LogWarning("Tag 'Goal' not defined in Tag Manager. Please add it to avoid issues."); }
        // -----------------------------------------------------

        open.Clear();
        closed.Clear();
        finalPath.Clear();

        open.Add(startNode);
        lastPos = startNode;

        StartCoroutine(SearchRoutine());
    }

    IEnumerator SearchRoutine() {
        // Extra veiligheid: wacht 1 frame zodat alles van vorige keer zeker weg is
        yield return null; 

        while (!done) {
             // Als de open lijst leeg is, kunnen we niet verder. Stop.
            if (open.Count == 0) {
                done = true;
                Debug.Log("Geen pad mogelijk (Open list is leeg).");
                yield break; // Stop coroutine
            }

            Search(lastPos);
            yield return new WaitForSeconds(0.05f); 
        }

        // Alleen als het doel echt gevonden is (en niet omdat open leeg was)
        if (lastPos != null && lastPos.Equals(goalNode)) {
            Debug.Log("Doel gevonden! Pad reconstrueren...");
            ReconstructPath();
            StartCoroutine(MovePlayer()); 
        }
    }

    void Search(PathMarker thisNode) {
        if (thisNode.Equals(goalNode)) {
            done = true;
            return;
        }

        foreach (MapLocation dir in maze.directions) {
            MapLocation neighbour = dir + thisNode.location;

            if (maze.map[neighbour.x, neighbour.z] == 1) continue; 
            if (neighbour.x < 1 || neighbour.x >= maze.width || neighbour.z < 1 || neighbour.z >= maze.depth) continue;
            if (IsClosed(neighbour)) continue;

            float g = Vector2.Distance(thisNode.location.ToVector(), neighbour.ToVector()) + thisNode.G;
            float h = Vector2.Distance(neighbour.ToVector(), goalNode.location.ToVector());
            float f = g + h;

            GameObject pathBlock = Instantiate(pathP, new Vector3(neighbour.x * maze.scale, 0.0f, neighbour.z * maze.scale), Quaternion.identity);
            // Tag the temporary search marker; wrap in try/catch to avoid exceptions
            // when the tag is not configured in the project Tag Manager.
            try { pathBlock.tag = "marker"; } catch { /* tag missing: continue without tagging */ }

            if (!UpdateMarker(neighbour, g, h, f, thisNode)) {
                open.Add(new PathMarker(neighbour, g, h, f, pathBlock, thisNode));
            }
        }

        // Strict safety check: if the open list is empty after adding neighbors,
        // the algorithm cannot proceed — end the search gracefully.
        if (open.Count == 0) {
            done = true;
            Debug.Log("No path found (Open List empty).");
            return;
        }

        open = open.OrderBy(p => p.F).ToList();
        PathMarker pm = open.ElementAt(0);
        closed.Add(pm);

        open.RemoveAt(0);
        // Only hide the marker if it's not the instantiated start or goal marker.
        // Hiding the start marker caused the player cube to disappear.
        if (pm.marker != null) {
            bool isStartMarker = (startNode != null && pm.marker == startNode.marker);
            bool isGoalMarker = (goalNode != null && pm.marker == goalNode.marker);
            if (!isStartMarker && !isGoalMarker) {
                pm.marker.SetActive(false);
            }
        }

        lastPos = pm;
    }

    void ReconstructPath() {
        finalPath.Clear();
        PathMarker current = lastPos;

        while (current != null && !current.Equals(startNode)) {
            finalPath.Add(current);
            current = current.parent;
            if(current != null && current.marker != null && !current.Equals(startNode))
                current.marker.SetActive(true); 
        }
        
        finalPath.Reverse(); 
    }

    IEnumerator MovePlayer() {
        foreach (PathMarker step in finalPath) {
            // Check of marker nog bestaat (voor als je snel op P drukt)
            if (startNode != null && startNode.marker != null && step.marker != null) {
                startNode.marker.transform.position = step.marker.transform.position;
            }
            yield return new WaitForSeconds(1.0f); 
        }
    }

    bool UpdateMarker(MapLocation pos, float g, float h, float f, PathMarker prt) {
        for (int i = 0; i < open.Count; i++) {
            PathMarker p = open[i];
            
            if (p.location.Equals(pos)) {
                p.G = g;
                p.H = h;
                p.F = f;
                p.parent = prt;
                return true;
            }
        }
        return false;
    }

    bool IsClosed(MapLocation marker) {
        foreach (PathMarker p in closed) {
            if (p.location.Equals(marker)) return true;
        }
        return false;
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.P)) {
            BeginSearch();
        }
    }
}

public static class Extensions {
    public static void Shuffle<T>(this IList<T> list) {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1) {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}
