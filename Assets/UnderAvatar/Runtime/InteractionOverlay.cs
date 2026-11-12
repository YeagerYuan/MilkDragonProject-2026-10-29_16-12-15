using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    // Interaction-only extraction from origin/ui. Phone UI remains in PhoneHud.
    public sealed class InteractionOverlay : MonoBehaviour
    {
        GUIStyle hint,check;
        Font font;
        bool ownsFont;
        Texture2D disc;
        void EnsureStyles()
        {
            if(hint!=null) return;
            font=Resources.Load<Font>("MainMenu/Fonts/Hangeuljaemin");
            if(!font) {font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},20);ownsFont=true;}
            hint=new GUIStyle(GUI.skin.label){font=font,fontSize=20,wordWrap=false,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),normal={textColor=Color.white}};
            check=new GUIStyle(hint){alignment=TextAnchor.MiddleCenter};
            disc=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};
            var pixels=new Color[4096];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++) pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01((.5f-new Vector2((x+.5f)/64-.5f,(y+.5f)/64-.5f).magnitude)*64));
            disc.SetPixels(pixels);disc.Apply();
        }
        static void Fill(Rect r,Color c)
        {var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Circle(Rect r,Color c)
        {var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,disc);GUI.color=old;}
        void Rounded(Rect r,float radius,Color color)
        {
            Fill(new Rect(r.x+radius,r.y,r.width-radius*2,r.height),color);
            Fill(new Rect(r.x,r.y+radius,r.width,r.height-radius*2),color);
            foreach(float x in new[]{r.x,r.xMax-radius*2}) foreach(float y in new[]{r.y,r.yMax-radius*2}) Circle(new Rect(x,y,radius*2,radius*2),color);
        }
        public string ReporterInteractionMessage(MatchSession match)
        {
            var reporter=match.Reporter;
            if(!reporter || !reporter.IsHuman || reporter.Hidden || reporter.PhoneOpen || match.Finished || match.DebugOpen || !match.InputCaptured)
                return null;
            var held=match.TaskWorld?match.TaskWorld.Held:null;
            if(held && Physics.Raycast(reporter.View.transform.position,reporter.View.transform.forward,out var heldHit,3f,
                PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
            {
                var heldTarget=heldHit.collider.GetComponentInParent<PlacedItemView>();
                if(heldTarget && heldTarget.Data.type==ItemType.Door) return heldTarget.IsOpen?"关闭门":"打开门";
            }
            if(held) return "放下 "+held.Data.name;
            var document=reporter.Documents?reporter.Documents.Target:null;
            if(document) return "阅读";
            var prop=match.TaskWorld?match.TaskWorld.AimProp():null;
            if(prop && prop.Data.portable) return "拿起 "+prop.Data.name;
            if(Physics.Raycast(reporter.View.transform.position,reporter.View.transform.forward,out var hit,3f,
                PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
            {
                var item=hit.collider.GetComponentInParent<PlacedItemView>();
                if(item && item.Data.type==ItemType.Door) return item.IsOpen?"关闭门":"打开门";
                if(item && item.IsHidingPlace) return "躲藏 "+item.DisplayName;
            }
            var openDoor=match.World.Items.Where(i=>i.Floor==reporter.Floor && i.Data.type==ItemType.Door && i.IsOpen)
                .Where(i=>Vector3.Distance(i.transform.position,reporter.transform.position)<2.3f)
                .OrderBy(i=>Vector3.Distance(i.transform.position,reporter.transform.position))
                .FirstOrDefault(i=>!Physics.Linecast(reporter.View.transform.position,i.transform.position+Vector3.up,
                    PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore));
            return openDoor?"关闭门":null;
        }
        public void DrawInteractionHint(string message,bool panel=false)
        {
            if(string.IsNullOrWhiteSpace(message)) return;
            EnsureStyles();
            var previous=GUI.matrix;float scale=Screen.height/800f,width=Screen.width/scale;
            try
            {
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                float textWidth=Mathf.Min(500,hint.CalcSize(new GUIContent(message)).x+4);
                const float keyWidth=36,gap=10;
                if(panel) Rounded(new Rect((width-keyWidth-gap-textWidth)*.5f-8,594,keyWidth+gap+textWidth+16,40),6,new Color(.08f,.08f,.08f,.65f));
                KeyHint(new Vector2((width-keyWidth-gap-textWidth)*.5f,600),"E",message,keyWidth,textWidth);
            }
            finally {GUI.matrix=previous;}
        }
        void KeyHint(Vector2 position,string key,string text,float keyWidth=26,float textWidth=180)
        {
            var r=new Rect(position.x,position.y,keyWidth,28);
            Rounded(r,6,Color.white);Rounded(new Rect(r.x+1.5f,r.y+1.5f,r.width-3,r.height-3),5,Color.black);
            GUI.Label(r,key,check);GUI.Label(new Rect(r.xMax+10,r.y,textWidth,28),text,hint);
        }
        void OnDestroy() {if(disc) PrototypeVisuals.Release(disc);if(ownsFont && font) PrototypeVisuals.Release(font);}
    }
}
