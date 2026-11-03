using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class InvestigationTests
    {
        GameObject root,guestRoot;
        BuildingAsset building;
        PrototypeApp app;
        MatchSession match;
        TaskPropData prop;
        InvestigationTaskData task;
        void Start(InvestigationKind kind=InvestigationKind.Photograph,int mistakes=3,bool network=false)
        {
            var data=BuildingData.CreateDefault();data.useTaskEditor=true;
            prop=new TaskPropData{position=new Vector3(-4,.03f,-4)};data.taskProps.Add(prop);
            data.taskClues.Add(new TaskClueData{position=new Vector3(-5,.03f,-4),text="这条线索可以不读。"});
            task=new InvestigationTaskData{targetId=prop.id,kind=kind,regionPosition=new Vector3(-2,.4f,-4),regionSize=new Vector3(1.5f,.8f,1.5f)};
            task.clueIds.Add(data.taskClues[0].id);data.tasks.Add(task);
            building=ScriptableObject.CreateInstance<BuildingAsset>();building.SetData(data);
            root=new GameObject("InvestigationTest");app=root.AddComponent<PrototypeApp>();app.Building=building;app.Settings.maxScore=mistakes;
            if(network) app.StartNetworkSession(data,app.Settings,TestRole.Streamer,true);else app.EnterPlay();
            match=app.Session;Assert.That(match,Is.Not.Null,app.StartupError);
            if(match.ReporterAI) match.ReporterAI.enabled=false;match.Streamer.Controller.Driver=null;
            match.Reporter.enabled=false;match.Reporter.Teleport(new Vector3(-4,0,-6));match.Reporter.SetPhone(true);match.SelectTask(task.id);
            Aim();
        }
        void Aim() {match.Reporter.SetLookAt(match.TaskWorld.Props.Single().PhotoPoint);Physics.SyncTransforms();}
        [UnityTearDown] public IEnumerator Cleanup()
        {if(root) Object.Destroy(root);if(guestRoot) Object.Destroy(guestRoot);if(building) Object.Destroy(building);yield return null;}
        [UnityTest] public IEnumerator ReadingIsOptionalAndOnlyUploadCompletesSelectedTask()
        {
            Start();yield return null;
            Assert.That(match.TargetInPhoto(prop.id),Is.True);
            match.TakePhoto();Assert.That(match.PendingPhoto,Is.Not.Null);
            Assert.That(match.Score,Is.EqualTo(3));Assert.That(match.Quests.Single().Complete,Is.False);
            int id=match.PendingPhoto.id;match.UploadPhoto(id);
            Assert.That(match.Quests.Single().Complete,Is.True);Assert.That(match.Score,Is.EqualTo(3));
            match.UploadPhoto(id);Assert.That(match.Score,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator CancelIsFreeAndOnlyAnIncorrectUploadConsumesOneConfiguredAttempt()
        {
            Start(mistakes:5);yield return null;match.Reporter.SetLookAt(match.Reporter.View.transform.position+Vector3.up*10);
            match.TakePhoto();Assert.That(match.Score,Is.EqualTo(5));
            match.ApplyPhoneCommand(new PlayerCommand{cancelPhoto=true});Assert.That(match.PendingPhoto,Is.Null);Assert.That(match.Score,Is.EqualTo(5));
            for(int i=0;i<5;i++)
            {
                yield return new WaitForSeconds(.27f);match.TakePhoto();int id=match.PendingPhoto.id;
                Assert.That(match.Score,Is.EqualTo(5-i));match.UploadPhoto(id);match.UploadPhoto(id);
                Assert.That(match.Score,Is.EqualTo(4-i));
            }
            Assert.That(match.Finished,Is.True);
        }
        [UnityTest] public IEnumerator PlacementErrorsPrioritizeObjectAndPhotosRetainTheirCaptureState()
        {
            Start(InvestigationKind.PlaceAndPhotograph);yield return null;
            match.Reporter.SetLookAt(match.Reporter.View.transform.position+Vector3.up*10);match.TakePhoto();match.UploadPhoto(match.PendingPhoto.id);
            Assert.That(match.Message,Does.StartWith("物体错误"));
            yield return new WaitForSeconds(.27f);Aim();match.TakePhoto();
            Assert.That(match.PendingPhoto.objectCorrect,Is.True);Assert.That(match.PendingPhoto.regionCorrect,Is.False);
            match.UploadPhoto(match.PendingPhoto.id);Assert.That(match.Message,Does.StartWith("区域错误"));
            var target=match.TaskWorld.Props.Single();target.transform.position=new Vector3(-2,.03f,-4);
            match.Reporter.Teleport(new Vector3(-2,0,-6));Aim();yield return new WaitForSeconds(.27f);
            match.TakePhoto();Assert.That(match.PendingPhoto.regionCorrect,Is.True);
            target.transform.position=new Vector3(-6,.03f,-4);match.UploadPhoto(match.PendingPhoto.id);
            Assert.That(match.Quests.Single().Complete,Is.True,"Evidence is judged at capture time, not after a prop moves.");
        }
        [UnityTest] public IEnumerator CarryRequiresSurfaceAndBlocksPhotographyUntilPlaced()
        {
            Start();yield return null;match.Reporter.SetPhone(false);Aim();
            Assert.That(match.TaskWorld.Interact(),Is.True);Assert.That(match.TaskWorld.HeldId,Is.EqualTo(prop.id));
            Assert.That(match.TaskWorld.Held.Body.enabled,Is.False);
            match.Reporter.SetPhone(true);Assert.That(match.Reporter.PhoneOpen,Is.False);
            match.Reporter.SetLookAt(match.Reporter.View.transform.position+Vector3.up*10);
            Assert.That(match.TaskWorld.TryPlacement(out var unused,out var angle),Is.False);
            match.Reporter.SetLookAt(new Vector3(-2,0,-5));Physics.SyncTransforms();
            Assert.That(match.TaskWorld.TryPlacement(out var bottom,out angle),Is.True);
            match.TaskWorld.Interact();Assert.That(match.TaskWorld.HeldId,Is.Null);Assert.That(match.TaskWorld.Props.Single().Body.enabled,Is.True);
            Assert.That(match.TaskWorld.Props.Single().transform.position,Is.EqualTo(bottom));
        }
        [UnityTest] public IEnumerator NetworkSnapshotsConfirmPredictedCarryAndHostOwnsPhotoResults()
        {
            Start(network:true);yield return null;
            guestRoot=new GameObject("InvestigationGuest");var guest=guestRoot.AddComponent<PrototypeApp>();
            guest.StartNetworkSession(building.CreateRuntimeCopy(),app.Settings,TestRole.Reporter,false);
            var remote=guest.Session;
            MatchSnapshot Copy() => JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(match.CaptureSnapshot()));
            remote.ApplySnapshot(Copy());remote.Reporter.Teleport(match.Reporter.transform.position);remote.Reporter.SetLookAt(match.TaskWorld.Props.Single().PhotoPoint);
            match.Reporter.SetPhone(false);remote.Reporter.SetPhone(false);Physics.SyncTransforms();
            remote.TaskWorld.Interact(true,10);Assert.That(remote.TaskWorld.HeldId,Is.EqualTo(prop.id));
            var before=Copy();before.inputAck=9;remote.ApplySnapshot(before);Assert.That(remote.TaskWorld.HeldId,Is.EqualTo(prop.id));
            match.TaskWorld.Interact();var confirmed=Copy();confirmed.inputAck=10;remote.ApplySnapshot(confirmed);
            Assert.That(remote.TaskWorld.HeldId,Is.EqualTo(match.TaskWorld.HeldId));
            match.Reporter.SetLookAt(new Vector3(-2,0,-5));match.TaskWorld.Interact();match.Reporter.SetPhone(true);Aim();
            match.TakePhoto();remote.ApplySnapshot(Copy());Assert.That(remote.PendingPhoto.id,Is.EqualTo(match.PendingPhoto.id));
            remote.UploadPhoto(remote.PendingPhoto.id);Assert.That(remote.Quests.Single().Complete,Is.False);
            int id=match.PendingPhoto.id;match.SimulateNetworkActor(TestRole.Reporter,new PlayerCommand{uploadPhoto=id},.016f);remote.ApplySnapshot(Copy());
            Assert.That(remote.Quests.Single().Complete,Is.True);Assert.That(remote.Score,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator AiReporterCanCarryPlacePhotographAndUploadAnAuthoredTask()
        {
            Start(InvestigationKind.PlaceAndPhotograph);app.ReturnToMenu();app.Role=TestRole.Streamer;app.EnterPlay();match=app.Session;
            match.ReporterAI.enabled=false;match.Streamer.Controller.Driver=null;
            match.SelectTask(InvestigationData.LiveTask);match.Reporter.SetPhone(true);
            match.Reporter.Teleport(match.Streamer.transform.position-match.Streamer.transform.forward*2);match.Reporter.SetLookAt(match.Streamer.PhotoPoint);Physics.SyncTransforms();
            match.TakePhoto();match.UploadPhoto(match.PendingPhoto.id);
            match.Reporter.SetPhone(false);match.Reporter.Teleport(new Vector3(-6,0,-6));match.ReporterAI.enabled=true;
            float deadline=Time.time+25;
            while(Time.time<deadline && !match.Quests.Single().Complete) yield return null;
            Assert.That(match.Quests.Single().Complete,Is.True,match.ReporterAI.Status+" / "+match.Message);
        }
        [UnityTest] public IEnumerator LivePhotoReservesOneSlotPerBroadcastWithoutPenalizingDuplicateCapture()
        {
            Start();yield return null;match.Streamer.Controller.BeginStreaming();match.SelectTask(InvestigationData.LiveTask);
            match.Reporter.Teleport(match.Streamer.transform.position-match.Streamer.transform.forward*2);
            match.Reporter.SetLookAt(match.Streamer.PhotoPoint);Physics.SyncTransforms();
            match.TakePhoto();Assert.That(match.PendingPhoto.live,Is.True);Assert.That(match.StreamerPhotoAvailable,Is.False);
            int id=match.PendingPhoto.id;match.UploadPhoto(id);Assert.That(match.StreamerPhotos,Is.EqualTo(1));
            yield return new WaitForSeconds(.27f);match.TakePhoto();Assert.That(match.PendingPhoto,Is.Null);Assert.That(match.Score,Is.EqualTo(3));
        }
    }
}
