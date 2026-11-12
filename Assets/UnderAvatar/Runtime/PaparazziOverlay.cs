using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    public enum PhonePage { Home, Tasks, Map }
    // Presentation only. Evidence and uploads remain authoritative in MatchSession.
    public sealed class PaparazziOverlay : MonoBehaviour
    {
        GUIStyle header,rowTitle,body,descriptionStyle,taskDescription,taskTitle,taskText,hint,action,check;
        Texture2D disc,actionUnderline;
        Texture portrait;
        readonly List<Font> ownedFonts=new List<Font>();
        readonly Dictionary<string,float> hoveredAt=new Dictionary<string,float>();
        readonly Color red=new Color(.9f,.025f,0);
        Vector2 scroll,mapScroll;
        float mapZoom=1;
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
            header=new GUIStyle(body){font=calligraphy,fontSize=28,wordWrap=false,normal={textColor=red}};
            descriptionStyle=new GUIStyle(body){alignment=TextAnchor.UpperLeft};
            rowTitle=new GUIStyle(body){fontSize=18,wordWrap=false};
            taskDescription=new GUIStyle(descriptionStyle){fontSize=14};
            taskTitle=new GUIStyle(header){fontSize=32};
            taskText=new GUIStyle(body){fontSize=24,wordWrap=false};
            hint=new GUIStyle(body){fontSize=20,wordWrap=false};
            action=new GUIStyle(hint){fontSize=32,alignment=TextAnchor.MiddleCenter};
            check=new GUIStyle(body){fontSize=20,alignment=TextAnchor.MiddleCenter};
            // Informational labels have no mouse states. Clickable rows/buttons tint explicitly.
            foreach(var style in new[]{body,header,descriptionStyle,rowTitle,taskTitle,taskText,hint,action,check})
            {
                style.hover=style.normal;
                style.active=style.normal;
                style.focused=style.normal;
            }
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
            bool open=match.LocalPhoneOpen && match.LocalPhoneMenuOpen;
            if(current!=match)
            {current=match;raised=open;fromY=toY=open?OpenY:ClosedY;slideStarted=Time.unscaledTime;scroll=Vector2.zero;hoveredAt.Clear();}
            if(open!=raised) {fromY=PhoneY;toY=open?OpenY:ClosedY;slideStarted=Time.unscaledTime;raised=open;}
            var previous=GUI.matrix;
            float scale=Screen.height/800f,width=Screen.width/scale;
            try
            {
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                if(!match.IsStreamerPlayer)
                {
                if(!match.IsTraining || match.Reporter.PhoneOpen)
                {
                string selected=match.SelectedTaskId==InvestigationData.LiveTask?"主播直播证据":match.Quests.Find(q=>q.Id==match.SelectedTaskId)?.Title??"请选择任务";
                GUI.Label(new Rect(32,690,600,42),match.IsTraining?"拍照练习":"当前任务",taskTitle);
                GUI.Label(new Rect(32,734,Mathf.Max(200,width-430),60),match.IsTraining?"自由拍照，不计分":selected,new GUIStyle(taskText){wordWrap=true,fontSize=20});
                }
                if(match.CameraMode) DrawViewfinder(width);
                if(match.PendingPhoto!=null) DrawReview(match);
                else if(match.UploadFeedbackId>0 && Time.unscaledTime<match.UploadFeedbackUntil)
                {
                    var result=new Rect(32,540,Mathf.Min(610,width-450),120);
                    Rounded(result,12,match.UploadFeedbackSuccess?new Color(.04f,.25f,.13f,.96f):new Color(.36f,.025f,.025f,.96f));
                    GUI.Label(new Rect(result.x+14,result.y+8,result.width-28,32),match.UploadFeedbackTitle,hint);
                    GUI.Label(new Rect(result.x+14,result.y+44,result.width-28,70),match.UploadFeedbackDetail,descriptionStyle);
                }
                }
                float x=width-PhoneWidth-26,y=PhoneY;
                if(!match.CameraMode || !match.LocalPhoneOpen)
                {
                    if(!match.IsStreamerPlayer) KeyHint(new Vector2(x+10,y-40),"Q","拍摄");
                    KeyHint(new Vector2(x+100,y-40),"F","打开/关闭手机");
                }
                DrawPhone(match,new Rect(x,y,PhoneWidth,690),open);

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
            if(portrait) GUI.DrawTexture(new Rect(28,20,48,48),portrait,ScaleMode.ScaleToFit,true);
            GUI.Label(new Rect(88,18,240,56),match.IsStreamerPlayer?"房屋地图":"了解爱播之旅",header);
            bool prior=GUI.enabled;GUI.enabled=prior && interactive;
            if(!match.IsStreamerPlayer && match.ActivePhonePage==PhonePage.Tasks)
            {
                if(GUI.Button(new Rect(34,78,180,22),"任务列表",new GUIStyle(body){normal={textColor=red}})) match.OpenPhonePage(PhonePage.Tasks);
                if(GUI.Button(new Rect(250,78,72,22),"地图 →",body)) match.OpenPhonePage(PhonePage.Map);
            }
            else
            {
                if(!match.IsStreamerPlayer) AppIcon(match,new Rect(34,90,136,58),PhonePage.Tasks,"任务",false);
                AppIcon(match,new Rect(match.IsStreamerPlayer?110:186,90,136,58),PhonePage.Map,"地图",true);
            }
            if(match.ActivePhonePage==PhonePage.Map) DrawMapApp(match);
            else if(match.ActivePhonePage==PhonePage.Tasks) DrawTasksApp(match,interactive);
            else
            {
                GUI.Label(new Rect(34,200,288,120),"点击任务图标选择拍摄目标。\n点击地图图标查看所有楼层。",descriptionStyle);
                GUI.Label(new Rect(34,348,288,130),"F：打开 / 收起手机\nQ：快捷相机\nM：直接查看地图\n\n地图不显示玩家实时位置。",descriptionStyle);
            }
            GUI.enabled=prior && interactive;
            if((match.IsStreamerPlayer || match.ActivePhonePage!=PhonePage.Tasks) && GUI.Button(new Rect(34,636,288,32),match.IsStreamerPlayer?"收起手机（F / M）":"⌂ 返回手机桌面",new GUIStyle(body){alignment=TextAnchor.MiddleCenter}))
                {if(match.IsStreamerPlayer) match.SetStreamerMap(false);else match.OpenPhonePage(PhonePage.Home);}
            GUI.enabled=prior;GUI.EndGroup();
        }
        void AppIcon(MatchSession match,Rect r,PhonePage page,string label,bool map)
        {
            bool active=match.ActivePhonePage==page;
            Rounded(r,12,active?new Color(.42f,.04f,.04f):new Color(.15f,.18f,.2f));
            var icon=new Rect(r.x+12,r.y+15,28,28);Outline(icon,Color.white,1.5f);
            if(map)
            {Fill(new Rect(icon.x+9,icon.y,1,28),Color.white);Fill(new Rect(icon.x+18,icon.y,1,28),Color.white);Fill(new Rect(icon.x,icon.y+14,28,1),Color.white);}
            else for(int i=0;i<3;i++) Fill(new Rect(icon.x+5,icon.y+6+i*7,18,1.5f),Color.white);
            GUI.Label(new Rect(r.x+50,r.y,80,r.height),label,hint);
            if(GUI.Button(r,GUIContent.none,GUIStyle.none)) {match.OpenPhonePage(page);scroll=Vector2.zero;}
        }
        void DrawTasksApp(MatchSession match,bool interactive)
        {
            GUI.Label(new Rect(34,108,288,24),match.IsTraining?"大厅自由拍照练习":"选择任务进行拍摄",body);
            if(!match.IsTraining && (match.RequiresLiveEvidence || match.World.Data.tasks.Any(t=>t.IncludesStreamer)))
                GUI.Label(new Rect(34,132,288,18),"主播拍摄任务：每场直播合计限一次",new GUIStyle(body){fontSize=12,normal={textColor=new Color(.75f,.72f,.70f)}});
            var tasks=match.IsTraining?new[]{match.SelectedTask}:match.Quests.Select(q=>new InvestigationTaskData{id=q.Id,title=q.Title,description=q.Description}).ToArray();
            float totalHeight=0;foreach(var q in tasks) totalHeight+=TaskHeight(q.title,q.description)+8;
            if(!match.IsTraining && match.RequiresLiveEvidence) totalHeight+=84;
            var view=new Rect(34,154,288,444);
            scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,272,Mathf.Max(444,totalHeight)));
            float y=0;int index=0;
            foreach(var q in tasks)
            {
                float height=TaskHeight(q.title,q.description);
                DrawTask(match,new Rect(0,y,272,height),q.id,(++index).ToString("00")+" "+q.title,q.description,match.Quests.Any(t=>t.Id==q.id && t.Complete),interactive);
                y+=height+8;
            }
            if(!match.IsTraining && match.RequiresLiveEvidence) DrawTask(match,new Rect(0,y,272,76),InvestigationData.LiveTask,"主播直播证据","每场最多一张。 "+match.StreamerPhotos+" / 3",match.StreamerPhotos>=3,interactive);
            GUI.EndScrollView();
            int total=match.Quests.Count+(match.RequiresLiveEvidence?1:0);
            int done=match.Quests.Count(q=>q.Complete)+(match.RequiresLiveEvidence && match.StreamerPhotos>=3?1:0);
            int segments=Mathf.Min(12,total);float segmentWidth=segments>0?(232f-6*(segments-1))/segments:0;
            for(int i=0;i<segments;i++)
            {
                var bar=new Rect(34+i*(segmentWidth+6),618,segmentWidth,7);Rounded(bar,3,new Color(.45f,.45f,.45f));
                float fill=Mathf.Clamp01((float)done/total*segments-i);
                if(fill>0) Rounded(new Rect(bar.x,bar.y,bar.width*fill,bar.height),Mathf.Min(3,bar.width*fill*.5f),red);
            }
            GUI.Label(new Rect(278,603,55,32),match.IsTraining?"练习":done+"/"+total,body);
            GUI.Label(new Rect(34,644,288,24),match.IsTraining?"大厅上传不计分":"剩余上传失误机会："+match.Score,new GUIStyle(body){fontSize=13});
        }
        float TaskHeight(string title,string description) => Mathf.Max(76,new GUIStyle(rowTitle){wordWrap=true}.CalcHeight(new GUIContent("00 "+title),206)+taskDescription.CalcHeight(new GUIContent(description??""),248)+20);
        void DrawMapApp(MatchSession match)
        {
            var map=match.HouseMap;int count=map.Floors.Count;
            for(int f=0;f<count;f++)
            {
                var r=new Rect(28+(f%6)*50,166+(f/6)*34,46,30);
                if(GUI.Button(r,(map.Floor==f?"•":"")+(f+1)+"F",new GUIStyle(body){alignment=TextAnchor.MiddleCenter})) {map.SelectFloor(f);mapScroll=Vector2.zero;}
            }
            var viewport=new Rect(24,238,308,290);var texture=map.Floors[map.Floor];
            float scale=Mathf.Min(288f/texture.width,270f/texture.height)*mapZoom;
            float width=Mathf.Max(288,texture.width*scale),height=Mathf.Max(270,texture.height*scale);
            mapScroll=GUI.BeginScrollView(viewport,mapScroll,new Rect(0,0,width,height));
            var plot=new Rect((width-texture.width*scale)*.5f,(height-texture.height*scale)*.5f,texture.width*scale,texture.height*scale);
            GUI.DrawTexture(plot,texture);
            foreach(var room in map.Rooms.Where(r=>r.floor==map.Floor))
            {
                var data=match.World.Data;
                var at=new Vector2(plot.x+(room.center.x+data.halfWidth+.5f)/data.WidthCells*plot.width,plot.yMax-(room.center.y+data.halfDepth+.5f)/data.DepthCells*plot.height);
                var label=new Rect(at.x-45,at.y-10,90,22);Fill(label,new Color(0,0,0,.75f));
                GUI.Label(label,room.computer?"主播电脑房":room.name,new GUIStyle(body){fontSize=12,alignment=TextAnchor.MiddleCenter});
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(28,540,50,24),"缩放",body);mapZoom=GUI.HorizontalSlider(new Rect(84,549,230,20),mapZoom,1,3);
            GUI.Label(new Rect(28,572,300,60),"绿：门 蓝：楼梯 粉：床\n紫：柜 灰：桌 橙：主播电脑（"+(match.Streamer.Computer.Floor+1)+"F）\n上为北；放大后可滚动查看房间。",new GUIStyle(body){fontSize=14});
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
            var wrappedTitle=new GUIStyle(rowTitle){wordWrap=true};
            float titleHeight=wrappedTitle.CalcHeight(new GUIContent(title),textWidth);
            GUI.Label(new Rect(r.x+12,r.y+6,textWidth,titleHeight),title,wrappedTitle);GUI.color=old;
            GUI.Label(new Rect(r.x+12,r.y+titleHeight+12,r.width-24,r.height-titleHeight-18),description??"",taskDescription);
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
            var task=match.PendingPhoto.taskId;
            string target=match.IsTraining?"大厅练习":task==InvestigationData.LiveTask?"主播直播证据":match.Quests.Find(q=>q.Id==task)?.Title??"拍摄时选定的任务";
            GUI.Label(new Rect(32,448,650,36),"上传目标："+target,new GUIStyle(body){wordWrap=true});
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
