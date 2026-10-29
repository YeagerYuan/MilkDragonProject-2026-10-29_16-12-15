using UnityEngine;

namespace UnderAvatar
{
    public sealed class PlacedItemView : MonoBehaviour
    {
        public PlacedItemData Data { get; private set; }
        public int Floor { get; private set; }
        public bool IsOpen { get; private set; }
        public Vector3 PhotoPoint => transform.position + Vector3.up * 1.15f;
        public Vector3 Front => transform.position - transform.forward * 1.4f;
        Transform doorLeaf;
        Collider doorCollider;
        Renderer[] visuals;
        Material[] originalMaterials;

        public void Initialize(PlacedItemData data, int floor, float gridSize, float floorHeight,
            PrototypeVisuals palette, bool ghost = false)
        {
            Data = data;
            Floor = floor;
            gameObject.name = data.type + "_" + data.id;
            transform.localRotation = Quaternion.Euler(0, data.rotationY, 0);
            Color color = PrototypeVisuals.ItemColors[(int)data.type];
            if (ghost) color.a = .4f;
            float h = floorHeight;
            GameObject Box(string name, Vector3 p, Vector3 s, Color c, string layer = "UA_Props", bool solid = true) =>
                palette.Box(transform, name, p, s, c, layer, solid, ghost);
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
            }
            visuals = GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[visuals.Length];
            for(int i=0;i<visuals.Length;i++) originalMaterials[i] = visuals[i].sharedMaterial;
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
                visuals[i].sharedMaterial = dimmed ? palette.Material(originalMaterials[i].color * new Color(.25f,.25f,.25f,1)) : originalMaterials[i];
        }
    }
}
