using System;
using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class LanTests
    {
        GameObject root;
        LanTransport host,guest,extra;
        [TearDown] public void DisposeConnections() { host?.Dispose();guest?.Dispose();extra?.Dispose(); }
        [UnityTearDown] public IEnumerator Cleanup() { if(root) UnityEngine.Object.Destroy(root);yield return null; }

        [Test] public void FrameDecoderHandlesPartialHeadersUnicodeAndMultipleFrames()
        {
            var decoder=new LanFrameDecoder();
            var bytes=LanFrameDecoder.Frame("主播 / 狗仔").Concat(LanFrameDecoder.Frame("second")).ToArray();
            foreach(byte value in bytes) decoder.Push(new[]{value},1);
            Assert.That(decoder.Messages.Dequeue(),Is.EqualTo("主播 / 狗仔"));
            Assert.That(decoder.Messages.Dequeue(),Is.EqualTo("second"));
            Assert.Throws<InvalidOperationException>(()=>new LanFrameDecoder().Push(new byte[]{127,255,255,255},4));
            Assert.Throws<InvalidOperationException>(()=>new LanFrameDecoder().Push(new byte[4],4));
        }
        [UnityTest] public IEnumerator RealTcpExchangesOrderedFramesRejectsThirdPlayerAndDetectsDisconnect()
        {
            host=new LanTransport();host.Listen(0);
            guest=new LanTransport();guest.Connect("127.0.0.1",host.Port);
            float deadline=Time.realtimeSinceStartup+4;
            while((!host.Connected||!guest.Connected)&&Time.realtimeSinceStartup<deadline)
            { host.Poll();guest.Poll();yield return null; }
            Assert.That(host.Connected&&guest.Connected,Is.True);
            string received=null;int ordered=0;
            host.Message+=text=>{received=text;ordered++;};
            guest.Send("first");guest.Send("第二条");
            while(ordered<2&&Time.realtimeSinceStartup<deadline) {host.Poll();guest.Poll();yield return null;}
            Assert.That(ordered,Is.EqualTo(2));Assert.That(received,Is.EqualTo("第二条"));
            extra=new LanTransport();string rejection=null;extra.Message+=text=>rejection=text;
            extra.Connect("127.0.0.1",host.Port);
            while(rejection==null&&Time.realtimeSinceStartup<deadline) {host.Poll();extra.Poll();yield return null;}
            // TCP may expose EOF in the same read; the room must retain its original peer either way.
            Assert.That(host.Connected,Is.True);
            Assert.That(rejection!=null||extra.Error!=null,Is.True);
            guest.Dispose();
            while(host.Error==null&&Time.realtimeSinceStartup<deadline) {host.Poll();yield return null;}
            Assert.That(host.Connected,Is.False);Assert.That(host.Error,Is.Not.Null);
        }
        [UnityTest] public IEnumerator PlayStartsAtRoleSelectionAndBothSoloRolesRemainAvailable()
        {
            root=new GameObject("LauncherTest");var app=root.AddComponent<PrototypeApp>();
            yield return null;Assert.That(app.Session,Is.Null);
            app.StartSingle(TestRole.Reporter);Assert.That(app.Session.Role,Is.EqualTo(TestRole.Reporter));
            Assert.That(app.Session.Streamer.Controller.Driver,Is.Not.Null);
            app.ReturnToMenu();yield return null;
            app.StartSingle(TestRole.Streamer);Assert.That(app.Session.ReporterAI,Is.Not.Null);
            Assert.That(app.Session.Streamer.State,Is.EqualTo(StreamerState.Streaming));
        }
        [UnityTest] public IEnumerator NetworkAuthorityDisablesAIAndReplicatesDoorsEvidenceFlappyAndVictory()
        {
            var data=BuildingData.CreateDefault();var rules=new MatchSettings();
            root=new GameObject("AuthorityTest");var app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(data,rules,TestRole.Reporter,true);
            var match=app.Session;Assert.That(match,Is.Not.Null,app.StartupError);
            Assert.That(match.ReporterAI,Is.Null);Assert.That(match.Streamer.Controller.Driver,Is.Null);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming));
            Assert.That(match.Streamer.View.enabled,Is.False,"No opponent observer camera in multiplayer");
            Vector3 before=match.Reporter.transform.position;
            match.SimulateNetwork(new PlayerCommand{x=float.NaN},new PlayerCommand(),.02f);
            Assert.That(match.Reporter.transform.position,Is.EqualTo(before));
            var door=app.World.Items.First(i=>i.Data.type==ItemType.Door);door.SetDoorOpen(true);
            match.Reporter.Teleport(match.Streamer.transform.position-match.Streamer.transform.forward*2);
            match.Reporter.SetLookAt(match.Streamer.PhotoPoint);match.Reporter.SetPhone(true);
            Physics.SyncTransforms();match.TakePhoto();
            Assert.That(match.StreamerPhotos,Is.EqualTo(1));
            match.Streamer.Controller.Flap();
            for(int i=0;i<80;i++)
                match.SimulateNetwork(new PlayerCommand(),new PlayerCommand{action=match.Streamer.Controller.Level.WantsAutoFlap},1f/60);
            var snapshot=match.CaptureSnapshot();
            Assert.That(snapshot.noises.Any(n=>n.kind==NoiseKind.Shutter),Is.True);
            string wire=JsonUtility.ToJson(new LanMessage{type="snapshot",snapshot=snapshot});
            snapshot=JsonUtility.FromJson<LanMessage>(wire).snapshot;
            UnityEngine.Object.Destroy(root);yield return null;
            root=new GameObject("GuestReplicaTest");app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(data,rules,TestRole.Streamer,false);match=app.Session;
            match.ApplySnapshot(snapshot);yield return null;
            Assert.That(match.ReporterAI,Is.Null);Assert.That(match.Streamer.Controller.Driver,Is.Null);
            Assert.That(match.StreamerPhotos,Is.EqualTo(1));Assert.That(match.StreamerPhotoAvailable,Is.False);
            Assert.That(app.World.Find(doorId(snapshot)).IsOpen,Is.True);
            Assert.That(match.Streamer.Controller.Level.BirdHeight,Is.EqualTo(snapshot.flappy.height));
            int score=match.Score;match.TakePhoto();match.Streamer.Controller.BeginReturn();
            Assert.That(match.Score,Is.EqualTo(score));Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming));
            Assert.That(root.GetComponentsInChildren<AudioListener>().Count(l=>l.enabled),Is.EqualTo(1));
            snapshot.result="主播获胜：狗仔被灭口";match.ApplySnapshot(snapshot);
            Assert.That(match.Finished,Is.True);Assert.That(match.Result,Is.EqualTo(snapshot.result));
        }
        string doorId(MatchSnapshot state) => state.doors.First(d=>d.open).id;
    }
}
