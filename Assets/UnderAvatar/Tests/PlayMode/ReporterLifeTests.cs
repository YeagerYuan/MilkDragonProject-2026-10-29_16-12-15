using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class ReporterLifeTests
    {
        GameObject hostRoot,guestRoot;
        MatchSession host;
        void Start()
        {
            hostRoot=new GameObject("Life authority");var app=hostRoot.AddComponent<PrototypeApp>();
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Streamer,true);
            host=app.Session;Assert.That(host,Is.Not.Null,app.StartupError);host.enabled=false;
            host.Streamer.Controller.enabled=false;host.Abilities.enabled=false;
        }
        [UnityTearDown] public IEnumerator Cleanup() {if(hostRoot) Object.Destroy(hostRoot);if(guestRoot) Object.Destroy(guestRoot);yield return null;}
        [UnityTest] public IEnumerator CapturePreservesTasksAndMistakesButClearsPhotoThenRespawnsWithProtection()
        {
            Start();yield return null;host.Quests[0].Complete=true;
            host.Reporter.SetPhone(true);
            typeof(MatchSession).GetProperty("PendingPhoto").SetValue(host,new PhotoEvidence{id=9,taskId=host.Quests.Last().Id});
            int score=host.Score;host.Reporter.Teleport(host.ReporterSpawn+Vector3.right*2);
            Assert.That(host.CaptureReporter(),Is.True);Assert.That(host.CaptureReporter(),Is.False);
            Assert.That(host.ReporterLives,Is.EqualTo(2));Assert.That(host.PendingPhoto,Is.Null);
            Assert.That(host.Reporter.GetComponent<CharacterController>().enabled,Is.False);
            var death=host.Reporter.transform.position;
            host.SimulateNetworkActor(TestRole.Reporter,new PlayerCommand{y=1,reporterLifeEpoch=host.ReporterLifeEpoch},.1f);
            Assert.That(host.Reporter.transform.position,Is.EqualTo(death));
            host.TickReporterLife(2.5f);Assert.That(host.ReporterDying,Is.False);
            Assert.That(host.Reporter.transform.position,Is.EqualTo(host.ReporterSpawn));
            Assert.That(host.ReporterProtected,Is.True);Assert.That(host.CaptureReporter(),Is.False);
            host.Reporter.SetPhone(true);host.TakePhoto();Assert.That(host.PendingPhoto,Is.Null);
            Assert.That(host.Quests[0].Complete,Is.True);Assert.That(host.Score,Is.EqualTo(score));
            host.TickReporterLife(3);Assert.That(host.CaptureReporter(),Is.True);
        }
        [UnityTest] public IEnumerator ThirdCaptureFinishesAfterDeathAnimationWithoutFourthLife()
        {
            Start();yield return null;
            for(int i=0;i<3;i++)
            {
                Assert.That(host.CaptureReporter(),Is.True);Assert.That(host.Finished,Is.False);
                if(i==2) {host.Finish("狗仔获胜：逾期开播");Assert.That(host.Finished,Is.False,"Final capture reserves the result until its animation finishes.");}
                host.TickReporterLife(2.5f);
                if(i<2) {Assert.That(host.Finished,Is.False);host.TickReporterLife(3);}
            }
            Assert.That(host.ReporterLives,Is.Zero);Assert.That(host.Result,Does.Contain("主播获胜"));
            Assert.That(host.CaptureReporter(),Is.False);
        }
        [UnityTest] public IEnumerator StaleInputCannotMoveOrPhotographAfterRespawn()
        {
            Start();yield return null;int old=host.ReporterLifeEpoch;
            host.CaptureReporter();host.TickReporterLife(2.5f);host.TickReporterLife(3);
            Vector3 spawn=host.Reporter.transform.position;
            host.SimulateNetworkActor(TestRole.Reporter,new PlayerCommand{reporterLifeEpoch=old,y=1,phone=true,action=true},.1f);
            Assert.That(host.Reporter.transform.position,Is.EqualTo(spawn));Assert.That(host.Reporter.PhoneOpen,Is.False);
        }
        [UnityTest] public IEnumerator RespawnClearsUnacknowledgedPredictionEvenWhenHostAckHasNotChanged()
        {
            Start();yield return null;
            guestRoot=new GameObject("Life guest");var app=guestRoot.AddComponent<PrototypeApp>();
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Reporter,false);
            var client=app.Session;client.enabled=false;var prediction=new ClientPrediction(client);
            prediction.Receive(host.CaptureSnapshot());
            for(int i=0;i<12;i++) prediction.Tick(new PlayerCommand{y=1},ClientPrediction.Step,_=>{});
            Assert.That(prediction.PendingCount,Is.GreaterThan(0));host.CaptureReporter();
            prediction.Receive(host.CaptureSnapshot());Assert.That(client.ReporterDying,Is.True);Assert.That(prediction.PendingCount,Is.Zero);
            host.TickReporterLife(2.5f);prediction.Receive(host.CaptureSnapshot());
            Assert.That(client.ReporterDying,Is.False);Assert.That(client.ReporterProtected,Is.True);
            Assert.That(Vector3.Distance(client.Reporter.transform.position,host.ReporterSpawn),Is.LessThan(.01f));
            Assert.That(prediction.LastReplayCount,Is.Zero);Assert.That(prediction.CorrectionDeferred,Is.False);
            Assert.That(client.ReporterLives,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator FloorSoundAttenuatesLocallyAndPrivateFeedbackRemainsClear()
        {
            Start();yield return null;
            var profile=ScriptableObject.CreateInstance<GameAudioProfile>();var node=new GameObject("Floor audio test");node.transform.SetParent(hostRoot.transform);
            var source=node.AddComponent<AudioSource>();var spatial=node.AddComponent<FloorSpatialAudio>();
            node.transform.position=new Vector3(0,0,0);spatial.Initialize(host,source,profile,.8f);
            host.Streamer.transform.position=new Vector3(0,host.World.Data.floorHeight,0);spatial.Refresh();
            Assert.That(source.volume,Is.EqualTo(.8f*.45f).Within(.0001f));Assert.That(spatial.FloorDifference,Is.EqualTo(1));
            Assert.That(node.GetComponent<AudioLowPassFilter>().enabled,Is.True);
            host.Streamer.transform.position=Vector3.zero;spatial.Refresh();Assert.That(source.volume,Is.EqualTo(.8f));
            Assert.That(node.GetComponent<AudioLowPassFilter>().enabled,Is.False);
            host.GetComponent<PrototypeAudio>().UploadFeedback(true);
            Assert.That(host.GetComponentsInChildren<FloorSpatialAudio>().All(a=>a.GetComponent<AudioSource>().spatialBlend>0 || a==spatial),Is.True);
            Object.Destroy(profile);
        }
    }
}
