using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        const float Width=1280, Height=800;
        PrototypeApp app;
        GUISkin skin;
        GUIStyle title,subtitle,section,small,notice;
        readonly List<Texture2D> textures=new List<Texture2D>();
        Font font;
        Vector2 questScroll;
        bool showObserver=true;
        Vector2 debugScroll;
        string joinAddress="127.0.0.1",portText="7777";
        int hostRole;
        MatchSession chatMatch;
        int chatRound;
        float chatStarted;
        GUIStyle chatName,chatText,badge,scoreText;
        static readonly string[] ChatNames={"夜航小灯","芝士云朵","晚风观众","路过的猫","薄荷苏打","星星没睡","小岛来信","月亮打卡","橘子汽水","躺平蘑菇","今天早睡","云端纸鸢"};
        static readonly string[] ChatTexts={"来了来了，今晚也要通关！","这次手感不错呀","稳住，下一根就过去了","刚才是不是听见门响了？","别急，跟着节奏飞","我猜你这把能一口气过","笑死，又差那么一点点","弹幕护体，主播加油","有人在看你的背后吗……","先把这关过了再说","今天的直播间好安静","别光看弹幕，看管道！","这波操作有点东西","再试一次，这把一定行","给小鸟一点飞起来的信心","保持节奏，不要连按太快","新观众报到，晚上好","主播下播记得早点回来"};
        Color accent=new Color(.31f,.84f,.78f);
        public static float Scale => Mathf.Min(Screen.width/Width,Screen.height/Height);
        public static Vector2 Offset => new Vector2((Screen.width-Width*Scale)/2,(Screen.height-Height*Scale)/2);
        public static Vector2 ToGui(Vector2 screen) => (new Vector2(screen.x,Screen.height-screen.y)-Offset)/Scale;
        public void Initialize(PrototypeApp prototype) { app=prototype; }

        Texture2D Texture(Color color)
        {
            var texture=new Texture2D(1,1); texture.SetPixel(0,0,color); texture.Apply();
            textures.Add(texture); return texture;
        }

        void EnsureStyles()
        {
            if(skin) return;
            skin=Instantiate(GUI.skin);
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Microsoft YaHei UI","SimHei","Arial"},18);
            skin.font=font;
            skin.label.fontSize=16;
            skin.label.normal.textColor=new Color(.85f,.90f,.94f);
            skin.label.wordWrap=true;
            skin.label.padding=new RectOffset(0,0,2,2);
            skin.button.fontSize=16;
            skin.button.alignment=TextAnchor.MiddleLeft;
            skin.button.padding=new RectOffset(12,8,8,8);
            skin.button.margin=new RectOffset(0,0,3,3);
            skin.button.normal.background=Texture(new Color(.15f,.20f,.25f));
            skin.button.hover.background=Texture(new Color(.22f,.32f,.37f));
            skin.button.active.background=Texture(new Color(.20f,.46f,.45f));
            skin.button.normal.textColor=Color.white; skin.button.hover.textColor=Color.white; skin.button.active.textColor=Color.white;
            skin.textField.fontSize=15; skin.textArea.fontSize=15;
            skin.textField.padding=new RectOffset(8,8,7,7); skin.textArea.padding=new RectOffset(8,8,7,7);
            foreach(var style in new[]{skin.textField,skin.textArea})
            {
                style.normal.background=Texture(new Color(.06f,.09f,.12f));
                style.focused.background=Texture(new Color(.08f,.15f,.18f));
                style.normal.textColor=Color.white; style.focused.textColor=Color.white;
            }
            skin.toggle.fontSize=15; skin.toggle.normal.textColor=Color.white;
            title=new GUIStyle(skin.label){fontSize=26,fontStyle=FontStyle.Bold};
            subtitle=new GUIStyle(skin.label){fontSize=12,normal={textColor=new Color(.51f,.64f,.71f)}};
            section=new GUIStyle(skin.label){fontSize=13,fontStyle=FontStyle.Bold,normal={textColor=accent},margin=new RectOffset(0,0,10,4)};
            small=new GUIStyle(skin.label){fontSize=13,normal={textColor=new Color(.62f,.72f,.78f)}};
            notice=new GUIStyle(skin.label){fontSize=16,alignment=TextAnchor.MiddleLeft,wordWrap=true};
            chatName=new GUIStyle(skin.label){fontSize=13,fontStyle=FontStyle.Bold,normal={textColor=accent}};
            chatText=new GUIStyle(skin.label){fontSize=14,wordWrap=true,normal={textColor=new Color(.78f,.84f,.90f)}};
            badge=new GUIStyle(skin.label){fontSize=10,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
            scoreText=new GUIStyle(title){fontSize=30};
        }

        void Fill(Rect rect,Color color)
        {
            Color before=GUI.color; GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=before;
        }

        bool Button(string text,bool primary=false)
        {
            Color before=GUI.backgroundColor;
            if(primary) GUI.backgroundColor=accent;
            bool pressed=GUILayout.Button(text,GUILayout.Height(37),GUILayout.MinWidth(0)); GUI.backgroundColor=before; return pressed;
        }

        void OnGUI()
        {
            if(!app) return;
            EnsureStyles();
            GUISkin previousSkin=GUI.skin;
            bool previousEnabled=GUI.enabled;
            GUI.skin=skin;
            Matrix4x4 matrix=GUI.matrix;
            try
            {
                GUI.matrix=Matrix4x4.TRS(Offset,Quaternion.identity,Vector3.one*Scale);
                if(app.Session) DrawPreview();
                else DrawLauncher();
            }
            finally { GUI.matrix=matrix; GUI.skin=previousSkin; GUI.enabled=previousEnabled; }
        }
        void DrawLauncher()
        {
            Fill(new Rect(-2000,-2000,6000,6000),new Color(.025f,.045f,.065f));
            Fill(new Rect(160,70,960,665),new Color(.045f,.075f,.10f));
            GUILayout.BeginArea(new Rect(195,95,890,615));
            GUILayout.Label("皮套之下  /  测试与双人对战",title);
            GUILayout.Space(12);
            var lan=app.Lan;
            if(lan.Active)
            {
                GUILayout.Label(lan.IsHost?"局域网房间 · 房主":"局域网房间 · 加入者",section);
                GUILayout.Label(lan.Status,notice,GUILayout.Height(48));
                if(lan.IsHost) GUILayout.Label("房主 IP："+lan.LocalAddresses+"    端口："+lan.Port,notice);
                if(lan.Phase==LanPhase.Lobby)
                {
                    GUILayout.Label("关卡："+lan.MapName,notice);
                    GUILayout.Label("你的角色："+(lan.LocalRole==TestRole.Streamer?"主播":"狗仔")+"    对方："+(lan.PeerJoined?(lan.LocalRole==TestRole.Streamer?"狗仔":"主播"):"尚未加入"),notice);
                    GUILayout.Space(18);
                    GUILayout.Label("你："+(lan.LocalReady?"已准备":"未准备")+"    对方："+(lan.RemoteReady?"已准备":"未准备"),notice);
                    GUI.enabled=lan.PeerJoined;
                    if(Button(lan.LocalReady?"取消准备":"我已准备",true)) lan.SetReady(!lan.LocalReady);
                    GUI.enabled=lan.CanStart;
                    if(lan.IsHost && Button("进入教学大厅",true)) lan.StartMatch();
                    GUI.enabled=true;
                }
                GUILayout.Space(18);
                if(Button("离开房间 / 取消连接")) app.ReturnToMenu();
            }
            else
            {
                GUILayout.Label("AI 对战 · 选择角色后先进入教学大厅",section);
                GUILayout.BeginHorizontal();
                if(Button("我是狗仔 · 对手是 AI 主播",true)) app.StartSingle(TestRole.Reporter);
                if(Button("我是主播 · 对手是 AI 狗仔",true)) app.StartSingle(TestRole.Streamer);
                GUILayout.EndHorizontal();
                GUILayout.Space(18);
                GUILayout.Label("局域网双人对战",section);
                GUILayout.Label("两台电脑使用相同版本。一人创建房间，另一人输入房主 IP 加入。",notice);
                GUILayout.BeginHorizontal();
                GUILayout.Label("房主角色",GUILayout.Width(100));
                hostRole=GUILayout.SelectionGrid(hostRole,new[]{"狗仔","主播"},2,GUILayout.Height(38));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("端口",GUILayout.Width(100));portText=GUILayout.TextField(portText,5,GUILayout.Height(34));
                GUILayout.EndHorizontal();
                bool portValid=int.TryParse(portText,out int port)&&port>=1024&&port<=65535;
                GUI.enabled=portValid;
                if(Button("创建房间",true)) lan.CreateRoom((TestRole)hostRole,port);
                GUI.enabled=true;
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                GUILayout.Label("房主 IPv4",GUILayout.Width(100));joinAddress=GUILayout.TextField(joinAddress,64,GUILayout.Height(34));
                GUILayout.EndHorizontal();
                GUI.enabled=portValid&&!string.IsNullOrWhiteSpace(joinAddress);
                if(Button("加入房间",true)) lan.JoinRoom(joinAddress,port);
                GUI.enabled=true;
                GUILayout.Label(portValid?"双方准备后先进入明亮教学大厅；大厅内再次准备，再开始正式对局。":"端口应为 1024–65535。",small);
                GUILayout.Space(10);
                GUILayout.Label(app.StartupError??app.Status??"单人模式中，另一角色由 AI 控制。",notice,GUILayout.Height(58));
                if(Button("退出测试")) app.QuitTest();
            }
            GUILayout.EndArea();
        }

        void DrawPreview()
        {
            var match=app.Session;
            if(!match) return;
            if(DocumentManager.Instance && DocumentManager.Instance.IsOpen && DocumentManager.Instance.Owner==match) return;
            if(match.IsTraining) { DrawTrainingHall(match);return; }
            if(!match.IsStreamerPlayer && match.Reporter.Hidden)
            {
                Fill(new Rect(-2000,-2000,6000,6000),new Color(.005f,.008f,.012f,.98f));
                GUI.Label(new Rect(430,330,500,50),match.Reporter.HidingPlace?.Data.type==ItemType.Bed?"你躲在床下":"你躲在衣柜里",title);
                GUI.Label(new Rect(430,385,500,50),"保持安静。无法使用手机，按 E 离开。",notice);
            }
            Fill(new Rect(20,16,800,88),new Color(.035f,.055f,.075f,.9f));
            GUI.Label(new Rect(34,23,750,28),"皮套之下  /  "+(match.IsNetwork
                ? "局域网对战 · "+(match.IsStreamerPlayer?"主播":"狗仔")
                : match.IsStreamerPlayer?"主播测试 · AI 狗仔":"狗仔测试 · AI 主播"),section);
            GUI.Label(new Rect(34,52,770,22),match.IsStreamerPlayer
                ? "空格 / 左键拍翅 · 下播后 WASD 移动 · Shift 静步 · Ctrl 蹲下 · E 门 / 开播 · Tab 调试"
                : "WASD 行走有声 · Shift 静步无声 · 按住 Ctrl 蹲下 · E 交互 · F 手机 · Tab 调试",small);
            GUI.Label(new Rect(34,78,770,22),"当前位置 "+((match.IsStreamerPlayer?match.Streamer.Floor:match.Reporter.Floor)+1)+"F  ·  主播电脑 "+(match.Streamer.Computer.Floor+1)+"F  ·  走楼梯上下楼  ·  Esc 退出",small);
            if(match.IsNetwork)
            {
                var net=app.Lan;
                Fill(new Rect(20,104,800,26),new Color(.035f,.055f,.075f,.9f));
                GUI.Label(new Rect(34,105,770,24),"本机 "+Mathf.RoundToInt(net.LocalFps)+" FPS · 房主 "+
                    Mathf.RoundToInt(net.IsHost?net.LocalFps:net.HostFps)+" FPS · 网络往返 "+
                    (net.RoundTripMs>0?Mathf.RoundToInt(net.RoundTripMs)+" ms":"测量中"),small);
            }
            if(match.IsStreamerPlayer) DrawStreamerPlayer(match);
            else if(!match.Reporter.Hidden)
            {
                if(match.Reporter.PhoneOpen) DrawPhone(match);
                else
                {
                    GUI.Label(new Rect(630,390,20,20),"+");
                    var held=match.TaskWorld?match.TaskWorld.Held:null;
                    var movable=match.TaskWorld && !held?match.TaskWorld.AimProp():null;
                    if(held) GUI.Label(new Rect(440,470,520,36),"手持："+held.Data.name+" · E 放下 · 绿框可放 / 红框受阻",notice);
                    else if(movable && movable.Data.portable) GUI.Label(new Rect(440,470,450,36),"E 拿起："+movable.Data.name,notice);
                    var target=match.Reporter.Documents?match.Reporter.Documents.Target:null;
                    if(target)
                    {
                        Fill(new Rect(440,428,400,36),new Color(.035f,.055f,.075f,.94f));
                        GUI.Label(new Rect(452,432,376,30),"鼠标左键阅读："+target.DisplayName,notice);
                    }
                }
            }
            if(showObserver && !match.IsStreamerPlayer && !match.IsNetwork) DrawStreamerObserver(match);
            if(match.DebugOpen && !match.Finished)
            { if(match.IsNetwork) DrawNetworkMenu();else DrawDebug(match); }
            if(match.IsNetwork && app.Lan.Phase==LanPhase.Loading)
            {
                Fill(new Rect(290,310,700,130),new Color(.025f,.05f,.08f,.98f));
                GUI.Label(new Rect(320,345,640,60),"正在同步关卡，等待另一位玩家加载…",notice);
            }
            if(Time.time<match.FlashUntil)
                Fill(new Rect(-2000,-2000,6000,6000),new Color(1,1,1,.13f*Mathf.Clamp01((match.FlashUntil-Time.time)/.3f)));
            if(Time.time<match.MessageUntil)
            {
                bool broadcasting=match.IsStreamerPlayer && match.Streamer.State==StreamerState.Streaming;
                float y=broadcasting?648:744,width=broadcasting?960:1240;
                Fill(new Rect(20,y,width,42),new Color(.035f,.055f,.075f,.94f));
                GUI.Label(new Rect(32,y+6,width-24,32),match.Message,notice);
            }
            if(match.Finished)
            {
                Fill(new Rect(-2000,-2000,6000,6000),new Color(0,0,0,.78f));
                Fill(new Rect(360,270,560,250),new Color(.055f,.10f,.13f));
                GUI.Label(new Rect(385,300,510,80),match.Result,title);
                if(GUI.Button(new Rect(385,410,235,50),match.IsNetwork?"返回房间":"返回大厅再来一局")) app.RestartGame();
                if(GUI.Button(new Rect(635,410,235,50),"返回角色选择")) app.ReturnToMenu();
            }
        }
        void DrawNetworkMenu()
        {
            Fill(new Rect(390,280,500,230),new Color(.025f,.05f,.08f,.98f));
            GUI.Label(new Rect(420,307,440,60),"局域网对战仍在进行。\n参数和 AI 观察窗在联机中禁用。",notice);
            if(GUI.Button(new Rect(420,385,440,40),"继续对战（Tab）"))
            { app.Session.DebugOpen=false;app.Session.Reporter.SetCursor(false); }
            if(GUI.Button(new Rect(420,443,440,40),"离开房间并返回角色选择")) app.ReturnToMenu();
        }

        void DrawTrainingHall(MatchSession match)
        {
            var net=app.Lan;
            bool single=!match.IsNetwork;
            bool ready=single?app.SingleReady:net.LocalReady;
            string preparation=single
                ? "你："+(ready?"已准备":"未准备")+"  /  AI：已就绪"
                : "你："+(ready?"已准备":"未准备")+"  /  对方："+(net.RemoteReady?"已准备":"未准备");
            if(!match.IsStreamerPlayer && match.Reporter.Hidden)
            {
                Fill(new Rect(-2000,-2000,6000,6000),new Color(.005f,.008f,.012f,.98f));
                GUI.Label(new Rect(430,330,520,50),match.Reporter.HidingPlace?.Data.type==ItemType.Bed?"躲藏练习：床下":"躲藏练习：衣柜",title);
                GUI.Label(new Rect(430,385,520,50),"按 E 离开。大厅没有抓捕或胜负。",notice);
            }
            else
            {
                GUI.Label(new Rect(630,390,24,24),"+",notice);
                if(!match.IsStreamerPlayer && match.Reporter.PhoneOpen)
                {
                    Outline(new Rect(300,160,650,470),accent,2);
                    Fill(new Rect(320,640,620,52),new Color(.035f,.055f,.075f,.9f));
                    GUI.Label(new Rect(338,651,585,35),"手机练习 · 左键 / 空格拍照 · F 收起 · 无任务、无计分",notice);
                }
            }
            Fill(new Rect(20,16,800,118),new Color(.035f,.055f,.075f,.93f));
            GUI.Label(new Rect(36,24,760,32),"教学大厅  /  "+(match.IsStreamerPlayer?"主播":"狗仔")+(single?" · AI 对战":" · 联机对战"),title);
            GUI.Label(new Rect(36,64,760,25),"WASD 移动 · Shift 静步 · Ctrl 蹲下 · E 交互 · Tab 大厅菜单",small);
            GUI.Label(new Rect(36,94,760,25),"自由交互，无小游戏、计时或胜负  ·  "+(single?"准备后开始 AI 对战":"网络 "+Mathf.RoundToInt(net.RoundTripMs)+" ms"),small);
            Fill(new Rect(840,16,420,118),new Color(.035f,.055f,.075f,.93f));
            GUI.Label(new Rect(858,26,385,28),"正式对局准备",section);
            GUI.Label(new Rect(858,62,385,52),preparation+"\n"+(single?"按 Tab 准备并开始正式 AI 对战":"按 Tab 准备，房主开始正式对局"),small);
            if(Time.time<match.FlashUntil) Fill(new Rect(-2000,-2000,6000,6000),new Color(1,1,1,.13f*Mathf.Clamp01((match.FlashUntil-Time.time)/.3f)));
            if(Time.time<match.MessageUntil)
            {
                Fill(new Rect(20,744,1240,42),new Color(.035f,.055f,.075f,.94f));GUI.Label(new Rect(36,751,1208,32),match.Message,notice);
            }
            if(!single && net.Phase==LanPhase.Loading)
            {
                Fill(new Rect(290,310,700,130),new Color(.025f,.05f,.08f,.98f));GUI.Label(new Rect(320,345,640,60),"正在同步大厅，等待另一位玩家加载…",notice);return;
            }
            if(!match.DebugOpen) return;
            Fill(new Rect(355,208,570,390),new Color(.025f,.05f,.08f,.98f));
            GUI.Label(new Rect(385,232,510,35),"准备进入正式对局",title);
            GUI.Label(new Rect(385,282,510,55),preparation+"\n正式对局将重新初始化所有状态。",notice);
            if(GUI.Button(new Rect(385,350,510,42),ready?"取消准备":"准备进入对局"))
            { if(single) app.SetSingleReady(!ready);else net.SetReady(!ready); }
            if(single || net.IsHost)
            {
                GUI.enabled=single?app.CanStartSingleMatch:net.CanStart;
                if(GUI.Button(new Rect(385,402,510,42),single?"开始正式 AI 对战":"开始正式对局"))
                { if(single) app.StartSingleMatch();else net.StartMatch(); }
                GUI.enabled=true;
            }
            else GUI.Label(new Rect(385,410,510,32),"双方准备后，等待房主开始。",small);
            if(GUI.Button(new Rect(385,454,510,42),"继续练习（Tab）")) { match.DebugOpen=false;match.Reporter.SetCursor(false); }
            if(GUI.Button(new Rect(385,506,510,42),single?"返回角色选择":"离开房间")) app.ReturnToMenu();
        }

        void DrawPhone(MatchSession match)
        {
            Fill(new Rect(946,94,318,638),new Color(.025f,.04f,.055f,.98f));
            GUILayout.BeginArea(new Rect(966,108,278,608));
            GUILayout.Label("取证终端 · "+(match.PhoneMenuOpen?"任务与上传":"相机"),section);
            GUILayout.Label("剩余失误次数  "+match.Score+" / "+match.Settings.maxScore,notice);
            GUILayout.Label(match.Streamer.State==StreamerState.Streaming?"主播正在直播":"主播已离开电脑，注意安全",small);
            questScroll=GUILayout.BeginScrollView(questScroll,GUILayout.Height(220));
            foreach(var quest in match.Quests)
            {
                string label=(quest.Complete?"✓ ":match.SelectedTaskId==quest.Id?"▶ ":"□ ")+(quest.Title??quest.Description);
                if(match.PhoneMenuOpen)
                { if(GUILayout.Button(label,GUILayout.MinHeight(36))) match.QueuePhoneAction(quest.Id); }
                else GUILayout.Label(label,small);
            }
            if(match.RequiresLiveEvidence)
            {
                string live=(match.SelectedTaskId==InvestigationData.LiveTask?"▶ ":"□ ")+"主播直播证据  "+match.StreamerPhotos+" / 3";
                if(match.PhoneMenuOpen) {if(GUILayout.Button(live,GUILayout.MinHeight(36))) match.QueuePhoneAction(InvestigationData.LiveTask);}
                else GUILayout.Label(live,small);
            }
            GUILayout.EndScrollView();
            GUILayout.Label(match.SelectedTaskId==InvestigationData.LiveTask?"拍摄正在直播的主播，每场最多一张。":match.SelectedTask?.description??"请选择任务",notice,GUILayout.Height(100));
            if(match.PendingPhoto!=null)
            {
                GUILayout.Label("待提交照片 #"+match.PendingPhoto.id,small);
                string destination=match.PendingPhoto.taskId==InvestigationData.LiveTask?"主播直播证据":match.Quests.Find(q=>q.Id==match.PendingPhoto.taskId)?.Title;
                GUILayout.Label("提交到："+destination,small);
                if(match.PhoneMenuOpen)
                {
                    if(GUILayout.Button("上传这张照片",GUILayout.Height(36))) match.QueuePhoneAction(upload:match.PendingPhoto.id);
                    if(GUILayout.Button("取消照片（不扣分）",GUILayout.Height(30))) match.QueuePhoneAction(cancel:true);
                }
            }
            else GUILayout.Label("暂无待上传照片",small);
            if(match.PhoneMenuOpen && GUILayout.Button("进入相机 / 继续取景（Q）",GUILayout.Height(34))) match.SetPhoneMenu(false);
            GUILayout.Label(match.PhoneMenuOpen?"先选任务，再进入相机。F / Esc 收起手机。":"左键 / 空格拍照 · Q 返回任务菜单 · F 收起",small);
            GUILayout.EndArea();
            if(match.PhoneMenuOpen)
            {
                if(match.PendingPhoto!=null && match.PhotoPreview)
                {Fill(new Rect(420,210,500,292),new Color(0,0,0,.9f));GUI.DrawTexture(new Rect(430,220,480,270),match.PhotoPreview,ScaleMode.ScaleToFit);}
                return;
            }
            Vector2 lower=ToGui(new Vector2(Screen.width*.22f,Screen.height*.23f));
            Vector2 upper=ToGui(new Vector2(Screen.width*.64f,Screen.height*.77f));
            var frame=new Rect(lower.x,upper.y,upper.x-lower.x,lower.y-upper.y);
            var color=new Color(.8f,.85f,.9f,.7f);Outline(frame,color,2);
            Fill(new Rect(frame.center.x-8,frame.center.y,16,2),color);Fill(new Rect(frame.center.x,frame.center.y-8,2,16),color);
            GUI.Label(new Rect(frame.x,frame.yMax+8,frame.width,35),"相机范围 < "+match.Settings.maxPhotoDistance.ToString("0.0")+" m · 拍摄不扣分",small);
        }

        void Outline(Rect rect,Color color,float thickness)
        {
            Fill(new Rect(rect.x,rect.y,rect.width,thickness),color);
            Fill(new Rect(rect.x,rect.yMax-thickness,rect.width,thickness),color);
            Fill(new Rect(rect.x,rect.y,thickness,rect.height),color);
            Fill(new Rect(rect.xMax-thickness,rect.y,thickness,rect.height),color);
        }

        void DrawStreamerObserver(MatchSession match)
        {
            var actor=match.Streamer;
            var brain=actor.Controller;
            var rect=new Rect(20,138,384,216);
            bool alarm=Time.time<actor.AlertUntil;
            if(alarm) rect.position+=new Vector2(Mathf.Sin(Time.time*67),Mathf.Cos(Time.time*83))*4;
            Fill(new Rect(20,116,384,22),new Color(.02f,.04f,.05f,.95f));
            GUI.Label(new Rect(28,116,375,24),"AI 主播观察窗（仅供原型测试）  ·  "+(actor.Floor+1)+"F",small);
            GUI.DrawTexture(rect,actor.ViewTexture,ScaleMode.StretchToFill,false);
            DrawSoundPulses(match,actor.View,rect);
            Outline(rect,alarm?new Color(1,.2f,.2f):new Color(.2f,.33f,.36f),2);
            Fill(new Rect(20,358,384,65),new Color(.02f,.04f,.05f,.96f));
            string status;
            if(actor.State==StreamerState.Streaming)
            {
                status="Flappy Bird  "+brain.Level.Score+" / "+brain.Level.TargetScore+" 分  ·  AI 拍翅中";
            }
            else if(actor.State==StreamerState.Roaming) status="搜捕中 · 距离返程 "+Mathf.CeilToInt(brain.RoamRemaining)+" 秒";
            else if(actor.State==StreamerState.Returning) status="正在返回电脑房…";
            else if(actor.State==StreamerState.Turning) status="转椅前摇："+brain.StateRemaining.ToString("0.0")+" 秒";
            else if(actor.State==StreamerState.Ambush) status="猛回头抓捕："+brain.StateRemaining.ToString("0.0")+" 秒";
            else status="眩晕中："+brain.StateRemaining.ToString("0.0")+" 秒";
            GUI.Label(new Rect(30,364,365,30),status,small);
            GUI.Label(new Rect(30,393,365,28),alarm?"背后有人闪光偷拍！":"声波穿墙显示，带来源楼层。",small);
        }


        void DrawSoundPulses(MatchSession match,Camera camera,Rect rect)
        {
            GUI.BeginGroup(rect);
            foreach(var pulse in match.Pulses)
            {
                float age=Time.time-pulse.Created;
                Vector3 point=camera.WorldToViewportPoint(pulse.Position+Vector3.up*1.2f);
                Vector2 center=new Vector2(point.x*rect.width,(1-point.y)*rect.height);
                bool behind=point.z<=0;
                if(behind)
                {
                    Vector3 direction=camera.transform.InverseTransformDirection(pulse.Position-camera.transform.position);
                    center=new Vector2(rect.width*.5f+Mathf.Sign(direction.x)*Mathf.Min(Mathf.Abs(direction.x)*20,170),rect.height-22);
                }
                center.x=Mathf.Clamp(center.x,24,rect.width-24); center.y=Mathf.Clamp(center.y,24,rect.height-35);
                Color red=new Color(1,.16f,.18f,Mathf.Clamp01(1-age/2.8f));
                float radius=8+age*22;
                for(int segment=0;segment<28;segment++)
                {
                    float a=segment*Mathf.PI*2/28,b=(segment+1)*Mathf.PI*2/28;
                    Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,red,2);
                }
                Fill(new Rect(center.x-3,center.y-3,6,6),red);
                int sourceFloor=Mathf.FloorToInt((pulse.Position.y+.15f)/match.World.Data.floorHeight)+1;
                string noise=pulse.Kind==NoiseKind.Shutter?"快门":pulse.Kind==NoiseKind.HideBed?"床铺巨响":(pulse.Kind==NoiseKind.Door || pulse.Kind==NoiseKind.DoorClose)?"门声":pulse.Kind==NoiseKind.Chair?"转椅":"脚步";
                GUI.Label(new Rect(Mathf.Clamp(center.x-44,4,rect.width-120),center.y+6,120,25),noise+" "+sourceFloor+"F"+(behind?" 后方":""),small);
            }
            GUI.EndGroup();
        }

        void DrawStreamerPlayer(MatchSession match)
        {
            var actor=match.Streamer; var brain=actor.Controller;
            if(actor.State==StreamerState.Streaming)
            {
                DrawBroadcastScreen(match);
                if(Time.time<actor.AlertUntil)
                {
                    Outline(new Rect(3,3,1274,794),new Color(1,.2f,.2f),5);
                    Fill(new Rect(170,105,660,42),new Color(.3f,.035f,.05f,.94f));
                    GUI.Label(new Rect(188,111,630,32),"有人在背后偷拍！本场直播照片已被取证。",notice);
                }
                return;
            }
            else
            {
                GUI.Label(new Rect(630,390,25,25),"+",notice);
                Fill(new Rect(360,660,600,60),new Color(.035f,.055f,.075f,.92f));
                GUI.Label(new Rect(380,670,560,48),actor.State==StreamerState.Returning
                    ? "返回电脑按 E 开播 · "+Mathf.CeilToInt(brain.NextFanLossIn)+" 秒后扣 "+match.Settings.lateFanLoss+" 粉丝"
                    : "搜捕剩余 "+Mathf.CeilToInt(brain.RoamRemaining)+" 秒 · E 搜查衣柜 / 床",notice);
            }
            DrawSoundPulses(match,actor.View,new Rect(20,112,1240,620));
            Fill(new Rect(842,16,418,88),new Color(.035f,.055f,.075f,.92f));
            GUI.Label(new Rect(858,27,385,26),match.RequiresLiveEvidence?"狗仔已取证主播 "+match.StreamerPhotos+" / 3 场":"狗仔任务完成 "+match.Quests.Count(q=>q.Complete)+" / "+match.Quests.Count,section);
            GUI.Label(new Rect(858,57,385,38),actor.State==StreamerState.Streaming
                ? "直播锁定 · "+brain.Level.Score+" / "+brain.Level.TargetScore+" 分才能下播"
                : "粉丝 "+brain.Fans.ToString("N0")+" · 粉丝归零即失败",small);
            if(Time.time<actor.AlertUntil)
            {
                Outline(new Rect(3,3,1274,794),new Color(1,.2f,.2f),5);
                GUI.Label(new Rect(435,116,620,45),"有人在背后偷拍！本场直播照片已被取证。",notice);
            }
        }

        void DrawBroadcastScreen(MatchSession match)
        {
            var brain=match.Streamer.Controller;var level=brain.Level;
            if(chatMatch!=match || chatRound!=level.LevelNumber)
            { chatMatch=match;chatRound=level.LevelNumber;chatStarted=Time.unscaledTime; }
            Fill(new Rect(-2000,-2000,6000,6000),new Color(.014f,.023f,.04f));
            Color panel=new Color(.04f,.066f,.10f);
            Fill(new Rect(20,16,610,68),panel);Fill(new Rect(646,16,614,68),panel);
            Fill(new Rect(20,16,4,68),accent);
            GUI.Label(new Rect(38,22,260,20),"FLAPPY BIRD  /  直播挑战",small);
            GUI.Label(new Rect(38,43,340,38),level.Score+" / "+level.TargetScore+"  分",scoreText);
            GUI.Label(new Rect(380,45,220,28),"达标后解除直播锁定",small);
            GUI.Label(new Rect(666,24,270,23),"当前直播场次",small);
            GUI.Label(new Rect(666,46,270,33),"第 "+level.LevelNumber+" 场",title);
            GUI.Label(new Rect(968,24,270,23),"当前粉丝数量",small);
            GUI.Label(new Rect(968,46,270,33),brain.Fans.ToString("N0"),title);
            DrawFlappy(level,new Rect(20,100,960,600));
            DrawLiveChat(new Rect(996,100,264,600),Time.unscaledTime-chatStarted,level.LevelNumber);
            Fill(new Rect(20,716,1240,68),panel);
            GUI.Label(new Rect(38,725,950,27),"空格 / 鼠标左键：拍翅    ·    碰撞后分数清零，按键重试    ·    达到 "+level.TargetScore+" 分后下播搜捕",notice);
            GUI.Label(new Rect(38,756,1100,22),"直播时无法移动或回头    |    Tab 调试 / 菜单    |    Esc 返回角色选择",small);
            if(match.IsNetwork) GUI.Label(new Rect(1020,756,220,22),Mathf.RoundToInt(app.Lan.LocalFps)+" FPS · "+Mathf.RoundToInt(app.Lan.RoundTripMs)+" ms",small);
        }
        void DrawLiveChat(Rect rect,float elapsed,int broadcast)
        {
            Fill(rect,new Color(.04f,.066f,.10f));Outline(rect,new Color(.13f,.22f,.29f),1);
            GUI.Label(new Rect(rect.x+16,rect.y+12,rect.width-32,26),"直播间弹幕",section);
            GUI.Label(new Rect(rect.x+16,rect.y+44,rect.width-32,22),"欢迎来到今晚的直播",small);
            Fill(new Rect(rect.x+12,rect.y+76,rect.width-24,1),new Color(.14f,.23f,.3f));
            var viewport=new Rect(rect.x+12,rect.y+90,rect.width-24,rect.height-102);
            GUI.BeginGroup(viewport);
            const float rowHeight=70,speed=19;
            float travel=Mathf.Max(0,elapsed)*speed;int first=Mathf.FloorToInt(travel/rowHeight);float fraction=travel%rowHeight;
            for(int row=0;row<9;row++)
            {
                int index=first+row;float y=row*rowHeight-fraction;
                int nameIndex=(index*5+broadcast)%ChatNames.Length,textIndex=(index*7+broadcast)%ChatTexts.Length;
                Color tint=index%3==0?new Color(.46f,.36f,.66f):index%3==1?new Color(.23f,.49f,.63f):new Color(.63f,.37f,.48f);
                Fill(new Rect(0,y+3,39,18),tint);GUI.Label(new Rect(0,y+2,39,20),"Lv "+(6+index%29),badge);
                GUI.Label(new Rect(47,y,viewport.width-47,23),ChatNames[nameIndex],chatName);
                GUI.Label(new Rect(0,y+24,viewport.width,42),ChatTexts[textIndex],chatText);
            }
            GUI.EndGroup();
        }

        void DrawFlappy(MinigameLevel level,Rect rect)
        {
            GUI.BeginGroup(rect);
            Fill(new Rect(0,0,rect.width,rect.height),new Color(.12f,.32f,.42f));
            for(int i=0;i<5;i++)
            {
                float x=(i*183+70)%rect.width;
                Fill(new Rect(x,70+(i%3)*45,100,18),new Color(.25f,.46f,.54f));
                Fill(new Rect(x+23,60+(i%3)*45,47,10),new Color(.25f,.46f,.54f));
            }
            foreach(var pipe in level.Pipes)
            {
                float x=pipe.X*rect.width,w=MinigameLevel.PipeWidth*rect.width;
                float upper=(1-pipe.GapCenter-level.PipeGap*.5f)*rect.height;
                float lower=(1-pipe.GapCenter+level.PipeGap*.5f)*rect.height;
                Color green=new Color(.27f,.76f,.45f);
                Fill(new Rect(x,0,w,upper),green); Fill(new Rect(x,lower,w,rect.height-lower),green);
                Fill(new Rect(x-5,upper-16,w+10,16),new Color(.36f,.89f,.54f));
                Fill(new Rect(x-5,lower,w+10,16),new Color(.36f,.89f,.54f));
                Fill(new Rect(x+7,0,8,Mathf.Max(0,upper-16)),new Color(.45f,.91f,.58f));
                Fill(new Rect(x+7,lower+16,8,rect.height-lower),new Color(.45f,.91f,.58f));
            }
            float bx=MinigameLevel.BirdX*rect.width,by=(1-level.BirdHeight)*rect.height;
            float bw=MinigameLevel.BirdRadius*rect.width*2,bh=MinigameLevel.BirdRadius*rect.height*2;
            Fill(new Rect(bx-bw*.5f,by-bh*.5f,bw,bh),new Color(1,.8f,.25f));
            Fill(new Rect(bx-bw*.42f,by,bw*.45f,bh*.45f),new Color(1,.58f,.18f));
            Fill(new Rect(bx+bw*.13f,by-bh*.36f,6,6),Color.white);
            Fill(new Rect(bx+bw*.25f,by-bh*.28f,3,3),Color.black);
            Fill(new Rect(bx+bw*.4f,by,10,5),new Color(1,.38f,.16f));
            if(!level.Running && !level.Complete)
            {
                Fill(new Rect(110,rect.height*.42f,rect.width-220,90),new Color(.02f,.04f,.07f,.9f));
                GUI.Label(new Rect(135,rect.height*.42f+12,rect.width-270,74),
                    level.Crashed?"碰撞！得分已清零。\n按空格 / 左键重试，主播仍不能离开。":"按空格 / 左键开始并拍翅\n通过 "+level.TargetScore+" 根管道才能下播搜捕。",notice);
            }
            GUI.EndGroup();
            Outline(rect,new Color(.31f,.84f,.78f),2);
        }

        void Line(Vector2 from,Vector2 to,Color color,float width)
        {
            Matrix4x4 previous=GUI.matrix;
            Vector2 delta=to-from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);
            Fill(new Rect(from.x,from.y-width*.5f,delta.magnitude,width),color);
            GUI.matrix=previous;
        }

        void DrawDebug(MatchSession match)
        {
            Fill(new Rect(20,440,430,295),new Color(.035f,.06f,.08f,.98f));
            GUILayout.BeginArea(new Rect(35,448,400,276));
            debugScroll=GUILayout.BeginScrollView(debugScroll,false,false);
            GUILayout.Label("参数调试  ·  Tab 收起并恢复鼠标观察",section);
            GUILayout.Label("主播移速："+match.Settings.streamerSpeed.ToString("0.0")+" m/s",small);
            match.Settings.streamerSpeed=GUILayout.HorizontalSlider(match.Settings.streamerSpeed,1,8);
            GUILayout.Label("狗仔行走移速："+match.Settings.reporterSpeed.ToString("0.0")+" m/s（静步 / 蹲行 ×0.45）",small);
            match.Settings.reporterSpeed=GUILayout.HorizontalSlider(match.Settings.reporterSpeed,.5f,4);
            GUILayout.Label("拍照最大距离："+match.Settings.maxPhotoDistance.ToString("0.0")+" m",small);
            match.Settings.maxPhotoDistance=GUILayout.HorizontalSlider(match.Settings.maxPhotoDistance,2,15);
            GUILayout.Label("Flappy Bird 通关："+match.Settings.flappyTargetScore+" 分（下场生效）",small);
            match.Settings.flappyTargetScore=Mathf.RoundToInt(GUILayout.HorizontalSlider(match.Settings.flappyTargetScore,1,30));
            GUILayout.Label("狗仔失误上限："+match.Settings.maxScore,small);
            int max=Mathf.RoundToInt(GUILayout.HorizontalSlider(match.Settings.maxScore,1,999));
            if(max!=match.Settings.maxScore) match.SetMaxScore(max);
            if(!match.IsStreamerPlayer)
            {
                match.Settings.allowEmergencyTurn=GUILayout.Toggle(match.Settings.allowEmergencyTurn,"实验：允许 AI 主播猛回头（默认关闭）");
                showObserver=GUILayout.Toggle(showObserver,"显示 AI 主播观察窗");
            }
            GUI.enabled=match.Streamer.State==StreamerState.Roaming;
            if(Button(match.IsStreamerPlayer?"结束搜捕，提示返回电脑":"让主播现在返回电脑房")) match.Streamer.Controller.BeginReturn();
            GUI.enabled=!match.IsStreamerPlayer && match.Settings.allowEmergencyTurn && match.Streamer.State==StreamerState.Streaming;
            if(Button("测试：主播猛回头")) match.Streamer.Controller.TryEmergencyTurn();
            GUI.enabled=true;
            GUILayout.Label("通过管道才计分，达到目标才能下播；碰撞清零重试。每场直播最多一张主播照片。",small);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }

        void OnDestroy()
        {
            foreach(var texture in textures) PrototypeVisuals.Release(texture);
            PrototypeVisuals.Release(skin); PrototypeVisuals.Release(font);
        }
    }
}
