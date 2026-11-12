using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class RevisionFiveTests
    {
        GameObject root;
        PrototypeApp app;
        MatchSession Start(BuildingData data,bool hall)
        {
            root=new GameObject("Revision five");app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(data,new MatchSettings(),TestRole.Reporter,true,hall);
            Assert.That(app.Session,Is.Not.Null,app.StartupError);return app.Session;
        }
        [UnityTearDown] public IEnumerator Cleanup() {if(root) Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator CustomSpawnAndYawWorkInFormalMatch()
        {
            var data=BuildingData.CreateDefault();data.useCustomReporterSpawn=true;
            data.reporterSpawnCell=new Vector3Int(-2,1,-2);data.reporterSpawnYaw=125;
            var m=Start(data,false);
            Assert.That(Vector3.Distance(m.Reporter.transform.position,data.ReporterSpawnPosition),Is.LessThan(.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(m.Reporter.transform.eulerAngles.y,125)),Is.LessThan(.01f));yield return null;
        }
        [UnityTest] public IEnumerator HallDoesNotOverrideCustomReporterSpawn()
        {
            var data=TrainingHallLayout.Create();data.useCustomReporterSpawn=true;
            data.reporterSpawnCell=new Vector3Int(-2,0,-2);data.reporterSpawnYaw=70;
            var m=Start(data,true);
            Assert.That(Vector3.Distance(m.Reporter.transform.position,data.ReporterSpawnPosition),Is.LessThan(.01f));yield return null;
        }
        [UnityTest] public IEnumerator MapSwitchKeepsPhotoBoundToItsOriginalTaskAndUploadClosesPhone()
        {
            var m=Start(TrainingHallLayout.Create(),true);yield return null;
            m.EnterPhotoMode();var open=m.ConsumePhoneCommands();Assert.That(open.phone,Is.True);
            m.SimulateNetworkActor(TestRole.Reporter,open,.02f);Assert.That(m.CameraMode,Is.True);
            m.SimulateNetworkActor(TestRole.Reporter,new PlayerCommand{action=true},.02f);
            Assert.That(m.PendingPhoto,Is.Not.Null);var photo=m.PendingPhoto;Assert.That(m.PhoneMenuOpen,Is.True);
            m.OpenPhonePage(PhonePage.Map);Assert.That(m.HouseMap.IsOpen,Is.True);
            Assert.That(m.PendingPhoto,Is.SameAs(photo));m.OpenPhonePage(PhonePage.Tasks);
            Assert.That(m.PendingPhoto.taskId,Is.EqualTo("__practice"));
            m.QueuePhoneAction(upload:photo.id);m.SimulateNetworkActor(TestRole.Reporter,m.ConsumePhoneCommands(),.02f);
            Assert.That(m.Reporter.PhoneOpen,Is.False);Assert.That(m.PendingPhoto,Is.Null);Assert.That(m.UploadFeedbackSuccess,Is.True);
            Assert.That(m.GetComponentsInChildren<HouseMapBoard>(),Is.Empty);
        }
        [UnityTest] public IEnumerator PendingPhotoCanBeUploadedAfterPhoneWasClosed()
        {
            var m=Start(TrainingHallLayout.Create(),true);yield return null;
            m.Reporter.SetPhone(true);m.TakePhoto();var id=m.PendingPhoto.id;m.Reporter.SetPhone(false);
            m.QueuePhoneAction(upload:id);m.SimulateNetworkActor(TestRole.Reporter,m.ConsumePhoneCommands(),.02f);
            Assert.That(m.LastSubmittedPhoto,Is.EqualTo(id));Assert.That(m.Reporter.PhoneOpen,Is.False);
            Assert.That(m.UploadFeedbackId,Is.EqualTo(1));
            m.QueuePhoneAction(upload:id);m.SimulateNetworkActor(TestRole.Reporter,m.ConsumePhoneCommands(),.02f);
            Assert.That(m.UploadFeedbackId,Is.EqualTo(1));
        }
    }
}
