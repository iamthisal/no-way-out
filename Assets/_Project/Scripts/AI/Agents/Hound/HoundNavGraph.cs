using System;
using System.Collections.Generic;
using Lockdown.AI.Core.Hound;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    // A sampled waypoint grid for this single-floor museum. Geometry comes from the loaded
    // NavMesh, not hard-coded room coordinates. Every searched edge is checked again at runtime.
    public sealed class HoundNavGraph
    {
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<List<int>> neighbours = new List<List<int>>();
        private readonly List<int> result = new List<int>();
        private readonly float spacing;
        public int NodeCount => points.Count;

        public HoundNavGraph(float spacing) { this.spacing = Mathf.Clamp(spacing, 0.25f, 1f); }

        public bool Build(float floorHeight, NavMeshQueryFilter filter)
        {
            points.Clear();
            neighbours.Clear();
            Vector3[] vertices = NavMesh.CalculateTriangulation().vertices;
            if (vertices.Length == 0) return false;
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(vertices[i]);
            int minX = Mathf.FloorToInt(bounds.min.x / spacing);
            int maxX = Mathf.CeilToInt(bounds.max.x / spacing);
            int minZ = Mathf.FloorToInt(bounds.min.z / spacing);
            int maxZ = Mathf.CeilToInt(bounds.max.z / spacing);
            // Refuse an accidentally enormous/multi-scene graph rather than freeze the game.
            if ((long)(maxX - minX + 1) * (maxZ - minZ + 1) > 50000) return false;
            var cells = new Dictionary<Vector2Int, int>();
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector3 point = new Vector3(x * spacing, floorHeight, z * spacing);
                    if (!NavMesh.SamplePosition(point, out NavMeshHit hit, spacing * 0.45f, filter)
                        || Mathf.Abs(hit.position.y - floorHeight) > 0.25f) continue;
                    cells.Add(new Vector2Int(x, z), AddPoint(hit.position));
                }
            foreach (var cell in cells)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        if (cells.TryGetValue(cell.Key + new Vector2Int(dx, dz), out int next))
                            neighbours[cell.Value].Add(next);
                    }
            return points.Count > 0;
        }

        public bool FindPath(Vector3 start, Vector3 goal, Func<Vector3, Vector3, bool> edgeClear,
            List<Vector3> route, out float cost)
        {
            route.Clear();
            int startId = AddPoint(start);
            int goalId = AddPoint(goal);
            float connectionRadius = spacing * 2.5f;
            for (int i = 0; i < startId; i++)
            {
                if (Vector3.Distance(start, points[i]) <= connectionRadius) Connect(startId, i);
                if (Vector3.Distance(goal, points[i]) <= connectionRadius) Connect(goalId, i);
            }
            Connect(startId, goalId); // Direct route is an option only when the edge is clear.
            bool found = HoundAStar.FindPath(points, neighbours, startId, goalId,
                (a, b) => edgeClear(points[a], points[b]), result, out cost);
            if (found)
                for (int i = 0; i < result.Count; i++) route.Add(points[result[i]]);
            // Endpoint nodes belong to this query only; the museum grid is reused.
            for (int i = 0; i < startId; i++)
            {
                neighbours[i].Remove(startId);
                neighbours[i].Remove(goalId);
            }
            points.RemoveRange(startId, 2);
            neighbours.RemoveRange(startId, 2);
            return found;
        }

        private int AddPoint(Vector3 point)
        {
            points.Add(point);
            neighbours.Add(new List<int>());
            return points.Count - 1;
        }

        private void Connect(int a, int b) { neighbours[a].Add(b); neighbours[b].Add(a); }
    }
}
