using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    // Presentation only. Evidence and uploads remain authoritative in MatchSession.
    public sealed class PaparazziOverlay : MonoBehaviour
    {
        GUIStyle header,rowTitle,body,descriptionStyle,taskTitle,taskText,hint,action,check;
        Texture2D disc,actionUnderline;
        Texture portrait;
        readonly List<Font> ownedFonts=new List<Font>();
        readonly Dictionary<string,float> hoveredAt=new Dictionary<string,float>();
        readonly Color red=new Color(.9f,.025f,0);
        Vector2 scroll;
        MatchSession current;
        bool raised;
        float fromY,toY,slideStarted;
        const float PhoneWidth=356,OpenY=76,ClosedY=720,SlideSeconds=.35f;

        Font LoadFont(string path,string fallback)
        {
            var font=Resources.Load<Font>(path);if(font) return font;
            font=Font.CreateDynamicFontFromOSFont(fallback,20);ownedFonts.Add(font);return font;
        }
        void EnsureStyles()
        {
            if(body!=null) return;
            var font=LoadFont("MainMenu/Fonts/Hangeuljaemin","Microsoft YaHei");
            var calligraphy=LoadFont("MainMenu/Fonts/AQingRanTi","KaiTi");
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),normal={textColor=Color.white}};
            header=new GUIStyle(body){font=calligraphy,fontSize=36,wordWrap=false,normal={textColor=red}};
            descriptionStyle=new GUIStyle(body){alignment=TextAnchor.UpperLeft};
            rowTitle=new GUIStyle(body){fontSize=20,wordWrap=false};
            taskTitle=new GUIStyle(header){fontSize=32};
            taskText=new GUIStyle(body){fontSize=24,wordWrap=false};
            hint=new GUIStyle(body){fontSize=20,wordWrap=false};
            action=new GUIStyle(hint){fontSize=32,alignment=TextAnchor.MiddleCenter};
            check=new GUIStyle(body){fontSize=20,alignment=TextAnchor.MiddleCenter};
            disc=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[4096];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++) pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01((.5f-new Vector2((x+.5f)/64-.5f,(y+.5f)/64-.5f).magnitude)*64));
            disc.SetPixels(pixels);disc.Apply();
            actionUnderline=new Texture2D(256,32,TextureFormat.RGBA32,false){name="Phone action curved underline",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var underlinePixels=new Color[256*32];
            for(int x=0;x<256;x++)
            {
                float edge=Mathf.Min(x,255-x),curve=edge<28?Mathf.Sqrt(Mathf.Max(0,28*28-(28-edge)*(28-edge))):28;
                for(int y=0;y<32;y++) underlinePixels[y*256+x]=new Color(1,1,1,Mathf.Clamp01(1-Mathf.Abs((31-y)-curve))*.8f);
            }
            actionUnderline.SetPixels(underlinePixels);actionUnderline.Apply();
            portrait=Resources.Load<Texture2D>("MainMenu/PhoneLogo");
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
        static void Outline(Rect r,Color color,float thickness=1)
        {
            Fill(new Rect(r.x,r.y,r.width,thickness),color);Fill(new Rect(r.x,r.yMax-thickness,r.width,thickness),color);
            Fill(new Rect(r.x,r.y,thickness,r.height),color);Fill(new Rect(r.xMax-thickness,r.y,thickness,r.height),color);
        }
        float PhoneY => Mathf.Lerp(fromY,toY,Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-slideStarted)/SlideSeconds)));
        public void Draw(MatchSession match)
        {
            EnsureStyles();
            bool open=match.Reporter.PhoneOpen && match.PhoneMenuOpen && match.PendingPhoto==null;
            if(current!=match)
            {current=match;raised=open;fromY=toY=open?OpenY:ClosedY;slideStarted=Time.unscaledTime;scroll=Vector2.zero;hoveredAt.Clear();}
            if(open!=raised) {fromY=PhoneY;toY=open?OpenY:ClosedY;slideStarted=Time.unscaledTime;raised=open;}
            var previous=GUI.matrix;
            float scale=Screen.height/800f,width=Screen.width/scale;
            try
            {
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                string selected=match.SelectedTaskId==InvestigationData.LiveTask?"主播直播证据":match.Quests.Find(q=>q.Id==match.SelectedTaskId)?.Title??"请选择任务";
                GUI.Label(new Rect(32,690,600,42),"当前任务",taskTitle);
                GUI.Label(new Rect(32,734,Mathf.Max(200,width-430),40),selected,taskText);
                if(match.CameraMode) DrawViewfinder(width);
                if(match.PendingPhoto!=null) DrawReview(match);
                else if(Time.time<match.MessageUntil && match.LastSubmittedPhoto>0)
                    GUI.Label(new Rect(32,640,Mathf.Max(260,width-450),44),match.Message,body);
                float x=width-PhoneWidth-26,y=PhoneY;
                if(!match.CameraMode)
                {
                    KeyHint(new Vector2(x+10,y-40),"Q","拍摄");
                    KeyHint(new Vector2(x+100,y-40),"F","打开/关闭手机");
                }
                DrawPhone(match,new Rect(x,y,PhoneWidth,690),open);
                DrawInteractionHint(ReporterInteractionMessage(match));
            }
            finally {GUI.matrix=previous;}
        }
        string ReporterInteractionMessage(MatchSession match)
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
            if(document) return "阅读 "+document.DisplayName;
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
        public void DrawInteractionHint(string message)
        {
            if(string.IsNullOrWhiteSpace(message)) return;
            EnsureStyles();
            var previous=GUI.matrix;float scale=Screen.height/800f,width=Screen.width/scale;
            try
            {
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                float textWidth=Mathf.Min(500,hint.CalcSize(new GUIContent(message)).x+4);
                const float keyWidth=36,gap=10;
                KeyHint(new Vector2((width-keyWidth-gap-textWidth)*.5f,600),"E",message,keyWidth,textWidth);
            }
            finally {GUI.matrix=previous;}
        }
        void DrawPhone(MatchSession match,Rect rect,bool interactive)
        {
            Rounded(rect,44,new Color(.40f,.45f,.50f));
            Rounded(new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6),42,new Color(.10f,.12f,.15f));
            Rounded(new Rect(rect.x+8,rect.y+8,rect.width-16,rect.height-16),37,new Color(.20f,.22f,.24f));
            Rounded(new Rect(rect.x+12,rect.y+12,rect.width-24,rect.height-24),34,new Color(.055f,.055f,.055f));
            Fill(new Rect(rect.x-2,rect.y+110,3,44),new Color(.4f,.44f,.48f));
            Fill(new Rect(rect.xMax-1,rect.y+127,3,74),new Color(.4f,.44f,.48f));
            GUI.BeginGroup(rect);
            if(portrait) GUI.DrawTexture(new Rect(34,16,58,58),portrait,ScaleMode.ScaleToFit,true);
            GUI.Label(new Rect(114,16,230,58),"了解爱播之旅",header);
            GUI.Label(new Rect(34,105,285,28),"选择任务进行拍摄",body);
            int total=match.Quests.Count+(match.RequiresLiveEvidence?1:0);
            var view=new Rect(34,144,288,448);
            scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,total>5?272:288,Mathf.Max(448,total*92-12)));
            int index=0;
            foreach(var quest in match.Quests)
            {DrawTask(match,new Rect(0,index*92,total>5?272:288,80),quest.Id,(index+1).ToString("00")+" "+quest.Title,quest.Description,quest.Complete,interactive);index++;}
            if(match.RequiresLiveEvidence)
                DrawTask(match,new Rect(0,index*92,total>5?272:288,80),InvestigationData.LiveTask,(index+1).ToString("00")+" 主播直播证据","拍摄正在直播的主播，每场最多一张。 "+match.StreamerPhotos+" / 3",match.StreamerPhotos>=3,interactive);
            GUI.EndScrollView();
            int completed=match.Quests.Count(q=>q.Complete)+(match.RequiresLiveEvidence && match.StreamerPhotos>=3?1:0);
            int segments=Mathf.Max(1,total);float gap=6,w=(250-gap*(segments-1))/segments;
            for(int i=0;i<segments;i++) Rounded(new Rect(34+i*(w+gap),619,w,8),4,i<completed?red:new Color(.52f,.52f,.52f));
            GUI.Label(new Rect(292,605,52,36),completed+"/"+total,body);
            GUI.EndGroup();
        }
        void DrawTask(MatchSession match,Rect r,string id,string title,string description,bool complete,bool interactive)
        {
            bool selected=match.SelectedTaskId==id;
            if(interactive && r.Contains(Event.current.mousePosition)) hoveredAt[id]=Time.unscaledTime;
            float hover=hoveredAt.TryGetValue(id,out float at)?1-Mathf.Clamp01((Time.unscaledTime-at)/.18f):0;
            var color=Color.Lerp(new Color(.46f,.46f,.46f),red,selected?1:hover);
            Rounded(r,4,color);Rounded(new Rect(r.x+1.5f,r.y+1.5f,r.width-3,r.height-3),3,new Color(.055f,.055f,.055f));
            if(hover>0 || selected) Rounded(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),3,new Color(.65f,0,0,(selected?.045f:hover*.07f)));
            var old=GUI.color;GUI.color=selected?red:Color.Lerp(Color.white,red,hover);
            float textWidth=Mathf.Max(0,r.width-66);
            GUI.Label(new Rect(r.x+12,r.y+4,r.width-60,28),title,rowTitle);GUI.color=old;
            GUI.Label(new Rect(r.x+12,r.y+33,textWidth,49),description??"",descriptionStyle);
            var box=new Rect(r.xMax-35,r.y+8,22,22);Rounded(box,5,complete?red:Color.white);Rounded(new Rect(box.x+1,box.y+1,20,20),4,new Color(.055f,.055f,.055f));
            if(complete) GUI.Label(box,"√",check);
            if(interactive && GUI.Button(r,GUIContent.none,GUIStyle.none)) match.QueuePhoneAction(task:id);
        }
        void DrawViewfinder(float width)
        {
            var frame=new Rect(width*.025f,40,width*.95f,628);
            var color=new Color(.9f,.9f,.9f,.75f);Outline(frame,color);
            Fill(new Rect(width*.5f-15,354,30,1),color);Fill(new Rect(width*.5f,339,1,30),color);
            KeyHint(new Vector2(frame.xMax-252,frame.yMax-42),"左键","进行拍照",44,96);
            KeyHint(new Vector2(frame.xMax-92,frame.yMax-42),"Q","取消",26,58);
        }
        void KeyHint(Vector2 position,string key,string text,float keyWidth=26,float textWidth=180)
        {
            var r=new Rect(position.x,position.y,keyWidth,28);
            Rounded(r,6,Color.white);Rounded(new Rect(r.x+1.5f,r.y+1.5f,r.width-3,r.height-3),5,Color.black);
            GUI.Label(r,key,check);GUI.Label(new Rect(r.xMax+10,r.y,textWidth,28),text,hint);
        }
        void DrawReview(MatchSession match)
        {
            Fill(new Rect(32,490,330,180),Color.white);
            Fill(new Rect(43,501,308,158),Color.black);
            if(match.PhotoPreview) GUI.DrawTexture(new Rect(43,501,308,158),match.PhotoPreview,ScaleMode.ScaleToFit);
            if(TextButton(new Rect(390,518,170,48),"确认上传")) match.QueuePhoneAction(upload:match.PendingPhoto.id);
            if(TextButton(new Rect(390,584,170,48),"放弃上传")) match.QueuePhoneAction(cancel:true);
        }
        bool TextButton(Rect rect,string text)
        {
            bool hover=rect.Contains(Event.current.mousePosition);
            var old=GUI.color;GUI.color=hover?red:Color.white;GUI.Label(rect,text,action);GUI.color=old;
            GUI.color=hover?red:new Color(1,1,1,.9f);
            GUI.DrawTexture(new Rect(rect.x+6,rect.yMax-17,rect.width-12,23),actionUnderline);
            GUI.color=old;
            return GUI.Button(rect,GUIContent.none,GUIStyle.none);
        }
        void OnDestroy()
        {
            if(disc) Destroy(disc);
            if(actionUnderline) Destroy(actionUnderline);
            foreach(var font in ownedFonts) if(font) Destroy(font);
        }
    }
}
