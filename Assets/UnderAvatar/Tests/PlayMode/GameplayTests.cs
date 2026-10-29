using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class GameplayTests
    {
        GameObject root;
        PrototypeApp app;
        MatchSession match;
        StreamerController brain;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root=new GameObject("GameplayTest"); app=root.AddComponent<PrototypeApp>(); app.EnterPlay();
            Assert.That(app.Session,Is.Not.Null,app.StartupError);
            match=app.Session; brain=match.Streamer.Controller;
            brain.Driver=null; match.Reporter.enabled=false;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator StreamingCannotCaptureAndOnlyCompletionUnlocksCapture()
        {
            brain.BeginStreaming();
            Vector3 position=brain.transform.position;
            match.Reporter.Teleport(position-brain.transform.forward*.9f);
            Assert.That(brain.TryCapture(),Is.False);
            yield return new WaitForSeconds(.4f);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming));
            Assert.That(match.Streamer.Progress,Is.Zero);
            Assert.That(Vector2.Distance(new Vector2(brain.transform.position.x,brain.transform.position.z),new Vector2(position.x,position.z)),Is.LessThan(.01f));
            FinishFlappy(brain);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Roaming));
            Assert.That(brain.TryCapture(),Is.True);
            Assert.That(match.Result,Does.Contain("主播获胜"));
        }

        [UnityTest]
        public IEnumerator WallPreventsCaptureAndWardrobePreventsPhotographyAndCapture()
        {
            brain.enabled=false;
            var reporter=match.Reporter;
            reporter.Teleport(brain.transform.position-Vector3.forward*.9f);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform,false);
            wall.layer=PrototypeLayers.Get("UA_Wall"); wall.transform.position=(brain.transform.position+reporter.transform.position)*.5f+Vector3.up;
            wall.transform.localScale=new Vector3(1,2,.1f); Physics.SyncTransforms();
            Assert.That(brain.TryCapture(),Is.False);
            Object.Destroy(wall); yield return null;
            var wardrobe=app.World.Items.First(i=>i.Floor==0 && i.Data.hideable);
            reporter.Teleport(wardrobe.Front); reporter.Hide(wardrobe); reporter.SetPhone(true);
            Assert.That(reporter.Hidden,Is.True); Assert.That(reporter.PhoneOpen,Is.False);
            int score=match.Score; match.TakePhoto(); Assert.That(match.Score,Is.EqualTo(score));
            brain.transform.position=reporter.transform.position;
            Assert.That(brain.TryCapture(),Is.False);
            reporter.Interact(); Assert.That(reporter.Hidden,Is.False);
        }

        [UnityTest]
        public IEnumerator EmergencyTurnHasWindupMissPenaltyAndStun()
        {
            brain.BeginStreaming(); brain.Flap();
            Assert.That(brain.TryEmergencyTurn(),Is.False);
            match.Settings.allowEmergencyTurn=true;
            Assert.That(brain.TryEmergencyTurn(),Is.True);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Turning));
            yield return new WaitForSeconds(.65f);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Ambush));
            yield return new WaitForSeconds(3.05f);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Stunned));
            Assert.That(brain.Level.Score,Is.Zero);
            yield return new WaitForSeconds(3.1f);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Returning).Or.EqualTo(StreamerState.Streaming));
        }

        [UnityTest]
        public IEnumerator EvidenceRequiresThreeBroadcastsAndCollisionDoesNotRenewPhotoQuota()
        {
            brain.enabled=false;
            var reporter=match.Reporter; reporter.SetPhone(true);
            foreach(var item in app.World.Items.Where(i=>i.Data.type==ItemType.QuestItem))
            {
                reporter.Teleport(item.transform.position+Vector3.back*2); reporter.SetLookAt(item.PhotoPoint);
                match.TakePhoto(); yield return new WaitForSeconds(.3f);
            }
            Assert.That(match.Quests.All(q=>q.Complete),Is.True);
            brain.BeginStreaming();
            reporter.Teleport(brain.transform.position-brain.transform.forward*2); reporter.SetLookAt(match.Streamer.PhotoPoint);
            match.TakePhoto(); Assert.That(match.StreamerPhotos,Is.EqualTo(1));
            Assert.That(match.FlashUntil,Is.GreaterThan(Time.time+.25f));
            Assert.That(match.Streamer.AlertUntil,Is.GreaterThan(Time.time));
            Assert.That(match.Pulses.Any(p=>p.Kind==NoiseKind.Shutter),Is.True);
            yield return new WaitForSeconds(.3f);
            match.TakePhoto(); Assert.That(match.StreamerPhotos,Is.EqualTo(1)); Assert.That(match.Score,Is.EqualTo(2));
            brain.Flap();
            for(int i=0;i<300 && !brain.Level.Crashed;i++) brain.AdvanceMinigame(1f/60);
            Assert.That(brain.Level.Crashed,Is.True);
            brain.Flap(); Assert.That(match.StreamerPhotoAvailable,Is.False);
            yield return new WaitForSeconds(5.05f);
            Assert.That(match.HasValidTarget,Is.False,"Elapsed time must not refresh a broadcast photo.");
            FinishFlappy(brain); brain.BeginStreaming();
            match.TakePhoto(); Assert.That(match.StreamerPhotos,Is.EqualTo(2));
            yield return new WaitForSeconds(.3f);
            FinishFlappy(brain); brain.BeginStreaming();
            match.TakePhoto(); Assert.That(match.Result,Does.Contain("狗仔获胜"));
        }

        [UnityTest]
        public IEnumerator AiReturnsToDeskPlaysRealInputsAndFinishesALevel()
        {
            brain.Driver=new AiStreamerDriver();
            match.Settings.flappyTargetScore=3;
            brain.BeginReturn();
            float deadline=Time.time+15;
            bool streaming=false,completed=false;
            while(Time.time<deadline)
            {
                streaming|=match.Streamer.State==StreamerState.Streaming;
                if(streaming && match.Streamer.State==StreamerState.Roaming && match.Streamer.Progress==1) { completed=true; break; }
                yield return null;
            }
            Assert.That(streaming,Is.True,"AI failed to reach the computer.");
            Assert.That(completed,Is.True,"AI failed to pass Flappy Bird pipes.");
        }

        [UnityTest]
        public IEnumerator StreamerCanPhysicallyReturnFromAnotherFloorThroughDoorsAndStairs()
        {
            match.Reporter.Hide(app.World.Items.First(i=>i.Floor==0 && i.Data.hideable));
            brain.transform.position=new Vector3(-8,0,-4);
            match.Settings.streamerSpeed=8;
            brain.BeginReturn();
            float deadline=Time.time+15;
            bool climbed=false;
            float previousHeight=brain.transform.position.y;
            while(Time.time<deadline && match.Streamer.State==StreamerState.Returning)
            {
                yield return null;
                float height=brain.transform.position.y;
                Assert.That(Mathf.Abs(height-previousHeight),Is.LessThan(.65f),"AI must walk stairs rather than teleport floors");
                climbed|=height>.4f && height<app.World.Data.floorHeight-.4f;
                previousHeight=height;
            }
            Assert.That(climbed,Is.True,"AI never traversed the intermediate stair height");
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming),"Return position: "+brain.transform.position);
            Assert.That(match.Streamer.Floor,Is.EqualTo(1));
        }

        public static void FinishFlappy(StreamerController controller)
        {
            for(int i=0;i<7200 && controller.Actor.State==StreamerState.Streaming;i++)
            {
                if(controller.Level.WantsAutoFlap) controller.Flap();
                controller.AdvanceMinigame(1f/60);
            }
            Assert.That(controller.Level.Complete,Is.True,"Flappy Bird must be completed using flap inputs.");
        }
    }
}
