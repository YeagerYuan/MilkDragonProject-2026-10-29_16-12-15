using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    public sealed class BuildingNavigation
    {
        readonly BuildingData data;
        readonly List<StairLayout> stairs;
        readonly HashSet<Vector3Int> stairInterior=new HashSet<Vector3Int>();
        static readonly Vector3Int[] Steps={Vector3Int.right,Vector3Int.left,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
        public BuildingNavigation(BuildingData building)
        {
            data=building; stairs=StairLayout.All(data);
            foreach(var stair in stairs)
                for(int f=stair.Floor;f<=stair.Floor+1 && f<data.floors.Count;f++)
                    foreach(var cell in stair.Footprint(f))
                        if(cell!=stair.Bottom && cell!=stair.Top) stairInterior.Add(cell);
        }
        public Vector3Int Cell(Vector3 position) => new Vector3Int(Mathf.RoundToInt(position.x/data.gridSize),
            Mathf.Clamp(Mathf.FloorToInt((position.y+.15f)/data.floorHeight),0,data.floors.Count-1),Mathf.RoundToInt(position.z/data.gridSize));
        public Vector3 Position(Vector3Int cell) => new Vector3(cell.x*data.gridSize,cell.y*data.floorHeight,cell.z*data.gridSize);
        public bool Walkable(Vector3Int cell)
        {
            if(cell.y<0 || cell.y>=data.floors.Count || !data.ContainsCell(cell.x,cell.z) || stairInterior.Contains(cell)) return false;
            var item=data.At(cell.y,cell.x,cell.z);
            return item==null || item.type==ItemType.Door || item.type==ItemType.Stairs;
        }
        public StairLayout StairBetween(Vector3Int from,Vector3Int to) => stairs.FirstOrDefault(s=>
            s.Floor+1<data.floors.Count && (s.Bottom==from && s.Top==to || s.Top==from && s.Bottom==to));
        public Vector3Int NearestWalkable(Vector3Int cell)
        {
            if(Walkable(cell)) return cell;
            for(int radius=1;radius<=data.halfWidth+data.halfDepth;radius++)
                for(int dx=-radius;dx<=radius;dx++)
                {
                    int dz=radius-Mathf.Abs(dx);
                    var candidate=new Vector3Int(cell.x+dx,cell.y,cell.z+dz);
                    if(Walkable(candidate)) return candidate;
                    candidate.z=cell.z-dz;
                    if(Walkable(candidate)) return candidate;
                }
            return cell;
        }
        IEnumerable<Vector3Int> Neighbors(Vector3Int cell)
        {
            foreach(var step in Steps) if(Walkable(cell+step)) yield return cell+step;
            foreach(var stair in stairs)
            {
                if(stair.Floor+1>=data.floors.Count) continue;
                if(cell==stair.Bottom) yield return stair.Top;
                else if(cell==stair.Top) yield return stair.Bottom;
            }
        }
        public List<Vector3Int> FindPath(Vector3 startPosition,Vector3 endPosition)
        {
            var start=NearestWalkable(Cell(startPosition));
            var end=NearestWalkable(Cell(endPosition));
            var queue=new Queue<Vector3Int>(); var parent=new Dictionary<Vector3Int,Vector3Int>();
            if(!Walkable(start) || !Walkable(end)) return null;
            queue.Enqueue(start); parent[start]=start;
            while(queue.Count>0)
            {
                var current=queue.Dequeue();
                if(current==end)
                {
                    var result=new List<Vector3Int>{end};
                    while(current!=start) { current=parent[current]; result.Add(current); }
                    result.Reverse(); return result;
                }
                foreach(var next in Neighbors(current))
                { if(parent.ContainsKey(next)) continue; parent[next]=current; queue.Enqueue(next); }
            }
            return null;
        }
        public HashSet<Vector3Int> ReachableFrom(Vector3 startPosition)
        {
            var start=NearestWalkable(Cell(startPosition));
            var visited=new HashSet<Vector3Int>(); var queue=new Queue<Vector3Int>();
            if(!Walkable(start)) return visited;
            visited.Add(start); queue.Enqueue(start);
            while(queue.Count>0)
                foreach(var next in Neighbors(queue.Dequeue())) if(visited.Add(next)) queue.Enqueue(next);
            return visited;
        }
    }
}
