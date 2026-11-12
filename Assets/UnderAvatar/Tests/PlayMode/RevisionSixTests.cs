using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class RevisionSixTests
    {
        GameObject root,guestRoot;BuildingAsset asset;
        PrototypeApp app;MatchSession m;
        void Start(TestRole role=TestRole.Streamer,bool authority=true,BuildingData data=null)
        {
            root=new GameObject("Revision six");app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(data??BuildingData.CreateDefault(),new MatchSettings(),role,authority);
            m=app.Session;Assert.That(m,Is.Not.Null,app.StartupError);
            m.enabled=false;m.Reporter.enabled=false;m.Streamer.Controller.enabled=false;m.Abilities.enabled=false;
            m.Reporter.Teleport(new Vector3(100,0,106));
            m.Streamer.GetComponent<CharacterController>().enabled=false;m.Streamer.transform.SetPositionAndRotation(new Vector3(100,0,100),Quaternion.identity);
        }
        [UnityTearDown] public IEnumerator Cleanup() {if(root) Object.Destroy(root);if(guestRoot) Object.Destroy(guestRoot);if(asset) Object.Destroy(asset);yield return null;}
        [UnityTest] public IEnumerator DecoyConsumesOnceCanBeDetectedAndEndsHuntOnTouch()
        {
            Start();yield return null;var a=m.Abilities;
            Assert.That(a.TryPlaceDecoy(),Is.True);Assert.That(a.DecoyCharges,Is.Zero);Assert.That(a.TryPlaceDecoy(),Is.False);
            m.Streamer.View.transform.rotation=Quaternion.LookRotation(m.Reporter.transform.position+Vector3.up-m.Streamer.View.transform.position);
            a.Tick(.1f,true);Assert.That(a.CaptureDecoys().Single().revealed,Is.GreaterThan(0));
            typeof(StreamerActor).GetProperty("State").SetValue(m.Streamer,StreamerState.Roaming);m.Streamer.transform.position=m.Reporter.transform.position+Vector3.back*.5f;
            Assert.That(m.Streamer.Controller.TryCapture(),Is.False);Assert.That(m.ReporterLives,Is.EqualTo(3));
            a.Tick(.1f,false);Assert.That(a.PlacedDecoyCount,Is.Zero);Assert.That(m.Streamer.State,Is.EqualTo(StreamerState.Returning));
            Assert.That(m.Streamer.Controller.RoamRemaining,Is.Zero);
        }
        [UnityTest] public IEnumerator DecoyRemovalAndChargesReplicateThroughWireSnapshots()
        {
            Start();yield return null;m.Abilities.TryPlaceDecoy();
            guestRoot=new GameObject("Decoy client");var guest=guestRoot.AddComponent<PrototypeApp>();
            guest.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Reporter,false);
            var client=guest.Session;
            client.ApplySnapshot(JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(m.CaptureSnapshot())));
            Assert.That(client.Abilities.PlacedDecoyCount,Is.EqualTo(1));Assert.That(client.Abilities.DecoyCharges,Is.Zero);
            client.ApplySnapshot(JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(m.CaptureSnapshot())));
            Assert.That(client.Abilities.PlacedDecoyCount,Is.EqualTo(1));
            m.Streamer.transform.position=m.Reporter.transform.position;m.Abilities.Tick(.1f,false);
            client.ApplySnapshot(JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(m.CaptureSnapshot())));
            Assert.That(client.Abilities.PlacedDecoyCount,Is.Zero);
        }
        [UnityTest] public IEnumerator ExplicitTaskRewardsAndDuplicatesAreIndependentOfDefaultRewards()
        {
            var data=BuildingData.CreateDefault();data.tasks=InvestigationData.Tasks(data);data.useTaskEditor=true;
            data.tasks[0].reward=TaskReward.Decoy;data.tasks[1].reward=TaskReward.Invisibility;
            Start(data:data);yield return null;m.Reporter.SetPhone(true);
            for(int i=0;i<3;i++)
            {
                var photo=new PhotoEvidence{id=i+1,taskId=m.Quests[i].Id,objectCorrect=true,regionCorrect=true};
                typeof(MatchSession).GetProperty("PendingPhoto").SetValue(m,photo);m.UploadPhoto(photo.id);m.UploadPhoto(photo.id);
            }
            Assert.That(m.Abilities.DecoyCharges,Is.EqualTo(2));Assert.That(m.Abilities.InvisibleCharges,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator DelayedTcpDecoyCommandIsAuthoritativeAndSnapshotRemovesIt()
        {
            Start();yield return null;
            guestRoot=new GameObject("TCP decoy guest");var guest=guestRoot.AddComponent<PrototypeApp>();
            guest.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Reporter,false);
            var server=new LanTransport();var peer=new LanTransport();
            try
            {
                server.Listen(0);peer.Connect("127.0.0.1",server.Port);
                float deadline=Time.realtimeSinceStartup+5;
                while((!server.Connected || !peer.Connected) && Time.realtimeSinceStartup<deadline) {server.Poll();peer.Poll();yield return null;}
                Assert.That(server.Connected && peer.Connected,Is.True);
                server.Message+=text=>
                {
                    var message=JsonUtility.FromJson<LanMessage>(text);Assert.That(message.protocol,Is.EqualTo(LanMessage.Version));
                    m.SimulateNetworkActor(TestRole.Reporter,message.input,ClientPrediction.Step);
                    server.Send(JsonUtility.ToJson(new LanMessage{type="snapshot",snapshot=m.CaptureSnapshot()}));
                };
                peer.Message+=text=>guest.Session.ApplySnapshot(JsonUtility.FromJson<LanMessage>(text).snapshot);
                peer.Send(JsonUtility.ToJson(new LanMessage{type="input",input=new PlayerCommand{decoy=true,reporterLifeEpoch=m.ReporterLifeEpoch}}));
                Assert.That(m.Abilities.PlacedDecoyCount,Is.Zero);
                yield return new WaitForSeconds(.12f);server.Poll();yield return new WaitForSeconds(.12f);
                while(guest.Session.Abilities.PlacedDecoyCount==0 && Time.realtimeSinceStartup<deadline) {server.Poll();peer.Poll();yield return null;}
                Assert.That(m.Abilities.PlacedDecoyCount,Is.EqualTo(1));Assert.That(guest.Session.Abilities.PlacedDecoyCount,Is.EqualTo(1));
                m.Streamer.transform.position=m.Reporter.transform.position;m.Abilities.ConsumeTouchingDecoys();
                server.Send(JsonUtility.ToJson(new LanMessage{type="snapshot",snapshot=m.CaptureSnapshot()}));
                yield return new WaitForSeconds(.12f);
                while(guest.Session.Abilities.PlacedDecoyCount!=0 && Time.realtimeSinceStartup<deadline) {server.Poll();peer.Poll();yield return null;}
                Assert.That(guest.Session.Abilities.PlacedDecoyCount,Is.Zero);Assert.That(guest.Session.Abilities.DecoyCharges,Is.Zero);
            }
            finally {peer.Dispose();server.Dispose();}
        }
        [UnityTest] public IEnumerator EveryStreamerTaskSharesOneBroadcastPhotoSlot()
        {
            var data=BuildingData.CreateDefault();data.useTaskEditor=true;data.requireLiveEvidence=false;
            data.tasks.Add(new InvestigationTaskData{targetId=InvestigationData.StreamerTarget,title="主播一"});
            data.tasks.Add(new InvestigationTaskData{targetId=InvestigationData.StreamerTarget,title="主播二"});
            Start(data:data);yield return null;
            m.Reporter.Teleport(m.Streamer.transform.position+Vector3.back*2);m.Reporter.SetLookAt(m.Streamer.PhotoPoint);m.Reporter.SetPhone(true);Physics.SyncTransforms();
            m.SelectTask(data.tasks[0].id);m.TakePhoto();Assert.That(m.PendingPhoto,Is.Not.Null);Assert.That(m.PendingPhoto.objectCorrect,Is.True);
            m.UploadPhoto(m.PendingPhoto.id);m.SelectTask(data.tasks[1].id);
            yield return new WaitForSeconds(.3f);m.TakePhoto();Assert.That(m.PendingPhoto,Is.Null);
            Assert.That(m.Message,Does.Contain("每场直播"));Assert.That(m.Score,Is.EqualTo(3));
            GameplayTests.FinishFlappy(m.Streamer.Controller);m.Streamer.Controller.BeginStreaming();
            yield return new WaitForSeconds(.3f);
            m.Reporter.Teleport(m.Streamer.transform.position+m.Streamer.transform.forward*2);m.Reporter.SetLookAt(m.Streamer.PhotoPoint);m.TakePhoto();Assert.That(m.PendingPhoto,Is.Not.Null);
        }
        [UnityTest] public IEnumerator StreamerMapCannotOpenDuringStreamAndUsesSameFloorData()
        {
            Start();yield return null;m.SetStreamerMap(true);Assert.That(m.StreamerMapOpen,Is.False);
            typeof(StreamerActor).GetProperty("State").SetValue(m.Streamer,StreamerState.Returning);m.SetStreamerMap(true);
            Assert.That(m.HouseMap.IsOpen,Is.True);Assert.That(m.LocalPhoneMenuOpen,Is.True);
            m.HouseMap.SelectFloor(1);Assert.That(m.HouseMap.Floor,Is.EqualTo(1));
            m.HouseMap.Close();Assert.That(m.StreamerMapOpen,Is.False);
            m.Streamer.Controller.BeginStreaming();Assert.That(m.Streamer.Controller.Fans,Is.EqualTo(5000));
        }
        [UnityTest] public IEnumerator DraftTasksAreSkippedWithoutMutatingAuthoredAsset()
        {
            var data=BuildingData.CreateDefault();data.tasks=InvestigationData.Tasks(data);data.useTaskEditor=true;data.tasks.Add(new InvestigationTaskData{title="未完成草稿"});
            asset=ScriptableObject.CreateInstance<BuildingAsset>();asset.SetData(data);
            Start(data:asset.CreateRuntimeCopy());yield return null;
            Assert.That(m.Quests.Count,Is.EqualTo(3));Assert.That(m.Message,Does.Contain("未完成草稿"));Assert.That(asset.Data.tasks.Count,Is.EqualTo(4));
        }
        [Test] public void FailureReductionIsCappedAndResetsAtNextBroadcast()
        {
            var level=new MinigameLevel(5){MaxFailureReduction=4};level.Start(1,12);
            for(int i=0;i<7;i++) {level.Flap();while(level.Running) level.Tick(.25f);Assert.That(level.TargetScore,Is.EqualTo(12-Mathf.Min(i+1,4)));}
            level.Start(2,12);Assert.That(level.TargetScore,Is.EqualTo(12));
            level.Start(3,2);for(int i=0;i<5;i++) {level.Flap();while(level.Running) level.Tick(.25f);}Assert.That(level.TargetScore,Is.EqualTo(1));
        }
    }
}
