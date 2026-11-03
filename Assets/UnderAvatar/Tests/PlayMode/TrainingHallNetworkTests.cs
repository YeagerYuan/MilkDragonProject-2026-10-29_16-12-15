using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class TrainingHallNetworkTests
    {
        GameObject root;
        LanTransport peer;
        readonly List<LanMessage> received=new List<LanMessage>();
        void Send(LanMessage message) { peer.Send(JsonUtility.ToJson(message));peer.Flush(); }
        IEnumerator Until(Func<bool> ready)
        {
            float deadline=Time.realtimeSinceStartup+5;
            while(!ready() && Time.realtimeSinceStartup<deadline) { peer.Poll();yield return null; }
            Assert.That(ready(),Is.True,"LAN transition timed out");
        }
        [UnityTearDown] public IEnumerator Cleanup() {peer?.Dispose();if(root) UnityEngine.Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator BothReadyTwiceAndSessionEpochRejectsOldInputsAndLoadAcknowledgements()
        {
            var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
            root=new GameObject("Hall network host");var app=root.AddComponent<PrototypeApp>();app.Lan.CreateRoom(TestRole.Streamer,port);
            var lan=app.Lan;peer=new LanTransport();peer.Message+=text=>received.Add(JsonUtility.FromJson<LanMessage>(text));peer.Connect("127.0.0.1",port);
            yield return Until(()=>peer.Connected);Send(new LanMessage{type="hello"});yield return Until(()=>lan.PeerJoined);
            lan.SetReady(true);Assert.That(lan.CanStart,Is.False);
            Send(new LanMessage{type="ready",guestReady=true});yield return Until(()=>lan.CanStart);
            lan.StartMatch();yield return Until(()=>received.Any(m=>m.type=="start"));var hallStart=received.Last(m=>m.type=="start");
            Assert.That(hallStart.training,Is.True);Assert.That(lan.Phase,Is.EqualTo(LanPhase.Loading));
            Send(new LanMessage{type="loaded",sessionId=hallStart.sessionId});yield return Until(()=>lan.Phase==LanPhase.Training);
            Assert.That(app.Session.IsTraining,Is.True);Assert.That(lan.LocalReady||lan.RemoteReady,Is.False);
            Send(new LanMessage{type="hall_ready",sessionId=hallStart.sessionId-1,guestReady=true});
            Send(new LanMessage{type="input",sessionId=hallStart.sessionId-1,input=new PlayerCommand{sequence=90,x=1}});
            yield return null;yield return null;Assert.That(lan.RemoteReady,Is.False);Assert.That(lan.Active,Is.True);
            Send(new LanMessage{type="hall_ready",sessionId=hallStart.sessionId,guestReady=true});yield return Until(()=>lan.RemoteReady);
            Assert.That(lan.CanStart,Is.False);lan.SetReady(true);Assert.That(lan.CanStart,Is.True);
            app.Session.World.Items.First(i=>i.Data.type==ItemType.Door).SetDoorOpen(true);
            lan.StartMatch();yield return Until(()=>received.Count(m=>m.type=="start")==2);var gameStart=received.Last(m=>m.type=="start");
            Assert.That(gameStart.training,Is.False);Assert.That(gameStart.sessionId,Is.GreaterThan(hallStart.sessionId));
            Send(new LanMessage{type="loaded",sessionId=hallStart.sessionId});yield return null;yield return null;
            Assert.That(lan.Phase,Is.EqualTo(LanPhase.Loading));
            Send(new LanMessage{type="loaded",sessionId=gameStart.sessionId});yield return Until(()=>lan.Phase==LanPhase.Playing);
            Assert.That(app.Session.IsTraining,Is.False);Assert.That(app.Session.Streamer.State,Is.EqualTo(StreamerState.Streaming));
            Assert.That(app.Session.World.Items.Where(i=>i.Data.type==ItemType.Door).All(i=>!i.IsOpen),Is.True);
            Send(new LanMessage{type="input",sessionId=hallStart.sessionId,input=new PlayerCommand{sequence=100,x=1}});
            Send(new LanMessage{type="input",sessionId=gameStart.sessionId,input=new PlayerCommand{sequence=1,x=1}});
            yield return Until(()=>received.Any(m=>m.type=="snapshot" && m.sessionId==gameStart.sessionId && m.snapshot.inputAck==1));
            Assert.That(lan.Active,Is.True);Assert.That(RenderSettings.ambientIntensity,Is.Zero);
            Send(new LanMessage{type="leave",text="test leave"});yield return Until(()=>!lan.Active);Assert.That(app.Session,Is.Null);
        }
    }
}
