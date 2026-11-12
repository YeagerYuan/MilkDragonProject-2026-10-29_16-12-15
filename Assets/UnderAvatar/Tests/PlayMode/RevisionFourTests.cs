using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class RevisionFourTests
    {
        GameObject root,guestRoot;
        PrototypeApp app;
        MatchSession Start(TestRole role,bool hall=true)
        {
            root=new GameObject("Revision four");app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(hall?TrainingHallLayout.Create():BuildingData.CreateDefault(),new MatchSettings(),role,true,hall);
            Assert.That(app.Session,Is.Not.Null,app.StartupError);return app.Session;
        }
        [UnityTearDown] public IEnumerator Cleanup() {if(root) Object.Destroy(root);if(guestRoot) Object.Destroy(guestRoot);yield return null;}
        [UnityTest] public IEnumerator HallPhotoHasPreviewUploadFeedbackAndNoScoreOrRewardChanges()
        {
            var m=Start(TestRole.Reporter);yield return null;
            int score=m.Score,charges=m.Abilities.InvisibleCharges;m.Reporter.SetPhone(true);m.SetPhoneMenu(false);m.TakePhoto();
            Assert.That(m.PendingPhoto,Is.Not.Null);Assert.That(m.PhotoPreview,Is.Not.Null);Assert.That(m.PhoneMenuOpen,Is.True);
            int photo=m.PendingPhoto.id;m.UploadPhoto(photo);Assert.That(m.UploadFeedbackSuccess,Is.True);Assert.That(m.UploadFeedbackId,Is.EqualTo(1));
            m.UploadPhoto(photo);Assert.That(m.UploadFeedbackId,Is.EqualTo(1));Assert.That(m.Score,Is.EqualTo(score));Assert.That(m.Abilities.InvisibleCharges,Is.EqualTo(charges));
            Assert.That(m.Quests,Is.Empty);Assert.That(m.Finished,Is.False);
        }
        [UnityTest] public IEnumerator HallSkillsCanBeRepeatedAndDetectionWorksThroughClosedDoor()
        {
            var m=Start(TestRole.Streamer);m.Abilities.enabled=false;yield return null;
            Assert.That(m.World.Items.First(i=>i.Data.type==ItemType.Door).IsOpen,Is.False);
            int charges=m.Abilities.InvisibleCharges;
            Assert.That(m.Abilities.TryInvisibility(),Is.True);m.Abilities.Tick(.1f,true);Assert.That(m.Abilities.Revealed,Is.True);
            Assert.That(m.Streamer.View.GetComponent<DetectionOutline>().Visible,Is.True);
            m.Abilities.Tick(11,false);Assert.That(m.Abilities.TryInvisibility(),Is.True);Assert.That(m.Abilities.InvisibleCharges,Is.EqualTo(charges));
            var place=m.World.Items.First(i=>i.IsHidingPlace);m.Reporter.Hide(place);m.Abilities.Tick(.1f,true);Assert.That(m.Abilities.Revealed,Is.False);
            Assert.That(m.Finished,Is.False);
        }
        [UnityTest] public IEnumerator UploadResultIsAuthoritativeAndDuplicateSnapshotDoesNotReplayCue()
        {
            var host=Start(TestRole.Streamer,false);host.Reporter.SetPhone(true);
            typeof(MatchSession).GetProperty("PendingPhoto").SetValue(host,new PhotoEvidence{id=1,taskId=host.Quests.First().Id,objectCorrect=false});
            host.UploadPhoto(1);Assert.That(host.Score,Is.EqualTo(2));Assert.That(host.UploadFeedbackId,Is.EqualTo(1));
            Assert.That(host.GetComponentsInChildren<AudioSource>().Count(s=>s.name=="Audio_UploadFailure"),Is.Zero);
            guestRoot=new GameObject("Feedback guest");var guest=guestRoot.AddComponent<PrototypeApp>();guest.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Reporter,false);
            var snapshot=host.CaptureSnapshot();guest.Session.ApplySnapshot(snapshot);guest.Session.ApplySnapshot(snapshot);
            Assert.That(guest.Session.Score,Is.EqualTo(2));Assert.That(guest.Session.UploadFeedbackSuccess,Is.False);
            Assert.That(guest.Session.GetComponentsInChildren<AudioSource>().Count(s=>s.name=="Audio_UploadFailure"),Is.EqualTo(1));
            host.UploadPhoto(1);Assert.That(host.Score,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator MapContainsEveryFloorComputerRoomAndBlocksLocalInputWhileOpen()
        {
            var m=Start(TestRole.Reporter,false);yield return null;var map=m.HouseMap;
            Assert.That(map.Floors.Count,Is.EqualTo(m.World.Data.floors.Count));Assert.That(map.Rooms.Any(r=>r.computer && r.floor==m.Streamer.Computer.Floor),Is.True);
            Assert.That(map.GetComponentsInChildren<HouseMapBoard>(),Is.Empty);
            m.Reporter.SetPhone(true);map.Open(0);yield return null;
            Assert.That(map.IsOpen,Is.True);Assert.That(m.InputCaptured,Is.False);
            map.SelectFloor(100);Assert.That(map.Floor,Is.EqualTo(map.Floors.Count-1));
            map.Close();Assert.That(map.IsOpen,Is.False);Assert.That(m.ActivePhonePage,Is.EqualTo(PhonePage.Home));
            Assert.That(m.PhoneMenuOpen,Is.True,"Returning home keeps world input captured by the phone.");
            m.Reporter.SetPhone(false);Assert.That(m.PhoneMenuOpen,Is.False);
        }
        [UnityTest] public IEnumerator HostReadingSettingsDoesNotBlockRemoteReporterPhoto()
        {
            var m=Start(TestRole.Streamer);yield return null;
            app.OpenSettings();Assert.That(app.GameplayInputBlocked,Is.True);
            m.Reporter.SetPhone(true);m.SimulateNetworkActor(TestRole.Reporter,new PlayerCommand{action=true},.02f);
            Assert.That(m.PendingPhoto,Is.Not.Null);Assert.That(app.SettingsOpen,Is.True);
        }
        [UnityTest] public IEnumerator HallModelAndSkillStateAreReplacedWhenFormalMatchBegins()
        {
            var m=Start(TestRole.Streamer);yield return null;
            Assert.That(m.Streamer.Appearance.Model.name,Does.Contain("MysteryHallStreamer"));m.Abilities.TryInvisibility();m.Abilities.Tick(.2f,true);
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Streamer,true,false);yield return null;
            Assert.That(app.Session.Streamer.Appearance.Model.name,Does.Not.Contain("MysteryHallStreamer"));
            Assert.That(app.Session.Abilities.Invisible,Is.False);Assert.That(app.Session.Abilities.LaughEnergy,Is.EqualTo(5));Assert.That(app.Session.Abilities.InvisibleCharges,Is.EqualTo(2));
            Assert.That(app.Session.GetComponent<TrainingGuide>(),Is.Null);Assert.That(app.Session.Streamer.Controller.RoamRemaining,Is.EqualTo(45));
        }
    }
}
