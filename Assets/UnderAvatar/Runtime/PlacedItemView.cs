using UnityEngine;

namespace UnderAvatar
{
    public sealed class PlacedItemView : MonoBehaviour
    {
        public PlacedItemData Data { get; private set; }
        public int Floor { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsHidingPlace => Data.IsHidingPlace;
        public string DisplayName => !string.IsNullOrWhiteSpace(Data.displayName)?Data.displayName:Data.type==ItemType.Bed?"床":Data.IsHidingPlace?"衣柜":Data.type.ToString();
        public Vector3 PhotoPoint => transform.position + Vector3.up * 1.15f;
        public Vector3 Front => transform.position - transform.forward * 1.4f;
        Transform doorLeaf;
        Collider doorCollider;
        Renderer[] visuals;
        Material[][] originalMaterials;
        Transform searchLeaf;
        Quaternion searchClosedRotation=Quaternion.identity;
        Vector3 searchOpenEuler;
        float searchCloseAt;

        public void Initialize(PlacedItemData data, int floor, float gridSize, float floorHeight,
            PrototypeVisuals palette, bool ghost = false)
        {
            Data = data;
            Floor = floor;
            gameObject.name = DisplayName + "_" + data.id;
            transform.localRotation = Quaternion.Euler(0, data.rotationY, 0);
            Color color = PrototypeVisuals.ItemColors[(int)data.type];
            if (ghost) color.a = .4f;
            float h = floorHeight;
            GameObject Box(string name, Vector3 p, Vector3 s, Color c, string layer = "UA_Props", bool solid = true) =>
                name=="Wall" || name=="FrameLeft" || name=="FrameRight" || name=="Lintel"
                    ?palette.SurfaceBox(transform,name,p,s,c,BuildingSurfaceKind.Wall,transform.position.y,h,layer,solid,ghost)
                    :palette.Box(transform, name, p, s, c, layer, solid, ghost);
            switch (data.type)
            {
                case ItemType.Wall:
                    Box("Wall", new Vector3(0,h/2,0), new Vector3(gridSize,h,gridSize), color,"UA_Wall");
                    break;
                case ItemType.Door:
                    Box("FrameLeft", new Vector3(-gridSize/2+.08f,h/2,0), new Vector3(.16f,h,.24f),color,"UA_Wall");
                    Box("FrameRight", new Vector3(gridSize/2-.08f,h/2,0), new Vector3(.16f,h,.24f),color,"UA_Wall");
                    Box("Lintel", new Vector3(0,h-.15f,0), new Vector3(gridSize,.3f,.24f),color,"UA_Wall");
                    doorLeaf = new GameObject("DoorHinge").transform;
                    doorLeaf.SetParent(transform, false);
                    doorLeaf.localPosition = new Vector3(-gridSize/2+.16f,0,0);
                    var leaf = palette.Box(doorLeaf,"DoorLeaf",new Vector3((gridSize-.32f)/2,(h-.3f)/2,0),
                        new Vector3(gridSize-.32f,h-.3f,.12f),color,"UA_ClosedDoor",true,ghost);
                    doorCollider = leaf.GetComponent<Collider>();
                    break;
                case ItemType.Furniture:
                    if (data.hideable)
                    {
                        Box("Wardrobe",new Vector3(0,1.25f,0),new Vector3(1.5f,2.5f,.9f),color);
                        Box("DoorSeam",new Vector3(0,1.25f,-.46f),new Vector3(.03f,2.2f,.015f),Color.black,"UA_Props",false);
                        searchLeaf=new GameObject("WardrobeSearchDoor").transform;searchLeaf.SetParent(transform,false);
                        searchLeaf.localPosition=new Vector3(-.75f,0,-.48f);
                        palette.Box(searchLeaf,"CabinetDoor",new Vector3(.75f,1.25f,0),new Vector3(1.48f,2.48f,.06f),color*.8f,"UA_Props",false,ghost);
                    }
                    else
                    {
                        Box("TableTop",new Vector3(0,.85f,0),new Vector3(1.7f,.22f,1.05f),color);
                        foreach (float x in new[] { -.65f,.65f })
                            foreach (float z in new[] { -.35f,.35f })
                                Box("Leg",new Vector3(x,.4f,z),new Vector3(.14f,.8f,.14f),color);
                    }
                    break;
                case ItemType.QuestItem:
                    Box("Pedestal",new Vector3(0,.4f,0),new Vector3(.65f,.8f,.65f),new Color(.25f,.28f,.32f));
                    Box("Evidence",new Vector3(0,1.02f,0),new Vector3(.44f,.44f,.28f),color,"UA_Target");
                    break;
                case ItemType.StreamerPC:
                    Box("Desk",new Vector3(0,.8f,0),new Vector3(1.8f,.18f,1.05f),color);
                    Box("Base",new Vector3(0,.4f,.1f),new Vector3(.5f,.8f,.5f),color);
                    Box("Monitor",new Vector3(0,1.4f,.25f),new Vector3(1.05f,.68f,.12f),new Color(.08f,.12f,.18f));
                    Box("Screen",new Vector3(0,1.4f,.18f),new Vector3(.94f,.55f,.02f),new Color(.25f,.8f,.73f),"UA_Props",false);
                    Box("Seat",new Vector3(0,.45f,-.95f),new Vector3(.65f,.15f,.65f),color,"UA_Props",false);
                    break;
                case ItemType.Stairs:
                    new StairLayout(new BuildingData{gridSize=gridSize,floorHeight=floorHeight},floor,data).Build(transform,palette,ghost);
                    break;
                case ItemType.Bed:
                    Box("BedBase",new Vector3(0,.35f,0),new Vector3(1.75f,.65f,1.85f),color*.6f);
                    Box("Headboard",new Vector3(0,.7f,.9f),new Vector3(1.85f,1.3f,.12f),color);
                    searchLeaf=new GameObject("MattressHinge").transform;searchLeaf.SetParent(transform,false);searchLeaf.localPosition=new Vector3(0,.7f,.85f);
                    palette.Box(searchLeaf,"Mattress",new Vector3(0,.08f,-.85f),new Vector3(1.75f,.22f,1.8f),new Color(.62f,.66f,.73f),"UA_Props",false,ghost);
                    break;
                case ItemType.Custom:
                    Box("CustomObject",Vector3.up*data.customSize.y*.5f,data.customSize,color);
                    break;
            }
            searchOpenEuler=Data.type==ItemType.Bed?new Vector3(-55,0,0):new Vector3(0,100,0);
            ApplyPrefab(LevelObjectCatalog.Default?.Resolve(data),ghost);
            visuals = GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[visuals.Length][];
            for(int i=0;i<visuals.Length;i++) originalMaterials[i] = visuals[i].sharedMaterials;
        }

        void Update()
        {
            if(IsHidingPlace && IsOpen && Time.time>=searchCloseAt) SetSearchOpen(false);
        }
        public void SetSearchOpen(bool open)
        {
            if(!IsHidingPlace) return;
            IsOpen=open;searchCloseAt=Time.time+1.2f;
            if(searchLeaf) searchLeaf.localRotation=searchClosedRotation*Quaternion.Euler(open?searchOpenEuler:Vector3.zero);
        }

        void ApplyPrefab(LevelObjectDefinition definition,bool ghost)
        {
            if(definition==null || !definition.prefab || ghost) return;
            // Keep proven gameplay colliders and anchors. Prefabs replace visuals only.
            Transform parent=Data.type==ItemType.Door?doorLeaf:transform;
            var replaced=parent.GetComponentsInChildren<Renderer>();
            var bounds=LocalBounds(parent,replaced);
            var model=new GameObject("CustomVisual_"+definition.displayName);model.transform.SetParent(parent,false);
            var content=Instantiate(definition.prefab,model.transform,false);
            content.transform.localPosition=Vector3.zero;
            content.transform.localRotation=Quaternion.Euler(definition.rotation)*definition.prefab.transform.localRotation;
            content.transform.localScale=definition.prefab.transform.localScale;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic=true;body.useGravity=false; }
            foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
            foreach(var source in model.GetComponentsInChildren<AudioSource>(true)) source.enabled=false;
            foreach(var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled=false;
            foreach(var listener in model.GetComponentsInChildren<AudioListener>(true)) listener.enabled=false;
            foreach(var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=PrototypeLayers.Get("UA_Props");
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) { PrototypeVisuals.Release(model);return; }
            if(definition.fitToPrototype)
            {
                var own=LocalBounds(parent,renderers);
                model.transform.localScale=Vector3.Scale(definition.scale,new Vector3(bounds.size.x/Mathf.Max(.001f,own.size.x),bounds.size.y/Mathf.Max(.001f,own.size.y),bounds.size.z/Mathf.Max(.001f,own.size.z)));
                own=LocalBounds(parent,renderers);model.transform.localPosition=bounds.center-own.center+definition.offset;
            }
            else { model.transform.localScale=definition.scale;model.transform.localPosition=definition.offset; }
            foreach(var renderer in replaced) renderer.enabled=false;
            var bindings=model.GetComponentInChildren<LevelPrefabBindings>();
            if(IsHidingPlace && bindings && bindings.searchPivot)
            { searchLeaf=bindings.searchPivot;searchClosedRotation=searchLeaf.localRotation;searchOpenEuler=bindings.searchOpenEuler; }
        }
        static Bounds LocalBounds(Transform root,Renderer[] renderers)
        {
            bool first=true;var result=new Bounds();
            foreach(var renderer in renderers)
            {
                var b=renderer.localBounds;
                for(int i=0;i<8;i++)
                {
                    var point=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    point=root.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if(first) { result=new Bounds(point,Vector3.zero);first=false; } else result.Encapsulate(point);
                }
            }
            return result;
        }

        public void SetDoorOpen(bool open)
        {
            if (Data.type != ItemType.Door) return;
            IsOpen = open;
            doorLeaf.localRotation = Quaternion.Euler(0,open ? -100 : 0,0);
            doorCollider.enabled = !open;
            doorCollider.gameObject.layer = PrototypeLayers.Get(open ? "UA_OpenDoor" : "UA_ClosedDoor");
        }

        public void SetDimmed(bool dimmed, PrototypeVisuals palette)
        {
            for(int i=0;i<visuals.Length;i++)
                if(visuals[i])
                {
                    var result=new Material[originalMaterials[i].Length];
                    for(int j=0;j<result.Length;j++) result[j]=dimmed?palette.Dimmed(originalMaterials[i][j]):originalMaterials[i][j];
                    visuals[i].sharedMaterials=result;
                }
        }
    }
}
