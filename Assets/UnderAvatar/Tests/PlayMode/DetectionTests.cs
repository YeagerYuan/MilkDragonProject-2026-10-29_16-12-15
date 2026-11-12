using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class DetectionTests
    {
        GameObject root,guestRoot;
        PrototypeApp app;
        MatchSession match;
        PlayerAbilities ability;
        void Start(bool network=false)
        {
            root=new GameObject("Detection test");app=root.AddComponent<PrototypeApp>();app.Role=TestRole.Streamer;
            if(network) app.StartNetworkSession(BuildingData.CreateDefault(),app.Settings,TestRole.Streamer,true);else app.EnterPlay();
            match=app.Session;Assert.That(match,Is.Not.Null,app.StartupError);ability=match.Abilities;ability.enabled=false;
            match.Streamer.Controller.enabled=false;if(match.ReporterAI) match.ReporterAI.enabled=false;
            match.Reporter.enabled=false;
            match.Streamer.transform.SetPositionAndRotation(new Vector3(100,0,100),Quaternion.identity);
            match.Reporter.Teleport(new Vector3(100,0,106));Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {if(root) Object.Destroy(root);if(guestRoot) Object.Destroy(guestRoot);Time.timeScale=1;yield return null;}

        [UnityTest] public IEnumerator LaughterWorksWhileBroadcastingAndRechargeRequiresRelease()
        {
            Start();Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming));
            ability.Tick(1,true);Assert.That(ability.Laughing,Is.True);Assert.That(ability.LaughEnergy,Is.EqualTo(4).Within(.001));
            ability.Tick(2,false);Assert.That(ability.LaughEnergy,Is.EqualTo(5).Within(.001));
            ability.Tick(5,true);Assert.That(ability.Laughing,Is.False);Assert.That(ability.LaughEnergy,Is.Zero);
            ability.Tick(3,true);Assert.That(ability.LaughEnergy,Is.Zero,"Holding F at exhaustion must not alternate short laughs/recharges.");
            ability.Tick(1,false);Assert.That(ability.LaughEnergy,Is.EqualTo(.5f).Within(.001));
            ability.Tick(.1f,true);Assert.That(ability.Laughing,Is.True);Assert.That(ability.LaughEnergy,Is.EqualTo(.4f).Within(.001));
            yield return null;
        }
        [UnityTest] public IEnumerator ConeUsesConfiguredRangeIgnoresWallsAndHidingImmediatelySuppressesReveal()
        {
            Start();Assert.That(ability.DetectionDistance,Is.EqualTo(9.75f).Within(.001));
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);
            wall.layer=PrototypeLayers.Get("UA_Wall");wall.transform.position=new Vector3(100,1.5f,103);wall.transform.localScale=new Vector3(8,4,.2f);Physics.SyncTransforms();
            ability.Tick(.1f,true);Assert.That(ability.Revealed,Is.True);
            match.Reporter.Teleport(new Vector3(108,0,100));ability.Tick(4,false);ability.Tick(.1f,true);Assert.That(ability.Revealed,Is.False);
            match.Reporter.Teleport(new Vector3(100,0,111));ability.Tick(.1f,true);Assert.That(ability.Revealed,Is.False);
            match.Settings.detection.rangeMultiplier=2;ability.Tick(.1f,true);Assert.That(ability.Revealed,Is.True);
            var hiding=match.World.Items.First(i=>i.IsHidingPlace);match.Reporter.Hide(hiding);
            Assert.That(ability.Revealed,Is.False);ability.Tick(.1f,true);Assert.That(ability.OutlineRemaining,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator InvisibleBodyStaysHiddenWhenDetectedButCanStillBeCaughtAtCloseRange()
        {
            Start();Assert.That(ability.TryInvisibility(),Is.True);Assert.That(ability.InvisibleCharges,Is.EqualTo(1));
            Assert.That(ability.TryInvisibility(),Is.False);Assert.That(ability.InvisibleCharges,Is.EqualTo(1));
            Assert.That(match.Streamer.Controller.CanSeeReporter(),Is.False);yield return null;
            Assert.That(match.Reporter.Appearance.Model.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),Is.True);
            Assert.That(match.Reporter.Vision.Lamp.enabled,Is.False);
            ability.Tick(.1f,true);Assert.That(ability.Revealed,Is.True);yield return null;
            Assert.That(match.Reporter.Appearance.Model.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),Is.True);
            Assert.That(match.Streamer.View.GetComponent<DetectionOutline>().Visible,Is.True);
            typeof(StreamerActor).GetProperty("State").SetValue(match.Streamer,StreamerState.Roaming);match.Reporter.Teleport(new Vector3(100,0,100.8f));Physics.SyncTransforms();
            Assert.That(match.Streamer.Controller.TryCapture(),Is.True);Assert.That(match.ReporterLives,Is.EqualTo(2));Assert.That(match.ReporterDying,Is.True);
        }
        [UnityTest] public IEnumerator TaskRewardIsGrantedOnceAndExpirationRestoresVisibility()
        {
            Start();match.Reporter.SetPhone(true);
            match.World.Data.tasks=InvestigationData.Tasks(match.World.Data);match.World.Data.useTaskEditor=true;
            match.World.Data.tasks[0].reward=TaskReward.Invisibility;
            var quest=match.Quests.First();
            var photo=new PhotoEvidence{id=1,taskId=quest.Id,objectCorrect=true,regionCorrect=true};
            typeof(MatchSession).GetProperty("PendingPhoto").SetValue(match,photo);
            match.UploadPhoto(1);Assert.That(quest.Complete,Is.True);Assert.That(ability.InvisibleCharges,Is.EqualTo(3));
            match.UploadPhoto(1);Assert.That(ability.InvisibleCharges,Is.EqualTo(3));
            ability.TryInvisibility();ability.Tick(10.1f,false);yield return null;
            Assert.That(ability.Invisible,Is.False);Assert.That(match.Reporter.Appearance.Model.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),Is.True);
        }
        [UnityTest] public IEnumerator AuthorityOwnsAbilityRequestsAndSnapshotsReplicateToReporterClient()
        {
            Start(true);typeof(StreamerActor).GetProperty("State").SetValue(match.Streamer,StreamerState.Roaming);
            match.SimulateNetworkActor(TestRole.Reporter,new PlayerCommand{invisible=true},.01f);
            Assert.That(ability.Invisible,Is.True);
            ability.Tick(.1f,true);
            guestRoot=new GameObject("Detection guest");var guest=guestRoot.AddComponent<PrototypeApp>();
            guest.StartNetworkSession(BuildingData.CreateDefault(),app.Settings,TestRole.Reporter,false);
            var client=guest.Session;client.Abilities.enabled=false;client.ApplySnapshot(match.CaptureSnapshot());
            Assert.That(client.Abilities.Invisible,Is.True);Assert.That(client.Abilities.InvisibleCharges,Is.EqualTo(1));
            Assert.That(client.Abilities.Revealed,Is.True);Assert.That(client.Abilities.LaughEnergy,Is.EqualTo(ability.LaughEnergy));
            Assert.That(client.Abilities.TryInvisibility(),Is.False);
            Assert.That(client.Streamer.View.GetComponent<DetectionOutline>(),Is.Null,"Reporter must not see the streamer's detection overlay.");
            yield return null;
        }
        [UnityTest] public IEnumerator TaskPhotoRangeIsCloserThanLivePhotoRangeAndClampedByVision()
        {
            Start();var item=match.World.Items.First(i=>i.Data.type==ItemType.QuestItem);
            item.transform.position=new Vector3(100,0,105);Physics.SyncTransforms();
            match.Reporter.Teleport(new Vector3(100,0,100));match.Reporter.SetLookAt(item.PhotoPoint);Physics.SyncTransforms();
            Assert.That(match.TargetInPhoto(item.Data.id),Is.False);
            match.Reporter.Teleport(new Vector3(100,0,103));match.Reporter.SetLookAt(item.PhotoPoint);Physics.SyncTransforms();
            Assert.That(match.TargetInPhoto(item.Data.id),Is.True);
            match.Settings.reporterVision.range=1;Assert.That(match.TaskPhotoDistance,Is.EqualTo(1));Assert.That(match.TargetInPhoto(item.Data.id),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator BgmContinuesThroughComicAndHallAndStopsInFormalMatch()
        {
            root=new GameObject("Menu audio test");app=root.AddComponent<PrototypeApp>();yield return null;
            Assert.That(app.MenuSound.MusicPlaying,Is.True);
            app.StartSingle(TestRole.Reporter);yield return null;Assert.That(app.MenuSound.MusicPlaying,Is.True);
            app.Comic.Advance();app.Comic.Finish();yield return null;
            Assert.That(app.Session.IsTraining,Is.True);Assert.That(app.MenuSound.MusicPlaying,Is.True);
            Assert.That(app.Session.Abilities.TryInvisibility(),Is.True,"Teaching hall now allows repeatable skill practice.");
            app.SetSingleReady(true);app.StartSingleMatch();yield return null;
            Assert.That(app.MenuSound.MusicPlaying,Is.False);
            app.ReturnToMenu();yield return null;Assert.That(app.MenuSound.MusicPlaying,Is.True);
        }
    }
}
