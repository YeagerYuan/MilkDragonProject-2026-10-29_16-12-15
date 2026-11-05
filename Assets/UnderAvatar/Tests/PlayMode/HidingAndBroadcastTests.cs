using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class HidingAndBroadcastTests
    {
        GameObject root;
        PrototypeApp app;
        MatchSession match;
        void Start()
        {
            root=new GameObject("Hiding tests");app=root.AddComponent<PrototypeApp>();
            var map=BuildingData.CreateDefault();
            map.floors[0].items.Add(new PlacedItemData{id="test_bed",type=ItemType.Bed,gridX=-2,gridZ=-2});
            app.StartNetworkSession(map,new MatchSettings(),TestRole.Streamer,true);match=app.Session;
            match.enabled=false;match.Reporter.enabled=false;match.Streamer.Controller.enabled=false;
            GameplayTests.FinishFlappy(match.Streamer.Controller);
        }
        void Approach(PlacedItemView item)
        {
            var body=match.Streamer.GetComponent<CharacterController>();body.enabled=false;
            match.Streamer.transform.position=item.Front;body.enabled=true;Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Cleanup() { if(root) Object.Destroy(root);yield return null; }

        [UnityTest] public IEnumerator WardrobeRequiresExplicitSearchAndWrongSearchCostsOnlyOnce()
        {
            Start();yield return null;var brain=match.Streamer.Controller;
            var wardrobe=match.World.Items.First(i=>i.Floor==0 && i.Data.type==ItemType.Furniture && i.IsHidingPlace);
            Approach(wardrobe);float before=brain.RoamRemaining;
            Assert.That(brain.SearchHidingPlace(wardrobe),Is.False);Assert.That(brain.RoamRemaining,Is.EqualTo(before-5));
            Assert.That(match.Message,Is.EqualTo("未发现任何人，搜捕时间 -5 秒。"));
            Assert.That(match.MessageUntil,Is.GreaterThan(Time.time));
            wardrobe.SetSearchOpen(false);brain.SearchHidingPlace(wardrobe);Assert.That(brain.RoamRemaining,Is.EqualTo(before-5));
            Assert.That(match.Message,Does.Contain("不重复扣除"));
            wardrobe.SetSearchOpen(false);match.Reporter.Teleport(wardrobe.Front);match.Reporter.Hide(wardrobe);
            Assert.That(match.Reporter.HidingPlaceId,Is.EqualTo(wardrobe.Data.id));Assert.That(match.Pulses.Last().Kind,Is.EqualTo(NoiseKind.Door));
            var body=match.Streamer.GetComponent<CharacterController>();body.enabled=false;match.Streamer.transform.position=wardrobe.transform.position;body.enabled=true;
            Assert.That(brain.TryCapture(),Is.False,"Proximity must not capture hidden players.");
            Approach(wardrobe);match.Streamer.View.transform.rotation=Quaternion.LookRotation(wardrobe.transform.position+Vector3.up*1.2f-match.Streamer.View.transform.position);
            brain.Interact();
            Assert.That(match.Finished,Is.True);
        }
        [UnityTest] public IEnumerator BedIsLouderAndSearchUsesBedPenaltyWithoutCapturingDifferentHidingSpot()
        {
            Start();yield return null;var brain=match.Streamer.Controller;
            var bed=match.World.Find("test_bed");Approach(bed);float before=brain.RoamRemaining;
            brain.SearchHidingPlace(bed);Assert.That(brain.RoamRemaining,Is.EqualTo(before-5));
            Assert.That(match.Message,Is.EqualTo("未发现任何人，搜捕时间 -5 秒。"));bed.SetSearchOpen(false);
            match.Reporter.Teleport(bed.Front);match.Reporter.Hide(bed);
            Assert.That(match.Reporter.Hidden,Is.True);Assert.That(match.Pulses.Last().Kind,Is.EqualTo(NoiseKind.HideBed));
            Assert.That(brain.TryCapture(),Is.False);
            var other=match.World.Items.First(i=>i.Floor==0 && i.Data.type==ItemType.Furniture && i.IsHidingPlace);
            Approach(other);Assert.That(brain.SearchHidingPlace(other),Is.False);Assert.That(match.Finished,Is.False);
            Approach(bed);Assert.That(brain.SearchHidingPlace(bed),Is.True);Assert.That(match.Finished,Is.True);
        }
        [UnityTest] public IEnumerator SearchExhaustsRoamAndReturningFanDeadlineCannotBeResetByRepeatedReturn()
        {
            Start();yield return null;var brain=match.Streamer.Controller;
            app.RuntimeSettings.wrongBedPenalty=100;
            var bed=match.World.Find("test_bed");Approach(bed);brain.SearchHidingPlace(bed);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Returning));Assert.That(brain.RoamRemaining,Is.Zero);
            Assert.That(match.Message,Is.EqualTo("未发现任何人，搜捕时间 -100 秒。"),"The return transition must not replace the search feedback.");
            brain.AdvanceReturnDeadline(19.9f);Assert.That(brain.Fans,Is.EqualTo(5000));
            brain.BeginReturn();brain.AdvanceReturnDeadline(.2f);Assert.That(brain.Fans,Is.EqualTo(4000));
            brain.AdvanceReturnDeadline(40);Assert.That(brain.Fans,Is.Zero);Assert.That(match.Result,Does.StartWith("狗仔获胜"));
        }
        [UnityTest] public IEnumerator BroadcastingStopsFanLossAndRoundScoresUseSharedPhysics()
        {
            Start();yield return null;var brain=match.Streamer.Controller;
            app.RuntimeSettings.broadcastTargets.Add(new BroadcastTarget{broadcast=2,score=3});
            app.RuntimeSettings.flappyRules.gapSize=.4f;
            brain.BeginReturn();brain.AdvanceReturnDeadline(20);Assert.That(brain.Fans,Is.EqualTo(4000));
            brain.BeginStreaming();brain.AdvanceReturnDeadline(200);
            Assert.That(brain.Fans,Is.EqualTo(4000));Assert.That(brain.Level.TargetScore,Is.EqualTo(3));
            Assert.That(brain.Level.PipeGap,Is.EqualTo(.4f));GameplayTests.FinishFlappy(brain);brain.BeginStreaming();
            Assert.That(brain.Level.TargetScore,Is.EqualTo(15));Assert.That(brain.Level.PipeGap,Is.EqualTo(.4f));
            Assert.That(brain.Level.Rules.flapImpulse,Is.EqualTo(.56f));
        }
        [UnityTest] public IEnumerator HiddenIdentitySearchStateFansAndRulesReplicateToClient()
        {
            Start();yield return null;var map=match.World.Data.Copy();var settings=app.RuntimeSettings;
            var bed=match.World.Find("test_bed");match.Reporter.Teleport(bed.Front);match.Reporter.Hide(bed);
            var other=match.World.Items.First(i=>i.Floor==0 && i.Data.type==ItemType.Furniture && i.IsHidingPlace);
            Approach(other);match.Streamer.Controller.SearchHidingPlace(other);
            string otherId=other.Data.id;
            match.Streamer.Controller.BeginReturn();match.Streamer.Controller.AdvanceReturnDeadline(20);
            var snapshot=JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(match.CaptureSnapshot()));
            Object.Destroy(root);yield return null;
            root=new GameObject("Hiding client");app=root.AddComponent<PrototypeApp>();app.StartNetworkSession(map,settings,TestRole.Reporter,false);
            app.Session.ApplySnapshot(snapshot);
            Assert.That(app.Session.Reporter.HidingPlaceId,Is.EqualTo("test_bed"));Assert.That(app.Session.Reporter.HidingPlace.Data.type,Is.EqualTo(ItemType.Bed));
            Assert.That(app.Session.World.Find(otherId).IsOpen,Is.True);
            Assert.That(app.Session.Streamer.Controller.Fans,Is.EqualTo(4000));Assert.That(app.Session.Streamer.Controller.ReturnElapsed,Is.EqualTo(20));
        }
    }
}
