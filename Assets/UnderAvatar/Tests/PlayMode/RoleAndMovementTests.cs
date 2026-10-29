using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class RoleAndMovementTests
    {
        GameObject root;
        PrototypeApp app;
        MatchSession match;
        void StartMatch(TestRole role)
        {
            root=new GameObject("RoleTest"); app=root.AddComponent<PrototypeApp>();
            app.Role=role; app.EnterPlay(); match=app.Session;
            Assert.That(match,Is.Not.Null,app.StartupError);
            if(match.ReporterAI) match.ReporterAI.enabled=false;
            match.Reporter.enabled=false; match.Streamer.Controller.Driver=null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        { if(root) Object.Destroy(root); yield return null; }

        [UnityTest] public IEnumerator WalkMakesSoundButShiftAndCrouchAreSlowerAndSilent()
        {
            StartMatch(TestRole.Reporter); match.Streamer.Controller.enabled=false;
            var reporter=match.Reporter; var start=new Vector3(-4,0,-4);
            reporter.Teleport(start); reporter.transform.rotation=Quaternion.identity;
            yield return null;
            int sounds=0; match.NoiseEmitted+=pulse=>{ if(pulse.Kind==NoiseKind.Footstep) sounds++; };
            for(int i=0;i<30;i++) { reporter.MoveInput(Vector2.up,false,1f/60); yield return null; }
            float normalDistance=reporter.transform.position.z-start.z;
            Assert.That(sounds,Is.GreaterThan(0)); Assert.That(normalDistance,Is.GreaterThan(.8f));
            reporter.Teleport(start); sounds=0;
            for(int i=0;i<30;i++) { reporter.MoveInput(Vector2.up,true,1f/60); yield return null; }
            float quietDistance=reporter.transform.position.z-start.z;
            Assert.That(quietDistance,Is.EqualTo(normalDistance*.45f).Within(.1f)); Assert.That(sounds,Is.Zero);
            reporter.Teleport(start); Assert.That(reporter.SetCrouched(true),Is.True);
            Assert.That(reporter.View.transform.localPosition.y,Is.LessThan(1));
            Assert.That(reporter.GetComponent<CharacterController>().height,Is.EqualTo(1));
            for(int i=0;i<30;i++) { reporter.MoveInput(Vector2.up,false,1f/60); yield return null; }
            Assert.That(sounds,Is.Zero); Assert.That(reporter.transform.position.z-start.z,Is.EqualTo(quietDistance).Within(.1f));
        }

        [UnityTest] public IEnumerator CrouchingFitsUnderLowCeilingAndCannotStandThroughIt()
        {
            StartMatch(TestRole.Reporter); match.Streamer.Controller.enabled=false;
            var reporter=match.Reporter; reporter.Teleport(new Vector3(-4,0,-4)); reporter.SetCrouched(true);
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube); roof.transform.SetParent(root.transform);
            roof.layer=PrototypeLayers.Get("UA_Props");
            roof.transform.position=reporter.transform.position+Vector3.up*1.4f; roof.transform.localScale=new Vector3(2,.2f,2);
            Physics.SyncTransforms();
            Assert.That(reporter.SetCrouched(false),Is.False); Assert.That(reporter.Crouched,Is.True);
            Object.Destroy(roof); yield return null;
            Assert.That(reporter.SetCrouched(false),Is.True); Assert.That(reporter.Crouched,Is.False);
            Assert.That(reporter.View.transform.localPosition.y,Is.EqualTo(1.65f));
        }

        [UnityTest] public IEnumerator StreamerViewHasOneListenerAndCannotMoveLookOrLeaveBeforeFifteen()
        {
            StartMatch(TestRole.Streamer); yield return null;
            var brain=match.Streamer.Controller;
            Assert.That(match.Reporter.IsHuman,Is.False); Assert.That(match.Reporter.View.enabled,Is.False);
            Assert.That(match.Streamer.View.enabled,Is.True); Assert.That(match.Streamer.View.targetTexture,Is.Null);
            Assert.That(root.GetComponentsInChildren<AudioListener>().Count(l=>l.enabled),Is.EqualTo(1));
            Assert.That(brain.Level.TargetScore,Is.EqualTo(15)); Assert.That(brain.Driver,Is.Null);
            Vector3 before=brain.transform.position; Quaternion look=brain.transform.rotation;
            brain.MoveInput(Vector2.up,false,.2f); brain.LookInput(new Vector2(100,50)); brain.BeginReturn();
            Assert.That(brain.transform.position,Is.EqualTo(before)); Assert.That(brain.transform.rotation,Is.EqualTo(look));
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming));
            brain.Flap();
            for(int i=0;i<300 && !brain.Level.Crashed;i++) brain.AdvanceMinigame(1f/60);
            Assert.That(brain.Level.Crashed,Is.True); Assert.That(brain.Level.Score,Is.Zero);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming));
            GameplayTests.FinishFlappy(brain);
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Roaming));
            brain.MoveInput(Vector2.down,false,.1f);
            Assert.That(Vector3.Distance(brain.transform.position,before),Is.GreaterThan(.2f));
        }

        [UnityTest] public IEnumerator AiReporterUsesDoorsAndPhysicalStairsAndTakesOnlyOnePhotoPerBroadcast()
        {
            StartMatch(TestRole.Streamer); match.ReporterAI.enabled=true;
            app.Settings.reporterSpeed=6;
            int doors=0; match.NoiseEmitted+=pulse=>{if(pulse.Kind==NoiseKind.Door) doors++;};
            float deadline=Time.time+55,previousHeight=match.Reporter.transform.position.y;
            bool climbed=false;
            while(Time.time<deadline && (match.StreamerPhotos<1 || !match.Quests.Any(q=>q.Complete)))
            {
                yield return null;
                float height=match.Reporter.transform.position.y;
                Assert.That(Mathf.Abs(height-previousHeight),Is.LessThan(.65f),"AI reporter teleported floors");
                climbed|=height>.4f && height<app.World.Data.floorHeight-.4f; previousHeight=height;
            }
            Assert.That(match.StreamerPhotos,Is.EqualTo(1),"AI status: "+match.ReporterAI.Status+" position: "+match.Reporter.transform.position);
            Assert.That(match.Quests.Any(q=>q.Complete),Is.True,"AI failed to photograph an evidence item.");
            Assert.That(climbed,Is.True); Assert.That(doors,Is.GreaterThan(0));
            int score=match.Score;
            yield return new WaitForSeconds(5.5f);
            Assert.That(match.StreamerPhotos,Is.EqualTo(1)); Assert.That(match.Score,Is.EqualTo(score));
            Assert.That(match.Streamer.State,Is.EqualTo(StreamerState.Streaming),"The human minigame must not auto-complete.");
        }
    }
}
