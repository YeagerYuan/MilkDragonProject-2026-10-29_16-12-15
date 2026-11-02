using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class TrainingHallTests
    {
        GameObject root;
        PrototypeApp app;
        MatchSession Start()
        {
            root=new GameObject("Training hall tests");app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(TrainingHallLayout.Create(),new MatchSettings(),TestRole.Streamer,true,true);
            Assert.That(app.Session,Is.Not.Null,app.StartupError);return app.Session;
        }
        [UnityTearDown] public IEnumerator Cleanup() {if(root) Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator HallContainsReachableInteractiveStationsAndNormalLighting()
        {
            var m=Start();yield return null;
            var nav=new BuildingNavigation(m.World.Data);
            foreach(var item in m.World.Items.Where(i=>i.Data.type!=ItemType.Wall && i.Data.type!=ItemType.Stairs))
                Assert.That(nav.FindPath(m.Reporter.transform.position,item.Front),Is.Not.Null,item.Data.type+" is unreachable");
            foreach(var type in new[]{ItemType.Door,ItemType.Bed,ItemType.Furniture,ItemType.StreamerPC,ItemType.QuestItem,ItemType.Stairs})
                Assert.That(m.World.Items.Any(i=>i.Data.type==type),Is.True,type.ToString());
            var stair=m.World.Stairs.Single();Assert.That(nav.FindPath(m.Reporter.transform.position,nav.Position(stair.Top)),Is.Not.Null);
            Assert.That(m.GetComponent<TrainingHall>().SignCount,Is.GreaterThanOrEqualTo(8));
            Assert.That(RenderSettings.ambientIntensity,Is.EqualTo(1));Assert.That(m.Reporter.Vision.Lamp.enabled,Is.False);
            Assert.That(m.Streamer.GetComponent<CharacterController>().enabled,Is.True);Assert.That(m.Reporter.GetComponent<CharacterController>().enabled,Is.True);
        }
        [UnityTest] public IEnumerator HallKeepsInteractionButNeverRunsCompetitionOrMinigame()
        {
            var m=Start();yield return null;var brain=m.Streamer.Controller;
            Assert.That(m.Quests,Is.Empty);Assert.That(m.ReporterAI,Is.Null);Assert.That(brain.Driver,Is.Null);
            float roam=brain.RoamRemaining;int fans=brain.Fans,score=m.Score;
            for(int i=0;i<100;i++) brain.TickNetwork(new PlayerCommand(),.02f);
            brain.BeginStreaming();brain.Flap();brain.AdvanceMinigame(100);brain.BeginReturn();brain.AdvanceReturnDeadline(1000);
            Assert.That(brain.RoamRemaining,Is.EqualTo(roam));Assert.That(brain.Fans,Is.EqualTo(fans));
            Assert.That(m.Streamer.State,Is.EqualTo(StreamerState.Roaming));Assert.That(brain.Level.TargetScore,Is.Zero);Assert.That(brain.Level.Running,Is.False);
            m.Reporter.Teleport(m.Streamer.transform.position);Assert.That(brain.TryCapture(),Is.False);
            m.Reporter.SetPhone(true);m.TakePhoto();Assert.That(m.Score,Is.EqualTo(score));Assert.That(m.StreamerPhotos,Is.Zero);
            m.Finish("Should not end the hall");Assert.That(m.Finished,Is.False);
            foreach(var place in m.World.Items.Where(i=>i.IsHidingPlace))
            {
                m.Reporter.Teleport(place.Front);m.Reporter.Hide(place);Assert.That(m.Reporter.Hidden,Is.True);
                var body=m.Streamer.GetComponent<CharacterController>();body.enabled=false;m.Streamer.transform.position=place.Front;body.enabled=true;Physics.SyncTransforms();
                brain.SearchHidingPlace(place);Assert.That(place.IsOpen,Is.True);Assert.That(m.Finished,Is.False);Assert.That(brain.RoamRemaining,Is.EqualTo(roam));
                m.Reporter.Interact();Assert.That(m.Reporter.Hidden,Is.False);
            }
        }
        [UnityTest] public IEnumerator FormalMatchResetsHallStateAndRestoresDarkness()
        {
            var m=Start();yield return null;
            var door=m.World.Items.First(i=>i.Data.type==ItemType.Door);door.SetDoorOpen(true);
            var place=m.World.Items.First(i=>i.IsHidingPlace);m.Reporter.Teleport(place.Front);m.Reporter.Hide(place);
            var source=BuildingData.CreateDefault();string before=source.ToJson();
            app.StartNetworkSession(source,new MatchSettings(),TestRole.Streamer,true);
            var game=app.Session;yield return null;
            Assert.That(game.IsTraining,Is.False);Assert.That(game.Reporter.Hidden,Is.False);Assert.That(game.Reporter.PhoneOpen,Is.False);
            Assert.That(game.Streamer.State,Is.EqualTo(StreamerState.Streaming));Assert.That(game.Streamer.Controller.Level.Score,Is.Zero);
            Assert.That(game.Streamer.Controller.Level.TargetScore,Is.EqualTo(15));Assert.That(game.Score,Is.EqualTo(3));Assert.That(game.StreamerPhotos,Is.Zero);
            Assert.That(game.World.Items.Where(i=>i.Data.type==ItemType.Door).All(i=>!i.IsOpen),Is.True);
            Assert.That(RenderSettings.ambientIntensity,Is.Zero);Assert.That(game.Reporter.Vision.enabled,Is.True);
            Assert.That(source.ToJson(),Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator DisablingTheWholeAppRestoresSceneLighting()
        {
            var mode=RenderSettings.ambientMode;var ambient=RenderSettings.ambientLight;
            float intensity=RenderSettings.ambientIntensity,reflection=RenderSettings.reflectionIntensity;
            Start();yield return null;
            root.SetActive(false);yield return null;
            Assert.That(RenderSettings.ambientMode,Is.EqualTo(mode));Assert.That(RenderSettings.ambientLight,Is.EqualTo(ambient));
            Assert.That(RenderSettings.ambientIntensity,Is.EqualTo(intensity));Assert.That(RenderSettings.reflectionIntensity,Is.EqualTo(reflection));
        }
    }
}
