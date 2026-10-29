using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    // One anchor on the lower floor owns both flights, the mid landing and the opening above.
    public sealed class StairLayout
    {
        public readonly BuildingData Building;
        public readonly PlacedItemData Item;
        public readonly int Floor;
        public int FlightCells => Mathf.Max(2,Mathf.CeilToInt(Building.floorHeight*.5f/(Building.gridSize*.65f)));
        public int DepthCells => FlightCells+2;
        public float Run => FlightCells*Building.gridSize;
        public float Height => Building.floorHeight;
        public Vector3 Origin => Building.Position(Floor,Item);
        public Quaternion Rotation => Quaternion.Euler(0,Item.rotationY,0);
        public Vector3Int Bottom => Cell(0,0,Floor);
        public Vector3Int Top => Cell(1,0,Floor+1);

        public StairLayout(BuildingData building,int floor,PlacedItemData item)
        { Building=building; Floor=floor; Item=item; }

        public Vector3Int Cell(int x,int z,int floor)
        {
            var offset=Rotation*new Vector3(x,0,z);
            return new Vector3Int(Item.gridX+Mathf.RoundToInt(offset.x),floor,Item.gridZ+Mathf.RoundToInt(offset.z));
        }
        public Vector3 WorldPoint(float x,float y,float z) => Origin+Rotation*new Vector3(x,y,z);
        public IEnumerable<Vector3Int> Footprint(int floor)
        {
            for(int x=0;x<2;x++) for(int z=0;z<DepthCells;z++) yield return Cell(x,z,floor);
        }
        public IEnumerable<Vector3Int> Opening()
        {
            for(int x=0;x<2;x++) for(int z=1;z<DepthCells;z++) yield return Cell(x,z,Floor+1);
            // The staircase supplies this landing; avoid a second coplanar floor collider/material.
            yield return Top;
        }
        public bool ContainsCell(int floor,int x,int z) => (floor==Floor || floor==Floor+1) &&
            Footprint(floor).Any(c=>c.x==x && c.z==z);
        public bool IsOpening(int floor,int x,int z) => floor==Floor+1 && Opening().Any(c=>c.x==x && c.z==z);

        public List<Vector3> Waypoints(bool ascending)
        {
            float g=Building.gridSize;
            var points=new List<Vector3>{WorldPoint(0,0,0),WorldPoint(0,0,g*.5f)};
            int steps=Mathf.CeilToInt(Height*.5f/.18f);
            for(int n=1;n<=steps;n++) points.Add(WorldPoint(0,Height*.5f*n/steps,g*.5f+Run*(n-.5f)/steps));
            points.Add(WorldPoint(0,Height*.5f,(FlightCells+1)*g));
            points.Add(WorldPoint(g,Height*.5f,(FlightCells+1)*g));
            for(int n=1;n<=steps;n++) points.Add(WorldPoint(g,Height*.5f+Height*.5f*n/steps,(FlightCells+.5f)*g-Run*(n-.5f)/steps));
            points.Add(WorldPoint(g,Height,g*.5f));
            points.Add(WorldPoint(g,Height,0));
            if(!ascending) points.Reverse();
            return points;
        }
        public static List<StairLayout> All(BuildingData data)
        {
            var result=new List<StairLayout>();
            foreach(var floor in data.floors)
                foreach(var item in floor.items)
                    if(item.type==ItemType.Stairs) result.Add(new StairLayout(data,floor.floorIndex,item));
            return result;
        }
        public static void Validate(BuildingData data)
        {
            var reserved=new Dictionary<Vector3Int,string>();
            foreach(var stair in All(data))
            {
                for(int f=stair.Floor;f<=stair.Floor+1 && f<data.floors.Count;f++)
                    foreach(var cell in stair.Footprint(f))
                    {
                        if(!data.ContainsCell(cell.x,cell.z))
                            throw new ArgumentException("第 "+(stair.Floor+1)+" 层楼梯超出边界；当前需要 2 × "+stair.DepthCells+" 格。");
                        var occupied=data.At(f,cell.x,cell.z);
                        if(occupied!=null && occupied.id!=stair.Item.id)
                            throw new ArgumentException("楼梯与第 "+(f+1)+" 层 ("+cell.x+", "+cell.z+") 的 "+occupied.type+" 重叠，请移动物品或楼梯。");
                        if(reserved.TryGetValue(cell,out string other) && other!=stair.Item.id)
                            throw new ArgumentException("两座楼梯的占地或上层洞口重叠。");
                        reserved[cell]=stair.Item.id;
                    }
            }
        }

        public void Build(Transform root,PrototypeVisuals palette,bool ghost)
        {
            float g=Building.gridSize,half=Height*.5f,width=g-.12f;
            Color stone=PrototypeVisuals.ItemColors[(int)ItemType.Stairs];
            Color rail=new Color(.26f,.32f,.40f);
            GameObject Box(string name,Vector3 p,Vector3 size,Color c,bool solid=true) =>
                palette.Box(root,name,p,size,c,"UA_Props",solid,ghost);
            int steps=Mathf.CeilToInt(half/.18f);
            float tread=Run/steps;
            for(int n=0;n<steps;n++)
            {
                float rise=half*(n+1)/steps;
                float lowerZ=g*.5f+(n+.5f)*tread, upperZ=(FlightCells+.5f)*g-(n+.5f)*tread;
                Box("LowerStep_"+n,new Vector3(0,rise*.5f,lowerZ),new Vector3(width,rise,tread+.002f),stone);
                Box("UpperStep_"+n,new Vector3(g,(half+rise)*.5f,upperZ),new Vector3(width,half+rise,tread+.002f),stone);
                // Continuous side guards keep both actors on the flights.
                foreach(float edge in new[]{-1f,1f})
                {
                    Box("LowerGuard",new Vector3(edge*(g*.5f-.035f),rise+.45f,lowerZ),new Vector3(.07f,.9f,tread+.005f),rail);
                    Box("UpperGuard",new Vector3(g+edge*(g*.5f-.035f),half+rise+.45f,upperZ),new Vector3(.07f,.9f,tread+.005f),rail);
                }
            }
            Box("MiddleLanding",new Vector3(g*.5f,half-.12f,(FlightCells+1)*g),new Vector3(g*2,.24f,g),stone);
            Box("MiddleBackGuard",new Vector3(g*.5f,half+.45f,(FlightCells+1.5f)*g-.035f),new Vector3(g*2,.9f,.07f),rail);
            Box("MiddleLeftGuard",new Vector3(-g*.5f+.035f,half+.45f,(FlightCells+1)*g),new Vector3(.07f,.9f,g),rail);
            Box("MiddleRightGuard",new Vector3(g*1.5f-.035f,half+.45f,(FlightCells+1)*g),new Vector3(.07f,.9f,g),rail);
            Box("UpperLanding",new Vector3(g,Height-.12f,0),new Vector3(g,.24f,g),stone);
            // Guard the first-flight opening from the upper floor; leave the upper exit clear.
            float openingLength=(FlightCells+1)*g,openingCenter=(FlightCells+2)*g*.5f;
            Box("OpeningLeftGuard",new Vector3(-g*.5f+.035f,Height+.45f,openingCenter),new Vector3(.07f,.9f,openingLength),rail);
            Box("OpeningRightGuard",new Vector3(g*1.5f-.035f,Height+.45f,openingCenter),new Vector3(.07f,.9f,openingLength),rail);
            Box("OpeningBackGuard",new Vector3(g*.5f,Height+.45f,(FlightCells+1.5f)*g-.035f),new Vector3(g*2,.9f,.07f),rail);
            Box("OpeningGuard",new Vector3(0,Height+.45f,g*.5f-.035f),new Vector3(g,.9f,.07f),rail);
        }
    }
}
