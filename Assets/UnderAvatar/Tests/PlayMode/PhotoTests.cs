using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class PhotoTests
    {
        GameObject root;
        PrototypeApp app;
        MatchSession match;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root=new GameObject("PhotoTest"); app=root.AddComponent<PrototypeApp>(); app.EnterPlay();
            Assert.That(app.Session,Is.Not.Null,app.StartupError);
            match=app.Session; match.Reporter.SetPhone(true);
            match.Reporter.enabled=false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(root); yield return null; }

        void Aim(PlacedItemView item)
        {
            match.Reporter.Teleport(item.transform.position+Vector3.back*2);
            match.Reporter.SetLookAt(item.PhotoPoint); Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator PhoneStartsOnFirstTaskAndUiUploadAdvancesSelection()
        {
            Assert.That(match.Reporter.PhoneOpen,Is.True);Assert.That(match.PhoneMenuOpen,Is.True);
            Assert.That(match.SelectedTaskId,Is.EqualTo(match.Quests.First().Id));
            var item=app.World.Items.First(i=>i.Data.type==ItemType.QuestItem && i.Floor==0);
            Aim(item);match.SelectTask(item.Data.questId);match.TakePhoto();
            int before=match.Quests.Count(q=>q.Complete),score=match.Score;
            int index=match.Quests.FindIndex(q=>q.Id==item.Data.questId);
            var next=match.Quests.Skip(index+1).Concat(match.Quests.Take(index+1)).First(q=>!q.Complete && q.Id!=item.Data.questId);
            match.ApplyPhoneCommand(new PlayerCommand{uploadPhoto=match.PendingPhoto.id});
            Assert.That(match.Quests.Count(q=>q.Complete),Is.EqualTo(before+1));
            Assert.That(match.SelectedTaskId,Is.EqualTo(next.Id));Assert.That(match.Score,Is.EqualTo(score));
            Assert.That(match.Reporter.PhoneOpen,Is.False);Assert.That(match.PhoneMenuOpen,Is.False);
            match.EnterPhotoMode();var command=match.ConsumePhoneCommands();
            Assert.That(command.phone,Is.True);match.Reporter.SetPhone(true);
            Assert.That(match.CameraMode,Is.True);Assert.That(match.PhoneMenuOpen,Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AValidEvidenceUploadCompletesQuestAndDuplicateDoesNotCostAnotherPoint()
        {
            var item=app.World.Items.First(i=>i.Data.type==ItemType.QuestItem && i.Floor==0);
            Aim(item); Assert.That(match.HasValidTarget,Is.True);
            match.SelectTask(item.Data.questId);match.TakePhoto();
            Assert.That(match.Quests.Single(q=>q.Id==item.Data.questId).Complete,Is.False);
            int id=match.PendingPhoto.id;match.UploadPhoto(id);
            Assert.That(match.Quests.Single(q=>q.Id==item.Data.questId).Complete,Is.True);
            Assert.That(match.Score,Is.EqualTo(3));
            yield return new WaitForSeconds(.3f);
            match.UploadPhoto(id);Assert.That(match.Score,Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator WallRangeAndViewfinderRejectInvalidTargets()
        {
            var item=app.World.Items.First(i=>i.Data.type==ItemType.QuestItem && i.Floor==0);
            Aim(item);
            var camera=match.Reporter.View;
            Assert.That(match.IsPhotographable(camera,item.PhotoPoint),Is.True);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform,false);
            wall.layer=PrototypeLayers.Get("UA_Wall");
            wall.transform.position=Vector3.Lerp(camera.transform.position,item.PhotoPoint,.5f);
            wall.transform.localScale=new Vector3(1,2,.2f); Physics.SyncTransforms();
            Assert.That(match.IsPhotographable(camera,item.PhotoPoint),Is.False);
            Object.Destroy(wall); yield return null;
            Assert.That(match.IsPhotographable(camera,camera.transform.position-camera.transform.forward*2),Is.False);
            Assert.That(match.IsPhotographable(camera,camera.ViewportToWorldPoint(new Vector3(.99f,.5f,2))),Is.False);
            Assert.That(match.IsPhotographable(camera,camera.ViewportToWorldPoint(new Vector3(.5f,.1f,2))),Is.False);
            Assert.That(match.IsPhotographable(camera,camera.transform.position+camera.transform.forward*match.Settings.maxPhotoDistance),Is.False);
        }

        [UnityTest]
        public IEnumerator ThreeEmptyUploadsEndMatchAndFurtherPhotosCannotChangeScore()
        {
            match.Reporter.SetLookAt(match.Reporter.View.transform.position+Vector3.up*10);
            for(int i=0;i<3;i++) { match.TakePhoto();match.UploadPhoto(match.PendingPhoto.id);yield return new WaitForSeconds(.3f); }
            Assert.That(match.Score,Is.Zero);
            Assert.That(match.Result,Does.Contain("失败"));
            match.TakePhoto(); Assert.That(match.Score,Is.Zero);
        }
    }
}
