using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class PredictionTests
    {
        GameObject root;
        MatchSnapshot initial;
        MatchSession match;
        ClientPrediction prediction;
        IEnumerator Setup(TestRole role)
        {
            root=new GameObject("PredictionAuthority");
            var app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),LanGame.Opposite(role),true);
            initial=app.Session.CaptureSnapshot();
            Object.Destroy(root);yield return null;
            root=new GameObject("PredictionClient");app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),role,false);
            match=app.Session;prediction=new ClientPrediction(match);prediction.Receive(initial);
        }
        [UnityTearDown] public IEnumerator TearDown() { if(root) Object.Destroy(root);yield return null; }

        [UnityTest] public IEnumerator DelayedPhoneSnapshotKeepsMapOpenAndEmptyWirePhotoIsNotPending()
        {
            yield return Setup(TestRole.Reporter);
            initial=JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(initial));
            prediction.Receive(initial);Assert.That(match.PendingPhoto,Is.Null);
            match.OpenPhonePage(PhonePage.Map);
            prediction.Tick(match.ConsumePhoneCommands(),ClientPrediction.Step,_=>{});
            Assert.That(match.HouseMap.IsOpen,Is.True);
            prediction.Receive(initial); // Host has not acknowledged the open-phone input yet.
            Assert.That(match.HouseMap.IsOpen,Is.True);Assert.That(match.PhoneMenuOpen,Is.True);
            initial.inputAck=1;initial.reporter=match.Reporter.CapturePose();
            prediction.Receive(initial);Assert.That(match.HouseMap.IsOpen,Is.True);
            match.QueuePhoneAction(close:true);prediction.Tick(match.ConsumePhoneCommands(),ClientPrediction.Step,_=>{});
            prediction.Receive(initial);Assert.That(match.Reporter.PhoneOpen,Is.False);
            Assert.That(match.PendingPhoto,Is.Null);
        }

        [UnityTest] public IEnumerator LocalMouseAndMovementRespondBeforeAnyHostReplyAndReplayDoesNotDoubleInput()
        {
            yield return Setup(TestRole.Reporter);
            Vector3 start=match.Reporter.transform.position;
            prediction.Tick(new PlayerCommand{lookX=100},.001f,_=>{});
            Assert.That(Mathf.DeltaAngle(0,match.Reporter.transform.eulerAngles.y),Is.EqualTo(12).Within(.01f),
                "Mouse must react in the render frame, before a network tick or reply.");
            ActorSnapshot ackPose=null;
            for(int i=0;i<12;i++)
            {
                prediction.Tick(new PlayerCommand{y=1,phone=i==0},ClientPrediction.Step,_=>{});
                if(i==4) ackPose=match.Reporter.CapturePose();
            }
            Vector3 predicted=match.Reporter.transform.position;
            Assert.That(Vector3.Distance(start,predicted),Is.GreaterThan(.35f));
            Assert.That(prediction.PendingCount,Is.EqualTo(12));Assert.That(match.Reporter.PhoneOpen,Is.True);
            prediction.Receive(initial); // A delayed ack for tick 0 must replay all pending movement.
            Assert.That(Vector3.Distance(match.Reporter.transform.position,predicted),Is.LessThan(.04f));
            Assert.That(Mathf.DeltaAngle(0,match.Reporter.transform.eulerAngles.y),Is.EqualTo(12).Within(.01f));
            initial.inputAck=5;initial.reporter=ackPose;
            prediction.Receive(initial);
            Assert.That(prediction.PendingCount,Is.EqualTo(7));
            Assert.That(Vector3.Distance(match.Reporter.transform.position,predicted),Is.LessThan(.04f));
            Assert.That(match.Reporter.PhoneOpen,Is.True,"Acknowledged phone input must not be repeated.");
            Assert.That(match.Pulses.Count,Is.Zero,"Prediction must not duplicate authoritative sounds.");
        }
        [UnityTest] public IEnumerator ClientFlapRespondsImmediatelyButCannotUnlockStreamingItself()
        {
            yield return Setup(TestRole.Streamer);
            var brain=match.Streamer.Controller;
            float before=brain.Level.BirdHeight;
            prediction.Tick(new PlayerCommand{action=true},ClientPrediction.Step,_=>{});
            Assert.That(brain.Level.Running,Is.True);
            Assert.That(brain.Level.BirdHeight,Is.GreaterThan(before));
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming));
            brain.BeginReturn();Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming));
            Assert.That(brain.TryCapture(),Is.False);
            prediction.Receive(initial);
            Assert.That(brain.Level.Running,Is.True,"Unacknowledged flap must survive a delayed snapshot.");
            Assert.That(brain.Level.BirdHeight,Is.EqualTo(before+.56f*ClientPrediction.Step).Within(.003f));
        }
        [UnityTest] public IEnumerator TenSecondStallKeepsReporterResponsiveAndBoundsCorrectionWork()
        {
            yield return Setup(TestRole.Reporter);
            match.Reporter.Teleport(new Vector3(100,0,100));
            var start=match.Reporter.transform.position;
            for(int i=0;i<600;i++) prediction.Tick(new PlayerCommand{y=1,lookX=.1f},ClientPrediction.Step,_=>{});
            var before=match.Reporter.CapturePose();
            Assert.That(Vector3.Distance(start,before.position),Is.GreaterThan(10));
            prediction.Receive(initial);
            Assert.That(prediction.CorrectionDeferred,Is.True);Assert.That(prediction.LastReplayCount,Is.Zero);
            Assert.That(Vector3.Distance(before.position,match.Reporter.transform.position),Is.LessThan(.001f));
            Assert.That(Mathf.DeltaAngle(before.yaw,match.Reporter.transform.eulerAngles.y),Is.EqualTo(0).Within(.01f));
            initial.inputAck=590;initial.reporter=before;
            prediction.Receive(initial);Assert.That(prediction.PendingCount,Is.EqualTo(10));
            Assert.That(prediction.LastReplayCount,Is.EqualTo(10));
        }
        [UnityTest] public IEnumerator TenSecondStallKeepsStreamerMouseResponsiveWithoutHostReplies()
        {
            yield return Setup(TestRole.Streamer);initial.state=StreamerState.Roaming;prediction.Receive(initial);
            var brain=match.Streamer.Controller;
            for(int i=0;i<600;i++) prediction.Tick(new PlayerCommand{lookX=.1f},ClientPrediction.Step,_=>{});
            var before=brain.CapturePose();prediction.Receive(initial);
            Assert.That(prediction.CorrectionDeferred,Is.True);Assert.That(prediction.LastReplayCount,Is.Zero);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before.yaw,match.Streamer.transform.eulerAngles.y)),Is.LessThan(.01f));
            Assert.That(prediction.PendingCount,Is.EqualTo(600));
        }
    }
}
