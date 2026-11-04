using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class AiTutorialTests
    {
        GameObject root;
        PrototypeApp app;
        BuildingAsset authoredMap;
        MatchSession Start(TestRole role)
        {
            root=new GameObject("AiTutorialTest");app=root.AddComponent<PrototypeApp>();
            authoredMap=ScriptableObject.CreateInstance<BuildingAsset>();
            var data=BuildingData.CreateDefault();data.name="AI 正式测试地图";
            authoredMap.SetData(data);app.Building=authoredMap;
            app.Settings.flappyTargetScore=7;
            app.StartSingle(role);
            Assert.That(app.Session,Is.Not.Null,app.StartupError);
            return app.Session;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if(root) Object.Destroy(root);if(authoredMap) Object.Destroy(authoredMap);yield return null; }

        [UnityTest] public IEnumerator ReporterEntersPracticeHallBeforeAiMatch() => CheckHall(TestRole.Reporter);
        [UnityTest] public IEnumerator StreamerEntersPracticeHallBeforeAiMatch() => CheckHall(TestRole.Streamer);
        IEnumerator CheckHall(TestRole role)
        {
            var match=Start(role);yield return null;
            Assert.That(match.IsTraining,Is.True);Assert.That(match.IsNetwork,Is.False);
            Assert.That(match.GetComponent<TrainingHall>().SignCount,Is.GreaterThanOrEqualTo(8));
            Assert.That(match.World.Data.ToJson(),Is.EqualTo(app.TrainingBuilding.CreateRuntimeCopy().ToJson()));
            Assert.That(match.Quests,Is.Empty);Assert.That(match.ReporterAI,Is.Null);
            Assert.That(match.Streamer.Controller.Driver,Is.Null);
            Assert.That(match.Streamer.Controller.Level.TargetScore,Is.Zero);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Roaming));
            Assert.That(RenderSettings.ambientIntensity,Is.EqualTo(1));
            Assert.That(match.GetComponentsInChildren<AudioListener>().Count(l=>l.enabled),Is.EqualTo(1));
            match.Streamer.Controller.BeginStreaming();match.Finish("大厅不能结束");
            Assert.That(match.Finished,Is.False);Assert.That(match.Streamer.Controller.Level.Running,Is.False);
            Assert.That(app.CanStartSingleMatch,Is.False);app.StartSingleMatch();
            Assert.That(app.Session,Is.SameAs(match),"Unready players must remain in the hall.");
            app.SetSingleReady(true);Assert.That(app.CanStartSingleMatch,Is.True);
            yield return null;Assert.That(app.Session,Is.SameAs(match),"Ready must not auto-start.");
            app.SetSingleReady(false);app.StartSingleMatch();
            Assert.That(app.Session,Is.SameAs(match));
        }

        [UnityTest] public IEnumerator ReporterReadyStartsFreshFormalMatch() => CheckTransition(TestRole.Reporter);
        [UnityTest] public IEnumerator StreamerReadyStartsFreshFormalMatch() => CheckTransition(TestRole.Streamer);
        IEnumerator CheckTransition(TestRole role)
        {
            var hall=Start(role);yield return null;
            string original=authoredMap.Data.ToJson();
            hall.World.Items.First(i=>i.Data.type==ItemType.Door).SetDoorOpen(true);
            var place=hall.World.Items.First(i=>i.IsHidingPlace);
            hall.Reporter.Teleport(place.Front);hall.Reporter.SetPhone(true);hall.Reporter.Hide(place);
            hall.DebugOpen=true;
            app.SetSingleReady(true);app.StartSingleMatch();
            var match=app.Session;Assert.That(match,Is.Not.Null,app.StartupError);yield return null;
            Assert.That(match.IsTraining,Is.False);Assert.That(match.IsNetwork,Is.False);
            Assert.That(match.Role,Is.EqualTo(role));Assert.That(match.World.Data.name,Is.EqualTo("AI 正式测试地图"));
            Assert.That(match.GetComponent<TrainingHall>(),Is.Null);Assert.That(match.GetComponent<TutorialSigns>(),Is.Null);
            Assert.That(app.SingleReady,Is.False);Assert.That(match.DebugOpen,Is.False);
            Assert.That(match.Reporter.Hidden,Is.False);Assert.That(match.Reporter.PhoneOpen,Is.False);
            Assert.That(match.World.Items.Where(i=>i.Data.type==ItemType.Door).All(i=>!i.IsOpen),Is.True);
            Assert.That(match.Quests.All(q=>!q.Complete),Is.True);Assert.That(match.Quests,Is.Not.Empty);
            Assert.That(match.Score,Is.EqualTo(app.Settings.maxScore));Assert.That(match.StreamerPhotos,Is.Zero);
            Assert.That(match.Streamer.Controller.Fans,Is.EqualTo(app.Settings.initialFans));
            Assert.That(match.Streamer.Controller.Level.Score,Is.Zero);Assert.That(match.Finished,Is.False);
            Assert.That(RenderSettings.ambientIntensity,Is.Zero);Assert.That(match.Reporter.Vision.enabled,Is.True);
            if(role==TestRole.Streamer)
            {
                Assert.That(match.ReporterAI.enabled,Is.True);
                Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming));
                Assert.That(match.Streamer.Controller.Level.TargetScore,Is.EqualTo(7));
            }
            else Assert.That(match.Streamer.Controller.Driver,Is.Not.Null);
            Assert.That(authoredMap.Data.ToJson(),Is.EqualTo(original));
            app.StartSingleMatch();Assert.That(app.Session,Is.SameAs(match));
        }

        [UnityTest] public IEnumerator ReplayAndRoleChangeResetHallReadiness()
        {
            Start(TestRole.Streamer);app.SetSingleReady(true);app.StartSingleMatch();yield return null;
            app.Session.Finish("测试结束");app.RestartGame();yield return null;
            Assert.That(app.Session.IsTraining,Is.True);Assert.That(app.Session.Role,Is.EqualTo(TestRole.Streamer));
            Assert.That(app.SingleReady,Is.False);Assert.That(app.Session.Finished,Is.False);
            app.SetSingleReady(true);app.ReturnToMenu();yield return null;
            Assert.That(app.Session,Is.Null);Assert.That(app.SingleReady,Is.False);
            app.StartSingle(TestRole.Reporter);yield return null;
            Assert.That(app.Session.IsTraining,Is.True);Assert.That(app.Session.Role,Is.EqualTo(TestRole.Reporter));
            Assert.That(app.CanStartSingleMatch,Is.False);
        }

        [UnityTest] public IEnumerator HuntDeadlineDrainsAudienceAndResumingStopsLoss()
        {
            Start(TestRole.Streamer);app.SetSingleReady(true);app.StartSingleMatch();
            var match=app.Session;var brain=match.Streamer.Controller;
            var completed=brain.Level.Capture();completed.score=completed.target;completed.running=false;
            brain.Level.Apply(completed);brain.AdvanceMinigame(0);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Roaming));
            Assert.That(brain.RoamRemaining,Is.EqualTo(30));
            int fans=brain.Fans;
            brain.AdvanceReturnDeadline(15);Assert.That(brain.Fans,Is.EqualTo(fans),"Hunting must not drain viewers.");
            brain.BeginReturn();brain.AdvanceReturnDeadline(3);
            Assert.That(brain.Fans,Is.EqualTo(fans),"The announcement precedes the drain.");
            brain.AdvanceReturnDeadline(15);Assert.That(brain.Fans,Is.EqualTo(Mathf.CeilToInt(fans*.5f)));
            int retained=brain.Fans;brain.BeginStreaming();brain.AdvanceReturnDeadline(30);
            Assert.That(brain.Fans,Is.EqualTo(retained));Assert.That(match.Finished,Is.False);
            completed=brain.Level.Capture();completed.score=completed.target;completed.running=false;
            brain.Level.Apply(completed);brain.AdvanceMinigame(0);brain.BeginReturn();
            brain.AdvanceReturnDeadline(32.9f);Assert.That(brain.Fans,Is.GreaterThan(0));
            brain.AdvanceReturnDeadline(.11f);Assert.That(brain.Fans,Is.Zero);Assert.That(match.Finished,Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator SingleReadyCannotStartANetworkHall()
        {
            Start(TestRole.Reporter);app.SetSingleReady(true);
            app.StartNetworkSession(TrainingHallLayout.Create(),new MatchSettings(),TestRole.Streamer,true,true);
            var hall=app.Session;yield return null;
            Assert.That(app.SingleReady,Is.False);app.SetSingleReady(true);app.StartSingleMatch();
            Assert.That(app.CanStartSingleMatch,Is.False);Assert.That(app.Session,Is.SameAs(hall));
            Assert.That(hall.GetComponent<TrainingHall>().SignCount,Is.GreaterThanOrEqualTo(8));
        }
    }
}
