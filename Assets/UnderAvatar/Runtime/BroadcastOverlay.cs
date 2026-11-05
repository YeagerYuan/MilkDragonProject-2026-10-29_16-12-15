using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnderAvatar
{
    public sealed class BroadcastOverlay : MonoBehaviour
    {
        [SerializeField] GameObject avatarPrefab;
        [SerializeField] string banner="超炫酷游戏主播不到{0}分不下播！";
        GUIStyle commentStyle,viewerStyle,bannerStyle,hintStyle,badgeStyle,huntTitle,huntSubtitle,huntTask,huntTaskTitle,timerStyle;
        Font bodyFont,titleFont;
        readonly List<Font> ownedFonts=new List<Font>();
        Texture2D disc,laugh,heart,timerRing,huntVignette;
        float huntChatStarted=-1;
        static readonly string[] AwayMessages={"主播你去哪了","我在等你呀主播","再不回来我就走了！！","？？？？？？","主播快回来我想你了","怎么突然没人了","主播人呢？","还在吗？快回来开播","我们还在等你回来","是不是离开电脑了？"};
        GameObject avatarStage;
        Camera avatarCamera;
        RenderTexture avatarTexture;
        int currentRound=-1;
        MinigameLevel currentLevel;
        float started;
        const int VisibleChatRows=6;
        const float ChatInterval=1.85f,RowHeight=38;
        const float InitialChatElapsed=.8f+ChatInterval*(VisibleChatRows-1)+.38f;
        static readonly string[] Names={"躺平蘑菇","每天都摆烂","夜航小灯","你真行","芝士云朵","橘子汽水","晚风观众","薄荷苏打","路过的猫","星星没睡"};
        static readonly string[] Messages={"主播你怎么这么菜？","你背后是什么？","你一定可以的！！","？？？？？","我赌你这把能一口气过","稳住，下一个就过去了","哈哈哈哈又撞了","来了来了！","别急，慢慢来","弹幕护体！","刚才是不是有脚步声","这波操作可以！","主播加油！","今晚一定能通关！！"};
        static readonly Color[] NameColors={new Color(.63f,.20f,.78f),new Color(.54f,.57f,.61f),new Color(.24f,.55f,.88f),new Color(.66f,.17f,.78f),new Color(.12f,.73f,.66f)};
        static readonly Dictionary<char,string[]> PixelGlyphs=new Dictionary<char,string[]>
        {
            {'S',new[]{"11111","10000","10000","11111","00001","00001","11111"}},
            {'C',new[]{"11111","10000","10000","10000","10000","10000","11111"}},
            {'O',new[]{"11111","10001","10001","10001","10001","10001","11111"}},
            {'R',new[]{"11110","10001","10001","11110","10100","10010","10001"}},
            {'E',new[]{"11111","10000","10000","11110","10000","10000","11111"}},
            {'0',new[]{"11111","10001","10011","10101","11001","10001","11111"}},
            {'1',new[]{"00100","01100","00100","00100","00100","00100","01110"}},
            {'2',new[]{"11111","00001","00001","11111","10000","10000","11111"}},
            {'3',new[]{"11111","00001","00001","01111","00001","00001","11111"}},
            {'4',new[]{"10001","10001","10001","11111","00001","00001","00001"}},
            {'5',new[]{"11111","10000","10000","11111","00001","00001","11111"}},
            {'6',new[]{"11111","10000","10000","11111","10001","10001","11111"}},
            {'7',new[]{"11111","00001","00010","00100","01000","01000","01000"}},
            {'8',new[]{"11111","10001","10001","11111","10001","10001","11111"}},
            {'9',new[]{"11111","10001","10001","11111","00001","00001","11111"}},
            {'/',new[]{"00001","00010","00010","00100","01000","01000","10000"}}
        };

        Font LoadFont(string resource,string fallback)
        {
            var result=Resources.Load<Font>(resource);if(result) return result;
            result=Font.CreateDynamicFontFromOSFont(fallback,24);ownedFonts.Add(result);return result;
        }
        void EnsureStyles()
        {
            if(commentStyle!=null) return;
            bodyFont=LoadFont("MainMenu/Fonts/Hangeuljaemin","Microsoft YaHei");
            titleFont=LoadFont("MainMenu/Fonts/AQingRanTi","KaiTi");
            commentStyle=new GUIStyle(GUI.skin.label){font=bodyFont,fontSize=20,richText=true,wordWrap=false,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),normal={textColor=Color.white}};
            viewerStyle=new GUIStyle(commentStyle){fontSize=23};
            bannerStyle=new GUIStyle(commentStyle){font=titleFont,fontSize=32,alignment=TextAnchor.MiddleCenter};
            hintStyle=new GUIStyle(commentStyle){fontSize=19,alignment=TextAnchor.MiddleCenter};
            badgeStyle=new GUIStyle(commentStyle){fontSize=18,alignment=TextAnchor.MiddleCenter};
            huntTitle=new GUIStyle(commentStyle){font=titleFont,fontSize=144,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.9f,.025f,0)}};
            huntSubtitle=new GUIStyle(commentStyle){fontSize=40,alignment=TextAnchor.MiddleCenter};
            timerStyle=new GUIStyle(huntSubtitle);
            huntTask=new GUIStyle(commentStyle){fontSize=24};
            huntTaskTitle=new GUIStyle(huntTask){font=titleFont,fontSize=32,normal={textColor=new Color(.9f,.025f,0)}};
            // Hunt announcements, objectives and livestream labels are informational.
            foreach(var style in new[]{commentStyle,viewerStyle,bannerStyle,hintStyle,badgeStyle,huntTitle,huntSubtitle,timerStyle,huntTask,huntTaskTitle})
            {
                style.hover=style.normal;
                style.active=style.normal;
                style.focused=style.normal;
            }
            timerRing=MakeTexture(128,(x,y)=> {float r=new Vector2(x-.5f,y-.5f).magnitude;return new Color(1,1,1,Mathf.Clamp01((.022f-Mathf.Abs(r-.465f))*128));});
            disc=MakeTexture(64,(x,y)=>new Color(1,1,1,Mathf.Clamp01((.5f-new Vector2(x-.5f,y-.5f).magnitude)*64)));
            laugh=MakeTexture(96,LaughPixel);heart=MakeTexture(96,HeartPixel);
        }
        static Texture2D MakeTexture(int size,System.Func<float,float,Color> paint)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=paint((x+.5f)/size,(y+.5f)/size);
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        static Color LaughPixel(float x,float y)
        {
            float radius=new Vector2(x-.5f,y-.5f).magnitude;
            Color c=radius<.45f?Color.Lerp(new Color(1,.64f,.04f),new Color(1,.88f,.18f),y):Color.clear;
            if(radius<.45f && ((x>.19f && x<.43f && Mathf.Abs(y-(.62f-Mathf.Abs(x-.31f)*.9f))<.025f) ||
                (x>.57f && x<.81f && Mathf.Abs(y-(.62f-Mathf.Abs(x-.69f)*.9f))<.025f))) c=new Color(.28f,.15f,.04f);
            float mouth=(x-.5f)/.245f;
            if(Mathf.Abs(mouth)<1 && y>.25f+.10f*mouth*mouth && y<.46f-.06f*mouth*mouth) c=y>.385f?Color.white:new Color(.42f,.12f,.06f);
            foreach(float eyeX in new[]{.16f,.84f})
                if(Mathf.Pow((x-eyeX)/.075f,2)+Mathf.Pow((y-.41f)/.14f,2)<1) c=new Color(.20f,.69f,1);
            return c;
        }
        static Color HeartPixel(float x,float y)
        {
            float a=(x-.5f)*2.5f,b=(y-.5f)*2.5f;
            float f=Mathf.Pow(a*a+b*b-1,3)-a*a*b*b*b;
            return f<0?new Color(1,.24f,.43f):Color.clear;
        }
        static void Fill(Rect rect,Color color) {var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;}
        static void TintTexture(Rect rect,Texture texture,Color color) {var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,texture);GUI.color=previous;}
        void Pill(Rect rect,Color color)
        {
            float cap=rect.height*.5f;
            // Draw only the outside half of each cap so translucent pixels never overlap.
            var previous=GUI.color;GUI.color=color;
            GUI.DrawTextureWithTexCoords(new Rect(rect.x,rect.y,cap,rect.height),disc,new Rect(0,0,.5f,1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax-cap,rect.y,cap,rect.height),disc,new Rect(.5f,0,.5f,1));
            GUI.color=previous;
            Fill(new Rect(rect.x+cap,rect.y,Mathf.Max(0,rect.width-rect.height),rect.height),color);
        }
        public void Draw(MatchSession match) {huntChatStarted=-1;Draw(match.Streamer.Controller.Level,match.Streamer.Controller.Fans);}
        public void DrawHunt(MatchSession match)
        {
            var brain=match.Streamer.Controller;
            DrawHunt(brain.RoamRemaining,brain.ReturnElapsed,match.Streamer.State==StreamerState.Returning,brain.Fans);
            if(match.IsStreamerPlayer && !match.Finished && Time.time<match.MessageUntil &&
                match.Message?.StartsWith(StreamerController.EmptySearchNoticePrefix)==true)
            {
                var previous=GUI.matrix;var previousColor=GUI.color;
                float scale=Screen.height/800f,width=Screen.width/scale;
                try
                {
                    GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                    float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01((match.MessageUntil-Time.time)/.5f));
                    GUI.color=new Color(previousColor.r,previousColor.g,previousColor.b,previousColor.a*alpha);
                    GUI.Label(new Rect(32,630,Mathf.Max(260,width-450),54),match.Message,huntTask);
                }
                finally {GUI.matrix=previous;GUI.color=previousColor;}
            }
        }
        // Both roles use the authoritative hunt clock, including replicated LAN snapshots.
        public static bool HuntAnnouncementVisible(float remaining,float returnElapsed,bool returning)
            => returning?returnElapsed<StreamerController.ReturnAnnouncementSeconds:remaining>StreamerController.HuntSeconds-3;

        void DrawHuntTimer(float remaining,bool returning)
        {
            TintTexture(new Rect(20,20,68,68),timerRing,Color.white);
            GUI.Label(new Rect(20,20,68,68),Mathf.CeilToInt(returning?0:Mathf.Max(0,remaining)).ToString(),timerStyle);
        }
        void DrawHuntAnnouncement(float width,float remaining,float returnElapsed,bool returning,bool reporter)
        {
            float duration=returning?StreamerController.ReturnAnnouncementSeconds:3;
            float elapsed=returning?returnElapsed:StreamerController.HuntSeconds-remaining;
            float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.5f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((duration-elapsed)/.65f));
            var previousColor=GUI.color;
            try
            {
                GUI.color=new Color(previousColor.r,previousColor.g,previousColor.b,previousColor.a*alpha);
                GUI.Label(new Rect(0,280,width,160),reporter?(returning?"搜捕结束":"搜捕开始"):(returning?"抓捕结束":"抓捕开始"),huntTitle);
                string subtitle=reporter?(returning?"恭喜你，没有被主播抓住":"在30秒内躲避主播的追捕"):(returning?"返回电脑开播，防止粉丝流失":"在30秒内抓到狗仔");
                GUI.Label(new Rect(0,438,width,64),subtitle,huntSubtitle);
            }
            finally {GUI.color=previousColor;}
        }
        public bool DrawReporterHunt(MatchSession match)
        {
            if(match.IsTraining || match.Finished || match.Streamer.State==StreamerState.Streaming) return false;
            bool returning=match.Streamer.State==StreamerState.Returning;
            var brain=match.Streamer.Controller;
            bool announcement=HuntAnnouncementVisible(brain.RoamRemaining,brain.ReturnElapsed,returning);
            EnsureStyles();
            var previous=GUI.matrix;float scale=Screen.height/800f,width=Screen.width/scale;
            try
            {
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                if(!returning)
                {
                    if(!huntVignette) huntVignette=MakeTexture(256,(x,y)=>
                    {
                        float edge=Mathf.Max(Mathf.Abs(x-.5f)*2,Mathf.Abs(y-.5f)*2);
                        return new Color(.38f,0,0, Mathf.SmoothStep(0,.25f,Mathf.InverseLerp(.55f,1,edge)));
                    });
                    TintTexture(new Rect(0,0,width,800),huntVignette,Color.white);
                }
                DrawHuntTimer(brain.RoamRemaining,returning);
                if(announcement) DrawHuntAnnouncement(width,brain.RoamRemaining,brain.ReturnElapsed,returning,true);
            }
            finally {GUI.matrix=previous;}
            return announcement;
        }
        public void DrawHunt(float remaining,float returnElapsed,bool returning,int fans)
        {
            EnsureStyles();
            if(huntChatStarted<0) huntChatStarted=Time.unscaledTime-InitialChatElapsed;
            var previous=GUI.matrix;float scale=Screen.height/800f,width=Screen.width/scale;
            try
            {
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                DrawHuntTimer(remaining,returning);
                float chatElapsed=Time.unscaledTime-huntChatStarted;
                DrawComments(new Rect(width-352,548,336,VisibleChatRows*RowHeight),chatElapsed,0,true);
                Pill(new Rect(width-352,508,318,36),new Color(.06f,.12f,.17f,.90f));
                DrawViewers(new Vector2(width-338,515));
                int viewers=Mathf.RoundToInt(fans*5.9f);
                if(!returning) viewers=Mathf.Max(0,viewers+Mathf.RoundToInt(Mathf.Sin(chatElapsed*.17f)*65));
                GUI.Label(new Rect(width-308,508,262,36),"在线观众： "+viewers.ToString("N0"),viewerStyle);
                bool announcement=HuntAnnouncementVisible(remaining,returnElapsed,returning);
                string task=returning?"返回电脑开播，防止粉丝流失":"在30秒内抓到狗仔";
                if(announcement)
                {
                    DrawHuntAnnouncement(width,remaining,returnElapsed,returning,false);
                }
                else
                {
                    GUI.Label(new Rect(32,690,450,42),returning?"当前任务":"当前目标",huntTaskTitle);
                    GUI.Label(new Rect(32,734,650,40),task,huntTask);
                }
            }
            finally {GUI.matrix=previous;}
        }
        public void Draw(MinigameLevel level,int fans)
        {
            var previous=GUI.matrix;
            float scale=Screen.height/800f;
            try {GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));DrawFrame(level,fans,Screen.width/scale);}
            finally {GUI.matrix=previous;}
        }
        void DrawFrame(MinigameLevel level,int fans,float width)
        {
            EnsureStyles();
            if(currentLevel!=level || currentRound!=level.LevelNumber) {currentLevel=level;currentRound=level.LevelNumber;started=Time.unscaledTime;}
            float elapsed=Time.unscaledTime-started;
            Fill(new Rect(-2000,-2000,6000,6000),new Color(.204f,.325f,.40f));
            DrawGame(level,new Rect(0,0,width,754));
            // Keep the footer behind the last chat row so it does not obscure the comment.
            Fill(new Rect(0,754,width,46),new Color(.18f,.165f,.155f));
            GUI.Label(new Rect(310,755,width-340,44),string.Format(banner,ChineseNumber(level.TargetScore)),bannerStyle);
            PixelText("SCORE",new Vector2(60,36),4);
            PixelText(level.Score+"/"+level.TargetScore,new Vector2(60,78),4);
            // Match the hunt chat panel and prefill it so all six rows are visible immediately.
            float chatElapsed=elapsed+InitialChatElapsed;
            DrawComments(new Rect(width-352,548,336,VisibleChatRows*RowHeight),chatElapsed,level.LevelNumber);
            Pill(new Rect(width-352,508,254,36),new Color(.06f,.12f,.17f,.90f));
            DrawViewers(new Vector2(width-338,515));
            int viewers=Mathf.Max(1,fans*6-500+Mathf.FloorToInt(Mathf.Sin(elapsed*.17f)*75));
            GUI.Label(new Rect(width-308,508,200,36),"在线观众： "+viewers.ToString("N0"),viewerStyle);
            DrawReactions(new Rect(width-90,610,82,144),elapsed);
            EnsureAvatar();if(avatarStage) avatarStage.SetActive(true);
            if(avatarTexture) GUI.DrawTexture(new Rect(-18,454,356,356),avatarTexture,ScaleMode.ScaleToFit,true);
        }
        void DrawGame(MinigameLevel level,Rect rect)
        {
            GUI.BeginGroup(rect);
            for(int i=0;i<6;i++)
            {
                float x=(i*257+130)%rect.width,y=86+i%3*63;
                Fill(new Rect(x,y,140,24),new Color(.32f,.46f,.54f));Fill(new Rect(x+32,y-15,65,15),new Color(.32f,.46f,.54f));
            }
            foreach(var pipe in level.Pipes)
            {
                float x=pipe.X*rect.width,w=MinigameLevel.PipeWidth*rect.width;
                float upper=(1-pipe.GapCenter-level.PipeGap*.5f)*rect.height,lower=(1-pipe.GapCenter+level.PipeGap*.5f)*rect.height;
                var green=new Color(.45f,.74f,.49f);var rim=new Color(.55f,.87f,.57f);
                Fill(new Rect(x,0,w,upper),green);Fill(new Rect(x,lower,w,rect.height-lower),green);
                Fill(new Rect(x+8,0,11,upper),rim);Fill(new Rect(x+8,lower,11,rect.height-lower),rim);
                Fill(new Rect(x-7,upper-21,w+14,21),rim);Fill(new Rect(x-7,lower,w+14,21),rim);
            }
            float bx=MinigameLevel.BirdX*rect.width,by=(1-level.BirdHeight)*rect.height;
            float bw=MinigameLevel.BirdRadius*rect.width*2,bh=MinigameLevel.BirdRadius*rect.height*2;
            Fill(new Rect(bx-bw*.5f,by-bh*.5f,bw,bh),new Color(1,.84f,.35f));
            Fill(new Rect(bx-bw*.42f,by,bw*.45f,bh*.45f),new Color(1,.63f,.22f));
            Fill(new Rect(bx+bw*.1f,by-bh*.38f,8,9),Color.white);Fill(new Rect(bx+bw*.18f,by-bh*.25f,4,5),Color.black);
            Fill(new Rect(bx+bw*.4f,by,14,6),new Color(.91f,.40f,.17f));
            if(!level.Running && !level.Complete)
            {
                var text=level.Crashed?"碰撞！分数清零 · 按空格 / 左键重试":"空格 / 左键拍翅 · 达到 "+level.TargetScore+" 分后下播";
                Pill(new Rect(rect.width*.5f-250,300,500,52),new Color(.055f,.11f,.15f,.84f));GUI.Label(new Rect(rect.width*.5f-240,300,480,52),text,hintStyle);
            }
            GUI.EndGroup();
        }
        void PixelText(string text,Vector2 position,float scale)
        {
            foreach(char c in text)
            {
                if(PixelGlyphs.TryGetValue(c,out var glyph))
                    for(int y=0;y<7;y++) for(int x=0;x<5;x++) if(glyph[y][x]=='1') Fill(new Rect(position.x+x*scale,position.y+y*scale,scale,scale),Color.white);
                position.x+=6*scale;
            }
        }
        void DrawComments(Rect viewport,float elapsed,int round,bool away=false)
        {
            GUI.BeginGroup(viewport);
            float rowHeight=away?38:RowHeight;
            float clock=Mathf.Max(0,elapsed-.8f);int latest=Mathf.FloorToInt(clock/ChatInterval);
            float enter=Mathf.SmoothStep(0,1,Mathf.Clamp01((clock-latest*ChatInterval)/.38f));
            for(int index=Mathf.Max(0,latest-Mathf.CeilToInt(viewport.height/rowHeight));index<=latest;index++)
            {
                float y=viewport.height-rowHeight-(latest-index)*rowHeight+(1-enter)*rowHeight;
                if(y<-rowHeight || y>=viewport.height) continue;
                var tint=NameColors[index%NameColors.Length];float alpha=index==latest?enter:Mathf.Clamp01((y+rowHeight)/rowHeight);
                var old=GUI.color;GUI.color=new Color(old.r,old.g,old.b,old.a*alpha);
                Pill(new Rect(0,y,viewport.width,36),new Color(.065f,.12f,.15f,.88f));
                TintTexture(new Rect(8,y+5,27,27),disc,tint);
                GUI.Label(new Rect(8,y+4,27,27),(2+(index*7)%19).ToString(),badgeStyle);
                string name=Names[(index*3+round)%Names.Length],body=away?AwayMessages[index%AwayMessages.Length]:Messages[(index*5+round)%Messages.Length];
                GUI.Label(new Rect(40,y+1,viewport.width-48,35),"<color=#"+ColorUtility.ToHtmlStringRGB(tint)+">"+name+"</color> "+body,commentStyle);
                GUI.color=old;
            }
            GUI.EndGroup();
        }
        void DrawViewers(Vector2 p)
        {
            foreach(float x in new[]{0,10,20}) {TintTexture(new Rect(p.x+x,p.y,8,8),disc,Color.white);TintTexture(new Rect(p.x+x-2,p.y+9,12,9),disc,Color.white);}
        }
        void DrawReactions(Rect viewport,float elapsed)
        {
            GUI.BeginGroup(viewport);int latest=Mathf.FloorToInt(elapsed/.43f);
            for(int i=Mathf.Max(0,latest-7);i<=latest;i++)
            {
                float age=elapsed-i*.43f,lifetime=2.4f;if(age<0 || age>=lifetime) continue;
                float t=age/lifetime,size=27+(i%3)*4;
                float x=12+(i*19)%34+Mathf.Sin(age*3+i)*9,y=viewport.height-size-t*(viewport.height-8);
                float alpha=Mathf.Clamp01(age/.12f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,1,t)));
                TintTexture(new Rect(x,y,size,size),i%5==0?heart:laugh,new Color(1,1,1,alpha));
            }
            GUI.EndGroup();
        }
        void EnsureAvatar()
        {
            if(avatarStage) return;
            var prefab=avatarPrefab?avatarPrefab:Resources.Load<GameObject>("BroadcastAvatar");if(!prefab) return;
            avatarStage=new GameObject("Broadcast VTuber render stage");avatarStage.transform.SetParent(transform,false);avatarStage.transform.position=Vector3.one*1000;
            var cameraObject=new GameObject("VTuber camera");cameraObject.transform.SetParent(avatarStage.transform,false);cameraObject.transform.localPosition=Vector3.back;
            avatarCamera=cameraObject.AddComponent<Camera>();avatarCamera.orthographic=true;avatarCamera.orthographicSize=.5f;avatarCamera.nearClipPlane=.1f;avatarCamera.farClipPlane=3;
            avatarCamera.clearFlags=CameraClearFlags.SolidColor;avatarCamera.backgroundColor=Color.clear;avatarCamera.cullingMask=1<<5;avatarCamera.allowHDR=false;avatarCamera.allowMSAA=false;avatarCamera.depth=-50;
            avatarTexture=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32){name="Live VTuber",filterMode=FilterMode.Bilinear};avatarTexture.Create();avatarCamera.targetTexture=avatarTexture;
            var canvasObject=new GameObject("VTuber canvas",typeof(RectTransform),typeof(Canvas));canvasObject.transform.SetParent(avatarStage.transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=avatarCamera;
            var canvasRect=canvasObject.GetComponent<RectTransform>();canvasRect.sizeDelta=Vector2.one*640;canvasRect.localScale=Vector3.one/640;
            var avatar=Instantiate(prefab,canvasObject.transform,false);var rect=avatar.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.one*.5f;rect.pivot=Vector2.one*.5f;rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Vector2.one*640;
            foreach(var child in avatarStage.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
        }
        void LateUpdate()
        {
            if(!avatarStage) return;
            var app=GetComponent<PrototypeApp>();
            if(app) avatarStage.SetActive(app.Session && !app.Session.IsTraining && app.Session.IsStreamerPlayer && app.Session.Streamer.State==StreamerState.Streaming);
        }
        static string ChineseNumber(int number)
        {
            const string digits="零一二三四五六七八九";
            if(number<10) return digits[Mathf.Max(0,number)].ToString();
            if(number<100) return (number/10==1?"":digits[number/10].ToString())+"十"+(number%10==0?"":digits[number%10].ToString());
            return number.ToString();
        }
        void OnDestroy()
        {
            if(avatarCamera) avatarCamera.targetTexture=null;
            if(avatarStage) PrototypeVisuals.Release(avatarStage);
            if(avatarTexture) {avatarTexture.Release();PrototypeVisuals.Release(avatarTexture);}
            foreach(var texture in new[]{disc,laugh,heart,timerRing,huntVignette}) if(texture) PrototypeVisuals.Release(texture);
            foreach(var item in ownedFonts) PrototypeVisuals.Release(item);
        }
    }
}
