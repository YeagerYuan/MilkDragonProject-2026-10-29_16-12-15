using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace UnderAvatar
{
    public enum LanPhase { None, Connecting, Lobby, Loading, Playing }
    [DefaultExecutionOrder(100)]
    public sealed class LanGame : MonoBehaviour
    {
        public PrototypeApp App { get; private set; }
        public LanPhase Phase { get; private set; }
        public bool IsHost { get; private set; }
        public bool Active => Phase!=LanPhase.None;
        public bool LocalReady { get; private set; }
        public bool RemoteReady { get; private set; }
        public bool PeerJoined { get; private set; }
        public TestRole LocalRole { get; private set; }
        public int Port { get; private set; }=7777;
        public string Status { get; private set; }="";
        public string MapName => map?.name??"";
        public bool CanStart => IsHost && Phase==LanPhase.Lobby && PeerJoined && LocalReady && RemoteReady;
        public int ReceivedFrames => transport?.ReceivedFrames??0;
        public float RoundTripMs { get; private set; }
        public float LocalFps { get; private set; }
        public float HostFps { get; private set; }
        public int PendingInputs => prediction?.PendingCount??0;
        public string LocalAddresses
        {
            get
            {
                try { return string.Join(" / ",Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(a=>a.AddressFamily==AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)).Select(a=>a.ToString())); }
                catch { return "请查看本机网络设置中的 IPv4 地址"; }
            }
        }
        LanTransport transport;
        BuildingData map;
        MatchSettings rules;
        readonly Queue<PlayerCommand> remoteInputs=new Queue<PlayerCommand>();
        ClientPrediction prediction;
        int receivedInput,confirmedInput;
        float remoteCredit;
        float lastTraffic,lastRemoteInput,nextHeartbeat,nextSnapshot,connectingSince;
        public Func<PlayerCommand> CommandProvider { get; set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public float TestReceiveDelay { get; set; }
        readonly Queue<KeyValuePair<float,string>> delayedMessages=new Queue<KeyValuePair<float,string>>();
#endif
        public void Initialize(PrototypeApp app) { App=app; }
        public void CreateRoom(TestRole role,int port)
        {
            try
            {
                CheckPort(port); Disconnect();
                map=App.Building?App.Building.CreateRuntimeCopy():BuildingData.CreateDefault();
                PrototypeApp.ValidateMap(map);
                rules=JsonUtility.FromJson<MatchSettings>(JsonUtility.ToJson(App.Settings)); rules.allowEmergencyTurn=false;
                ValidateRules(rules);
                IsHost=true;LocalRole=role;Port=port;Phase=LanPhase.Lobby;
                transport=new LanTransport();BindTransport();transport.Listen(port);
                Status="房间已创建，等待另一台电脑加入。";App.Report(Status);
            }
            catch(Exception e) { Fail("创建房间失败："+e.Message); }
        }
        public void JoinRoom(string address,int port)
        {
            try
            {
                CheckPort(port);Disconnect();IsHost=false;Port=port;
                Phase=LanPhase.Connecting;connectingSince=Time.realtimeSinceStartup;
                transport=new LanTransport();BindTransport();transport.Connect(address.Trim(),port);
                Status="正在连接 "+address+":"+port+" …";
            }
            catch(Exception e) { Fail("加入失败："+e.Message); }
        }
        static void CheckPort(int port) { if(port<1024||port>65535) throw new ArgumentException("端口必须为 1024–65535。"); }
        static void ValidateRules(MatchSettings settings)
        {
            if(settings==null || settings.flappyTargetScore<1 || settings.flappyTargetScore>100 || settings.maxScore<1 || settings.maxScore>10 ||
                !(settings.reporterSpeed>=.5f && settings.reporterSpeed<=8) || !(settings.streamerSpeed>=1 && settings.streamerSpeed<=12) ||
                !(settings.maxPhotoDistance>=2 && settings.maxPhotoDistance<=30) || !(settings.patrolSeconds>=5 && settings.patrolSeconds<=300))
                throw new ArgumentException("房间玩法参数无效。");
        }
        void BindTransport()
        {
            transport.ConnectedNow+=()=>
            {
                lastTraffic=Time.realtimeSinceStartup;nextHeartbeat=0;
                if(!IsHost) Send(new LanMessage{type="hello"});
            };
            transport.Message+=Receive;
        }
        void Send(LanMessage message) => transport?.Send(JsonUtility.ToJson(message));
        void Receive(string text)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(TestReceiveDelay>0)
            {
                delayedMessages.Enqueue(new KeyValuePair<float,string>(Time.realtimeSinceStartup+TestReceiveDelay,text));return;
            }
#endif
            ReceiveNow(text);
        }
        void ReceiveNow(string text)
        {
            lastTraffic=Time.realtimeSinceStartup;
            var message=JsonUtility.FromJson<LanMessage>(text);
            if(message==null || message.protocol!=LanMessage.Version) throw new InvalidOperationException("版本不一致，请使用相同版本的游戏。");
            if(message.type=="ping") { Send(new LanMessage{type="pong",stamp=message.stamp});return; }
            if(message.type=="pong")
            {
                float rtt=Mathf.Max(0,(Time.realtimeSinceStartup-message.stamp)*1000);
                RoundTripMs=RoundTripMs<=0?rtt:Mathf.Lerp(RoundTripMs,rtt,.3f);return;
            }
            if(message.type=="leave"||message.type=="reject") { Fail(message.text??"对方已离开房间。");return; }
            if(IsHost) ReceiveHost(message); else ReceiveGuest(message);
        }
        void ReceiveHost(LanMessage message)
        {
            switch(message.type)
            {
                case "hello":
                    if(PeerJoined||Phase!=LanPhase.Lobby) return;
                    PeerJoined=true;RemoteReady=false;
                    Send(new LanMessage{type="welcome",role=Opposite(LocalRole),map=map.ToJson(),settings=rules});
                    SendLobby();Status="玩家已加入，请双方点击准备。";break;
                case "ready":
                    if(!PeerJoined||Phase!=LanPhase.Lobby) return;
                    RemoteReady=message.guestReady;SendLobby();break;
                case "loaded":
                    if(!PeerJoined||Phase!=LanPhase.Loading) return;
                    Phase=LanPhase.Playing;ResetInputs();lastRemoteInput=Time.realtimeSinceStartup;
                    Send(new LanMessage{type="go",snapshot=Snapshot()});Status="对战进行中";break;
                case "input":
                    if(Phase!=LanPhase.Playing||message.input==null||!message.input.Valid) return;
                    if(message.input.sequence!=receivedInput+1) throw new InvalidOperationException("输入序号不一致，请使用相同版本重新连接。");
                    receivedInput=message.input.sequence;remoteInputs.Enqueue(message.input);
                    if(remoteInputs.Count>300) throw new InvalidOperationException("输入积压过多，请检查连接。");
                    lastRemoteInput=Time.realtimeSinceStartup;break;
                case "lobby":
                    if(Phase==LanPhase.Playing && App.Session.Finished) ReturnToLobby();break;
            }
        }
        void ReceiveGuest(LanMessage message)
        {
            switch(message.type)
            {
                case "welcome":
                    if(Phase!=LanPhase.Connecting) return;
                    if(message.role!=TestRole.Reporter && message.role!=TestRole.Streamer) throw new ArgumentException("角色无效。");
                    map=BuildingData.FromJson(message.map);PrototypeApp.ValidateMap(map);ValidateRules(message.settings);
                    rules=message.settings;LocalRole=message.role;PeerJoined=true;Phase=LanPhase.Lobby;
                    Status="已同步房主关卡，请点击准备。";break;
                case "lobby":
                    if(map==null) return;
                    if(Phase==LanPhase.Playing||Phase==LanPhase.Loading) App.StopMatchForLobby();
                    Phase=LanPhase.Lobby;LocalReady=message.guestReady;RemoteReady=message.hostReady;break;
                case "start":
                    if(Phase!=LanPhase.Lobby||!LocalReady) return;
                    Phase=LanPhase.Loading;App.StartNetworkSession(map,rules,LocalRole,false);
                    if(!App.Session) throw new InvalidOperationException(App.StartupError);
                    ApplyGuestSnapshot(message.snapshot);
                    Send(new LanMessage{type="loaded"});Status="等待房主开始…";break;
                case "go":
                    if(Phase!=LanPhase.Loading) return;
                    ApplyGuestSnapshot(message.snapshot);Phase=LanPhase.Playing;Status="对战进行中";break;
                case "snapshot":
                    if(Phase==LanPhase.Playing) ApplyGuestSnapshot(message.snapshot);break;
            }
        }
        void SendLobby() => Send(new LanMessage{type="lobby",hostReady=LocalReady,guestReady=RemoteReady});
        public static TestRole Opposite(TestRole role) => role==TestRole.Reporter?TestRole.Streamer:TestRole.Reporter;
        public void SetReady(bool ready)
        {
            if(Phase!=LanPhase.Lobby||!PeerJoined) return;
            LocalReady=ready;
            if(IsHost) SendLobby();else Send(new LanMessage{type="ready",guestReady=ready});
        }
        public void StartMatch()
        {
            if(!CanStart) return;
            Phase=LanPhase.Loading;
            App.StartNetworkSession(map,rules,LocalRole,true);
            if(!App.Session) { Fail(App.StartupError);return; }
            ResetInputs();
            Send(new LanMessage{type="start",snapshot=Snapshot()});Status="正在等待另一位玩家加载…";
        }
        public void ReturnToLobby()
        {
            if(!IsHost) { Send(new LanMessage{type="lobby"});return; }
            if(Phase!=LanPhase.Playing || !App.Session.Finished) return;
            App.StopMatchForLobby();Phase=LanPhase.Lobby;LocalReady=RemoteReady=false;SendLobby();Status="准备好后可再来一局。";
        }
        void Update()
        {
            float frame=Time.unscaledDeltaTime;
            if(frame>0) LocalFps=Mathf.Lerp(LocalFps,1/frame,1-Mathf.Exp(-frame*3));
            if(!Active) return;
            try
            {
                transport?.Poll();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                while(Active && delayedMessages.Count>0 && delayedMessages.Peek().Key<=Time.realtimeSinceStartup)
                    ReceiveNow(delayedMessages.Dequeue().Value);
#endif
                if(!Active) return;
                if(transport?.Error!=null) { Fail("连接中断："+transport.Error);return; }
                float now=Time.realtimeSinceStartup;
                if(Phase==LanPhase.Connecting && now-connectingSince>10) { Fail("连接超时，请检查房主 IP、端口和本地网络。");return; }
                if(transport!=null && transport.Connected)
                {
                    if(now-lastTraffic>12) { Fail("对方长时间没有响应，连接已断开。");return; }
                    if(now>=nextHeartbeat) { nextHeartbeat=now+1;Send(new LanMessage{type="ping",stamp=now}); }
                }
                if(Phase==LanPhase.Playing && App.Session && !App.Session.Finished)
                {
                    PlayerCommand local=CommandProvider?.Invoke()??PlayerCommand.Read(App.Session.InputCaptured);
                    if(!local.Valid) local=new PlayerCommand();
                    if(IsHost)
                    {
                        float dt=Mathf.Min(Time.deltaTime,.1f);
                        App.Session.SimulateNetworkActor(LocalRole,local,dt);
                        remoteCredit=Mathf.Min(remoteCredit+dt,.15f);
                        while(remoteInputs.Count>0 && remoteCredit>=ClientPrediction.Step)
                        {
                            var input=remoteInputs.Dequeue();
                            App.Session.SimulateNetworkActor(Opposite(LocalRole),input,ClientPrediction.Step);
                            confirmedInput=input.sequence;remoteCredit-=ClientPrediction.Step;
                        }
                        if(remoteInputs.Count==0 && now-lastRemoteInput>.35f)
                        {
                            App.Session.SimulateNetworkActor(Opposite(LocalRole),new PlayerCommand(),dt);
                            remoteCredit=0;
                        }
                        App.Session.Streamer.Controller.TryCapture();
                    }
                    else prediction?.Tick(local,Time.deltaTime,command=>Send(new LanMessage{type="input",input=command}));
                }
                if(IsHost && Phase==LanPhase.Playing && now>=nextSnapshot && transport.PendingSendBytes<65536)
                {
                    nextSnapshot=now+1f/30;
                    Send(new LanMessage{type="snapshot",snapshot=Snapshot()});
                }
                transport?.Flush();
            }
            catch(Exception e) { Fail("联机已结束："+e.Message); }
        }
        MatchSnapshot Snapshot()
        {
            var state=App.Session.CaptureSnapshot();state.inputAck=confirmedInput;state.hostFps=LocalFps;return state;
        }
        void ApplyGuestSnapshot(MatchSnapshot state)
        {
            if(prediction==null || prediction.Match!=App.Session) prediction=new ClientPrediction(App.Session);
            HostFps=state.hostFps;prediction.Receive(state);
        }
        void ResetInputs()
        {
            remoteInputs.Clear();receivedInput=confirmedInput=0;remoteCredit=0;prediction=null;
        }
        void Fail(string message)
        {
            Disconnect();App.StopMatchForLobby();Status=message;App.Report(message);
        }
        public void Disconnect()
        {
            if(transport!=null)
            {
                try { Send(new LanMessage{type="leave",text="对方已离开房间。"});transport.Flush(); } catch { }
                transport.Dispose();transport=null;
            }
            Phase=LanPhase.None;PeerJoined=LocalReady=RemoteReady=false;CommandProvider=null;
            map=null;rules=null;ResetInputs();RoundTripMs=0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            delayedMessages.Clear();
#endif
        }
        void OnDestroy() { Disconnect(); }
    }
}
