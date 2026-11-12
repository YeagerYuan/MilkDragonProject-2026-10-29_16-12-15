using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace UnderAvatar
{
    // Legacy marker retained for authored scenes; no floating map boards are spawned.
    public sealed class HouseMapBoard : MonoBehaviour {public int floor;}
    public sealed class HouseMap : MonoBehaviour
    {
        public sealed class Room {public int floor;public Vector2 center;public bool computer;public string name;}
        public readonly List<Room> Rooms=new List<Room>();
        public readonly List<Texture2D> Floors=new List<Texture2D>();
        public int Floor {get;private set;}
        public bool IsOpen => match && match.LocalPhoneOpen && match.LocalPhoneMenuOpen && match.ActivePhonePage==PhonePage.Map;
        public bool InputBlocked => IsOpen;
        MatchSession match;
        const int Tile=28;
        public void Initialize(MatchSession session)
        {match=session;for(int f=0;f<match.World.Data.floors.Count;f++) Floors.Add(BuildFloor(match.World.Data,f));}
        Texture2D BuildFloor(BuildingData map,int floor)
        {
            var texture=new Texture2D(map.WidthCells*Tile,map.DepthCells*Tile,TextureFormat.RGB24,false){name="房屋结构 "+(floor+1)+"F",filterMode=FilterMode.Point};
            var items=map.floors[floor].items.ToDictionary(i=>new Vector2Int(i.gridX,i.gridZ));
            var rooms=new Dictionary<Vector2Int,int>();int number=0;
            for(int z=-map.halfDepth;z<=map.halfDepth;z++) for(int x=-map.halfWidth;x<=map.halfWidth;x++)
            {
                var cell=new Vector2Int(x,z);if(rooms.ContainsKey(cell) || Boundary(cell)) continue;
                var found=new List<Vector2Int>();var queue=new Queue<Vector2Int>();queue.Enqueue(cell);rooms[cell]=number;
                while(queue.Count>0)
                {
                    var c=queue.Dequeue();found.Add(c);
                    foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                    {var n=c+d;if(!map.ContainsCell(n.x,n.y) || rooms.ContainsKey(n) || Boundary(n)) continue;rooms[n]=number;queue.Enqueue(n);}
                }
                Vector2 mean=found.Aggregate(Vector2.zero,(sum,c)=>sum+(Vector2)c)/found.Count;
                Vector2 center=found.OrderBy(c=>Vector2.Distance(c,mean)).First();
                bool pc=found.Any(c=>items.TryGetValue(c,out var i) && i.type==ItemType.StreamerPC);
                Rooms.Add(new Room{floor=floor,center=center,computer=pc,name=pc?"主播电脑房":"区域 "+(number+1)});number++;
            }
            bool Boundary(Vector2Int c) => items.TryGetValue(c,out var item) && (item.type==ItemType.Wall || item.type==ItemType.Door);
            var stairs=StairLayout.All(map);
            var paper=new Color(.94f,.93f,.88f);var wall=new Color(.08f,.09f,.10f);
            for(int z=-map.halfDepth;z<=map.halfDepth;z++) for(int x=-map.halfWidth;x<=map.halfWidth;x++)
            {
                items.TryGetValue(new Vector2Int(x,z),out var item);
                Color color=item!=null && item.type==ItemType.Wall?wall:paper;
                if(stairs.Any(stair=>stair.ContainsCell(floor,x,z))) color=new Color(.51f,.73f,.88f);
                for(int py=0;py<Tile;py++) for(int px=0;px<Tile;px++)
                {
                    bool step=color.b>color.r+.2f && py%6==0;
                    texture.SetPixel((x+map.halfWidth)*Tile+px,(z+map.halfDepth)*Tile+py,step?new Color(.15f,.35f,.5f):color);
                }
            }
            void Rectangle(Vector3 center,Vector3 size,float yaw,Color fill,bool outline)
            {
                var rot=Quaternion.Euler(0,-yaw,0);int radius=Mathf.CeilToInt(Mathf.Max(size.x,size.z)*Tile/map.gridSize);
                int cx=Mathf.RoundToInt((center.x/map.gridSize+map.halfWidth+.5f)*Tile),cz=Mathf.RoundToInt((center.z/map.gridSize+map.halfDepth+.5f)*Tile);
                for(int y=Mathf.Max(0,cz-radius);y<Mathf.Min(texture.height,cz+radius+1);y++)
                for(int x=Mathf.Max(0,cx-radius);x<Mathf.Min(texture.width,cx+radius+1);x++)
                {
                    var p=rot*new Vector3((x-cx)*map.gridSize/Tile,0,(y-cz)*map.gridSize/Tile);
                    if(Mathf.Abs(p.x)>size.x*.5f || Mathf.Abs(p.z)>size.z*.5f) continue;
                    bool edge=outline && (Mathf.Abs(p.x)>size.x*.5f-map.gridSize/Tile*1.5f || Mathf.Abs(p.z)>size.z*.5f-map.gridSize/Tile*1.5f);
                    texture.SetPixel(x,y,edge?wall:fill);
                }
            }
            foreach(var item in map.floors[floor].items)
            {
                if(item.type==ItemType.Wall || item.type==ItemType.Stairs) continue;
                var center=new Vector3(item.gridX*map.gridSize,0,item.gridZ*map.gridSize);
                var size=item.type==ItemType.Bed?new Vector3(1.8f,0,2):item.type==ItemType.Door?new Vector3(map.gridSize,0,.2f):new Vector3(1.7f,0,1);
                var fill=item.type==ItemType.Door?new Color(.1f,.6f,.35f):item.type==ItemType.StreamerPC?new Color(1,.68f,.16f):item.type==ItemType.Bed?new Color(.85f,.65f,.67f):item.hideable?new Color(.67f,.61f,.82f):new Color(.63f,.68f,.69f);
                var definition=LevelObjectCatalog.Default?.Resolve(item);
                if(definition!=null && definition.overrideColliderSize) size=definition.colliderSize;
                if(item.type==ItemType.Custom) size=item.customSize;
                Rectangle(center,size,item.rotationY,fill,true);
                if(item.type==ItemType.Bed) Rectangle(center+Quaternion.Euler(0,item.rotationY,0)*Vector3.forward*.6f,new Vector3(1.35f,0,.35f),item.rotationY,Color.white,true);
                if(item.hideable) Rectangle(center,new Vector3(.07f,0,size.z),item.rotationY,wall,false);
            }
            foreach(var prop in map.taskProps.Where(p=>Mathf.FloorToInt((p.position.y+.1f)/map.floorHeight)==floor)) Rectangle(prop.position,new Vector3(Mathf.Max(.3f,prop.size.x),0,Mathf.Max(.3f,prop.size.z)),prop.rotation.y,new Color(.2f,.6f,.73f),true);
            texture.Apply();return texture;
        }

        public void Open(int floor) {SelectFloor(floor);match.OpenPhonePage(PhonePage.Map);}
        public void SelectFloor(int floor) => Floor=Mathf.Clamp(floor,0,Floors.Count-1);
        public void Close() {if(match.IsStreamerPlayer) match.SetStreamerMap(false);else match.OpenPhonePage(PhonePage.Home);}
        void OnDestroy() {foreach(var t in Floors) Destroy(t);}
    }
}
