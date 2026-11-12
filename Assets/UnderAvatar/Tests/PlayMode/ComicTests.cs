using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace UnderAvatar.Tests
{
    public sealed class ComicTests
    {
        GameObject root;
        ComicPlayerUI comic;
        LanTransport peer;
        readonly List<LanMessage> messages=new List<LanMessage>();
        [SetUp] public void Setup() {messages.Clear();peer=null;root=null;comic=null;}
        PrototypeApp CreateApp() { root=new GameObject("Comic flow test");return root.AddComponent<PrototypeApp>(); }
        int FreePort()
        {
            var socket=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);socket.Start();
            int port=((System.Net.IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();return port;
        }
        void BindPeer() { peer=new LanTransport();peer.Message+=json=>messages.Add(JsonUtility.FromJson<LanMessage>(json)); }
        void Send(LanMessage message) { peer.Send(JsonUtility.ToJson(message));peer.Flush(); }
        IEnumerator Until(Func<bool> condition)
        {
            float deadline=Time.realtimeSinceStartup+8;
            while(!condition() && Time.realtimeSinceStartup<deadline) {peer?.Poll();yield return null;}
            var app=root?root.GetComponent<PrototypeApp>():null;
            Assert.That(condition(),Is.True,"Comic / network transition timed out; "+peer?.Error+" / "+(app?app.Lan.Phase+" / "+app.Lan.Status:"no app")+" / received: "+string.Join(",",messages.Select(m=>m.type+":"+m.sessionId)));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            peer?.Dispose();if(root) UnityEngine.Object.Destroy(root);if(comic) UnityEngine.Object.Destroy(comic.gameObject);
            Time.timeScale=1;yield return null;
        }
        [UnityTest] public IEnumerator FiveFramesFadeWithUnscaledTimeAndFastClicksCompleteOnlyCurrentFrame()
        {
            comic=UnityEngine.Object.Instantiate(Resources.Load<ComicPlayerUI>("PaparazziComicCanvas"));
            int completed=0;comic.Show(()=>completed++);
            Assert.That(comic.Panels.Length,Is.EqualTo(5));Assert.That(comic.Panels.All(p=>p.alpha==0),Is.True);
            yield return null;
            var click=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute<IPointerClickHandler>(comic.gameObject,click,ExecuteEvents.pointerClickHandler);
            Assert.That(comic.RevealedCount,Is.EqualTo(1));Assert.That(comic.IsAnimating,Is.True);
            comic.Advance();Assert.That(comic.RevealedCount,Is.EqualTo(1));Assert.That(comic.Panels[0].alpha,Is.EqualTo(1));
            Assert.That(comic.Panels.Skip(1).All(p=>p.alpha==0),Is.True);
            Time.timeScale=0;comic.Advance();yield return new WaitForSecondsRealtime(.55f);
            Assert.That(comic.IsAnimating,Is.False);Assert.That(comic.Panels[1].alpha,Is.EqualTo(1));
            for(int i=2;i<5;i++) {comic.Advance();comic.Advance();}
            Assert.That(comic.Panels.All(p=>p.alpha==1),Is.True);Assert.That(completed,Is.Zero);
            comic.Advance();comic.Finish();Assert.That(completed,Is.EqualTo(1));Assert.That(comic.gameObject.activeSelf,Is.False);
        }
        [UnityTest] public IEnumerator SingleReporterIsGatedEveryTimeAndStreamerEntersDirectly()
        {
            var app=CreateApp();app.StartSingle(TestRole.Reporter);yield return null;
            Assert.That(app.ComicOpen,Is.True);Assert.That(app.Session,Is.Null);Assert.That(app.GameplayInputBlocked,Is.True);
            var original=app.Comic;app.StartSingle(TestRole.Reporter);Assert.That(app.Comic,Is.SameAs(original));
            app.OpenSettings();Assert.That(app.SettingsOpen,Is.False);
            original.GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(app.Session.IsTraining,Is.True);Assert.That(app.ComicOpen,Is.False);
            Assert.That(app.GameplayInputBlocked,Is.True,"Finishing click must not leak into gameplay.");
            app.ReturnToMenu();app.StartSingle(TestRole.Reporter);yield return null;
            Assert.That(app.ComicOpen,Is.True);Assert.That(app.Comic.RevealedCount,Is.Zero);
            Assert.That(app.Comic.Panels.All(p=>p.alpha==0),Is.True);
            app.ReturnToMenu();original.Finish();Assert.That(app.Session,Is.Null);
            app.StartSingle(TestRole.Streamer);Assert.That(app.ComicOpen,Is.False);Assert.That(app.Session.IsTraining,Is.True);
        }
        [UnityTest] public IEnumerator ReporterHostKeepsHeartbeatAndDefersHallUntilComicFinishes()
        {
            var app=CreateApp();int port=FreePort();app.Lan.CreateRoom(TestRole.Reporter,port);
            BindPeer();peer.Connect("127.0.0.1",port);yield return Until(()=>peer.Connected);
            Send(new LanMessage{type="hello"});yield return Until(()=>app.Lan.PeerJoined);
            app.Lan.SetReady(true);Send(new LanMessage{type="ready",guestReady=true});yield return Until(()=>app.Lan.CanStart);
            app.Lan.StartMatch();yield return null;
            Assert.That(app.ComicOpen,Is.True);Assert.That(app.Session,Is.Null);Assert.That(app.Lan.Phase,Is.EqualTo(LanPhase.Loading));
            Send(new LanMessage{type="ping",stamp=123});yield return Until(()=>messages.Any(m=>m.type=="pong" && m.stamp==123));
            Assert.That(messages.Any(m=>m.type=="start"),Is.False);
            app.Comic.Finish();yield return Until(()=>messages.Any(m=>m.type=="start"));
            var start=messages.Last(m=>m.type=="start");Send(new LanMessage{type="loaded",sessionId=start.sessionId});
            yield return Until(()=>app.Lan.Phase==LanPhase.Training);Assert.That(app.Session.IsTraining,Is.True);
        }
        [UnityTest] public IEnumerator ReporterGuestDefersLoadedAndFormalMatchDoesNotReplayComic()
        {
            var app=CreateApp();int port=FreePort();BindPeer();peer.Listen(port);app.Lan.JoinRoom("127.0.0.1",port);
            yield return Until(()=>messages.Any(m=>m.type=="hello"));
            Send(new LanMessage{type="welcome",role=TestRole.Reporter,map=BuildingData.CreateDefault().ToJson(),trainingMap=TrainingHallLayout.Create().ToJson(),settings=new MatchSettings()});
            yield return Until(()=>app.Lan.Phase==LanPhase.Lobby);app.Lan.SetReady(true);
            Send(new LanMessage{type="start",training=true,sessionId=1});yield return Until(()=>app.ComicOpen);
            Assert.That(app.Session,Is.Null);Send(new LanMessage{type="ping",stamp=321});
            yield return Until(()=>messages.Any(m=>m.type=="pong" && m.stamp==321));Assert.That(messages.Any(m=>m.type=="loaded"),Is.False);
            app.Comic.Finish();yield return Until(()=>messages.Any(m=>m.type=="loaded"));
            Send(new LanMessage{type="go",sessionId=1});yield return Until(()=>app.Lan.Phase==LanPhase.Training);
            app.Lan.SetReady(true);Send(new LanMessage{type="start",training=false,sessionId=2});
            yield return Until(()=>messages.Any(m=>m.type=="loaded" && m.sessionId==2));
            Assert.That(app.ComicOpen,Is.False);Assert.That(app.Session.IsTraining,Is.False);
        }
        [UnityTest] public IEnumerator DisconnectDuringComicCancelsPendingEntry()
        {
            var app=CreateApp();int port=FreePort();BindPeer();peer.Listen(port);app.Lan.JoinRoom("127.0.0.1",port);
            yield return Until(()=>messages.Any(m=>m.type=="hello"));
            Send(new LanMessage{type="welcome",role=TestRole.Reporter,map=BuildingData.CreateDefault().ToJson(),trainingMap=TrainingHallLayout.Create().ToJson(),settings=new MatchSettings()});
            yield return Until(()=>app.Lan.Phase==LanPhase.Lobby);app.Lan.SetReady(true);
            Send(new LanMessage{type="start",training=true,sessionId=1});yield return Until(()=>app.ComicOpen);
            var pending=app.Comic;Send(new LanMessage{type="leave",text="测试中断"});yield return Until(()=>!app.Lan.Active);
            Assert.That(app.ComicOpen,Is.False);pending.Finish();Assert.That(app.Session,Is.Null);
        }
    }
}
