using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    public sealed class BuildingWorld : MonoBehaviour
    {
        public BuildingData Data { get; private set; }
        public IReadOnlyList<PlacedItemView> Items => items;
        public PrototypeVisuals Palette { get; private set; }
        readonly List<PlacedItemView> items = new List<PlacedItemView>();
        readonly List<GameObject> floors = new List<GameObject>();
        readonly List<List<Renderer>> slabs = new List<List<Renderer>>();
        public IReadOnlyList<StairLayout> Stairs { get; private set; }
        readonly List<GameObject> grids = new List<GameObject>();
        public bool Editing { get; private set; } = true;
        public int ActiveFloor { get; private set; }

        void Awake() { if(Palette==null) Palette=new PrototypeVisuals(); }

        public void Rebuild(BuildingData data)
        {
            data.Validate();
            StairLayout.Validate(data);
            if(Palette==null) Palette=new PrototypeVisuals();
            foreach(var floor in floors) { floor.SetActive(false); PrototypeVisuals.Release(floor); }
            floors.Clear(); slabs.Clear(); grids.Clear(); items.Clear();
            Data = data;
            Stairs=StairLayout.All(data);
            foreach(var floor in data.floors)
            {
                var root = new GameObject("Floor_" + floor.floorIndex);
                root.transform.SetParent(transform,false);
                root.transform.localPosition = Vector3.up * data.floorHeight * floor.floorIndex;
                floors.Add(root);
                var floorSlabs=new List<Renderer>(); slabs.Add(floorSlabs);
                var openings=new HashSet<Vector2Int>();
                foreach(var stair in Stairs.Where(st=>st.Floor+1==floor.floorIndex))
                    foreach(var cell in stair.Opening()) openings.Add(new Vector2Int(cell.x,cell.z));
                // Merge contiguous cells into row strips, leaving real collision/rendering openings.
                for(int z=-data.halfDepth;z<=data.halfDepth;z++)
                {
                    int x=-data.halfWidth;
                    while(x<=data.halfWidth)
                    {
                        while(x<=data.halfWidth && openings.Contains(new Vector2Int(x,z))) x++;
                        int first=x;
                        while(x<=data.halfWidth && !openings.Contains(new Vector2Int(x,z))) x++;
                        if(first==x) continue;
                        var slab=Palette.Box(root.transform,"FloorSlab",new Vector3((first+x-1)*data.gridSize*.5f,-.12f,z*data.gridSize),
                            new Vector3((x-first)*data.gridSize,.24f,data.gridSize),new Color(.20f,.23f,.27f),"UA_Wall");
                        floorSlabs.Add(slab.GetComponent<Renderer>());
                    }
                }
                var grid = new GameObject("EditorGrid");
                grid.transform.SetParent(root.transform,false);
                grids.Add(grid);
                for(int x=-data.halfWidth; x<=data.halfWidth+1; x++)
                    Palette.Box(grid.transform,"GridX",new Vector3((x-.5f)*data.gridSize,.015f,0),
                        new Vector3(.018f,.018f,(data.halfDepth*2+1)*data.gridSize),new Color(.33f,.40f,.46f),"UA_Props",false);
                for(int z=-data.halfDepth; z<=data.halfDepth+1; z++)
                    Palette.Box(grid.transform,"GridZ",new Vector3(0,.015f,(z-.5f)*data.gridSize),
                        new Vector3((data.halfWidth*2+1)*data.gridSize,.018f,.018f),new Color(.33f,.40f,.46f),"UA_Props",false);
                foreach(var item in floor.items) CreateView(floor.floorIndex,item);
            }
            SetMode(Editing,Mathf.Clamp(ActiveFloor,0,floors.Count-1));
            Physics.SyncTransforms();
        }

        public PlacedItemView CreateView(int floor, PlacedItemData item)
        {
            var go = new GameObject();
            go.transform.SetParent(floors[floor].transform,false);
            go.transform.localPosition = new Vector3(item.gridX*Data.gridSize,0,item.gridZ*Data.gridSize);
            var view = go.AddComponent<PlacedItemView>();
            view.Initialize(item,floor,Data.gridSize,Data.floorHeight,Palette);
            items.Add(view);
            return view;
        }

        public void RemoveView(string id)
        {
            var view = items.Find(v => v.Data.id == id);
            if (!view) return;
            items.Remove(view);
            view.gameObject.SetActive(false);
            PrototypeVisuals.Release(view.gameObject);
        }

        public void SetMode(bool editing, int activeFloor)
        {
            Editing = editing;
            ActiveFloor = activeFloor;
            for(int f=0; f<floors.Count; f++)
            {
                floors[f].SetActive(!editing || f<=activeFloor);
                foreach(var slab in slabs[f])
                {
                    slab.enabled=true;
                    slab.sharedMaterial=Palette.Material(f<activeFloor && editing ? new Color(.065f,.075f,.09f) : new Color(.20f,.23f,.27f));
                }
                grids[f].SetActive(editing && f==activeFloor);
            }
            foreach(var view in items) view.SetDimmed(editing && view.Floor<activeFloor-(view.Data.type==ItemType.Stairs?1:0),Palette);
            Physics.SyncTransforms();
        }

        public PlacedItemView Find(string id) => items.FirstOrDefault(v => v.Data.id==id);

        public PlacedItemView ItemAt(int floor,int x,int z)
        {
            var item=Data.At(floor,x,z);
            if(item!=null) return Find(item.id);
            var stair=Stairs.FirstOrDefault(st=>st.ContainsCell(floor,x,z));
            return stair==null?null:Find(stair.Item.id);
        }
        public IReadOnlyList<Renderer> FloorSlabs(int floor) => slabs[floor];

        public Vector3 FindFreePosition(int floor)
        {
            for(int radius=0;radius<=Data.halfWidth+Data.halfDepth;radius++)
                for(int z=-Data.halfDepth+1;z<Data.halfDepth;z++)
                    for(int x=-Data.halfWidth+1;x<Data.halfWidth;x++)
                    {
                        if (Mathf.Abs(x+4)+Mathf.Abs(z+2)!=radius || ItemAt(floor,x,z)!=null) continue;
                        Vector3 p=new Vector3(x*Data.gridSize,floor*Data.floorHeight,z*Data.gridSize);
                        if(!Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.5f,.32f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
                            return p;
                    }
            throw new System.InvalidOperationException("这一楼层没有可用的玩家出生位置。");
        }

        public void ReleaseMaterials() { Palette?.Dispose(); Palette=null; }
        void OnDestroy() { ReleaseMaterials(); }
    }
}
