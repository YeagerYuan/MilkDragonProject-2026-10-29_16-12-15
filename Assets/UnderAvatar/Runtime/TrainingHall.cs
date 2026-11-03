using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnderAvatar
{
    public static class TrainingHallLayout
    {
        public static BuildingData Create()
        {
            var map=new BuildingData{name="明亮教学大厅",halfWidth=7,halfDepth=5,floorHeight=4};
            for(int f=0;f<2;f++)
            {
                var floor=new FloorData{floorIndex=f};map.floors.Add(floor);
                for(int x=-7;x<=7;x++) { Add(floor,ItemType.Wall,x,-5);Add(floor,ItemType.Wall,x,5); }
                for(int z=-4;z<=4;z++) { Add(floor,ItemType.Wall,-7,z);Add(floor,ItemType.Wall,7,z); }
            }
            var ground=map.floors[0];
            Add(ground,ItemType.Stairs,-6,0);
            Add(ground,ItemType.Wall,-1,0);Add(ground,ItemType.Door,0,0);Add(ground,ItemType.Wall,1,0);
            Add(ground,ItemType.Furniture,-3,3).hideable=true;
            Add(ground,ItemType.Bed,0,3);Add(ground,ItemType.StreamerPC,4,3);
            var evidence=Add(ground,ItemType.QuestItem,4,-2);evidence.questId="hall_evidence";evidence.questDescription="大厅拍照练习物品";
            Add(ground,ItemType.Furniture,2,-2);
            map.Validate();StairLayout.Validate(map);return map;
        }
        static PlacedItemData Add(FloorData floor,ItemType type,int x,int z)
        {
            var item=new PlacedItemData{id="hall_"+floor.floorIndex+"_"+x+"_"+z,type=type,gridX=x,gridZ=z};floor.items.Add(item);return item;
        }
    }

    // Session-scoped decoration and lighting. Disabling the hall restores the formal match's darkness.
    public sealed class TrainingHall : MonoBehaviour
    {
        AmbientMode oldMode;
        Color oldAmbient;
        float oldIntensity,oldReflection;
        SphericalHarmonicsL2 oldProbe;
        bool captured;
        MatchSession match;
        public int SignCount => GetComponent<TutorialSigns>()?.SignCount??0;
        public void Initialize(MatchSession session)
        {
            match=session;
            oldMode=RenderSettings.ambientMode;oldAmbient=RenderSettings.ambientLight;
            oldIntensity=RenderSettings.ambientIntensity;oldReflection=RenderSettings.reflectionIntensity;oldProbe=RenderSettings.ambientProbe;captured=true;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.68f,.72f,.77f);
            RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.4f;
            var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.55f,.59f,.65f));RenderSettings.ambientProbe=probe;
            foreach(var vision in new[]{match.Reporter.Vision,match.Streamer.Vision}) { vision.enabled=false;vision.Lamp.enabled=false; }
            foreach(var renderer in match.World.GetComponentsInChildren<MeshRenderer>().Where(_=>!match.World.Palette.Surfaces || !match.World.Palette.Surfaces.Ready))
            {
                if(renderer.name=="FloorSlab") renderer.sharedMaterial=match.World.Palette.Material(new Color(.64f,.68f,.71f));
                else if(renderer.name=="Wall") renderer.sharedMaterial=match.World.Palette.Material(new Color(.78f,.81f,.83f));
            }
            for(int f=0;f<match.World.Data.floors.Count;f++)
            {
                var light=new GameObject("大厅顶灯 "+(f+1)).AddComponent<Light>();light.transform.SetParent(transform,false);
                light.transform.position=new Vector3(0,f*match.World.Data.floorHeight+3.6f,0);light.type=LightType.Point;light.range=32;light.intensity=2.4f;light.shadows=LightShadows.None;
                for(int x=-5;x<=5;x+=5)
                    match.World.Palette.Box(transform,"CeilingLight",new Vector3(x*2,(f+1)*match.World.Data.floorHeight-.15f,0),new Vector3(3,.08f,.5f),Color.white,"UA_Props",false);
            }
            gameObject.AddComponent<TutorialSigns>().Initialize(session);
        }
        void OnDisable()
        {
            if(!captured) return;captured=false;
            // The parent may already have restored the scene while the entire app is shutting down.
            var darkness=GetComponentInParent<DarkWorldLighting>();
            if(darkness && !darkness.IsApplied) return;
            RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientIntensity=oldIntensity;
            RenderSettings.reflectionIntensity=oldReflection;RenderSettings.ambientProbe=oldProbe;
        }
    }
}
