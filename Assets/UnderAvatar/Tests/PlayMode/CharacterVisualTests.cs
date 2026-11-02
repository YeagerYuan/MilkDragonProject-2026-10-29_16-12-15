using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class CharacterVisualTests
    {
        GameObject root;
        MatchSession Setup(bool network=false,bool authority=false)
        {
            root=new GameObject("CharacterVisualTest");var app=root.AddComponent<PrototypeApp>();
            if(network) app.StartNetworkSession(BuildingData.CreateDefault(),app.Settings,TestRole.Streamer,authority);
            else { app.Role=TestRole.Streamer;app.EnterPlay(); }
            var match=app.Session;Assert.That(match,Is.Not.Null,app.StartupError);
            match.Reporter.enabled=false;if(match.ReporterAI) match.ReporterAI.enabled=false;match.Streamer.Controller.enabled=false;
            return match;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if(root) Object.Destroy(root);yield return null; }
        [UnityTest] public IEnumerator ProvidedPrefabsHaveSkinAnimationAndCorrectVisibility()
        {
            var m=Setup();var r=m.Reporter;var a=r.Appearance;yield return null;
            Assert.That(a.Model.name,Does.Contain("Reporter_DouJie"));Assert.That(m.Streamer.Appearance.Model.name,Does.Contain("Streamer_HorrorMilkDragon"));
            var skin=a.GetComponentInChildren<SkinnedMeshRenderer>();Assert.That(skin,Is.Not.Null);Assert.That(skin.bones.Length,Is.GreaterThan(20));
            Assert.That(a.Animator.applyRootMotion,Is.False);
            Assert.That(m.Streamer.View.cullingMask & (1<<skin.gameObject.layer),Is.Not.Zero);
            Assert.That(r.View.cullingMask & (1<<skin.gameObject.layer),Is.Zero);
            r.Teleport(new Vector3(-4,0,-4));r.transform.rotation=Quaternion.identity;
            var leg=skin.bones.First(b=>b.name=="mixamorig:LeftUpLeg");var original=leg.localRotation;float maxAngle=0;
            for(int i=0;i<35;i++) { r.MoveInput(Vector2.up,false,1f/60);yield return null;maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(original,leg.localRotation)); }
            Assert.That(a.Animator.GetFloat("Speed"),Is.GreaterThan(1));Assert.That(maxAngle,Is.GreaterThan(8),"Leg bones must actually move, not just Animator parameters.");
            r.SetCrouched(true);
            for(int i=0;i<20;i++) {r.MoveInput(Vector2.zero,true,1f/60);yield return null;}
            Assert.That(a.Animator.GetBool("Crouched"),Is.True);Assert.That(a.Animator.GetCurrentAnimatorStateInfo(0).IsName("Crouching"),Is.True);
            Assert.That(r.GetComponent<CharacterController>().height,Is.EqualTo(1));
            a.SetVisible(false);Assert.That(a.GetComponentsInChildren<Renderer>().All(x=>!x.enabled),Is.True);
        }
        [UnityTest] public IEnumerator RemoteModelDoesNotInheritSimulationSnapsOrMoveCamera()
        {
            var m=Setup(true,false);var r=m.Reporter;var a=r.Appearance;
            r.ApplyPose(new ActorSnapshot{position=new Vector3(-4,0,-4)},10);yield return null;
            Vector3 visual=a.transform.position;r.transform.position+=Vector3.right*1.5f;
            Vector3 camera=r.View.transform.position;yield return null;
            Assert.That(Vector3.Distance(a.transform.position,visual),Is.LessThan(.02f));
            Assert.That(r.View.transform.position,Is.EqualTo(camera),"Visual interpolation must not drive first-person control/camera.");
            r.ApplyPose(new ActorSnapshot{position=new Vector3(8,0,8),hidden=true},11);yield return null;
            Assert.That(a.GetComponentsInChildren<Renderer>().All(x=>!x.enabled),Is.True);
            Assert.That(Vector3.Distance(a.transform.position,r.transform.position),Is.LessThan(.02f));
        }
        [Test] public void ReplayedPredictionDoesNotBecomeAnimationVelocity()
        {
            var motion=new CharacterMotion();motion.Record(Vector3.right*.04f,.02f);motion.Record(Vector3.forward*3,.02f,true);
            Assert.That(motion.Velocity,Is.EqualTo(Vector3.right*2));
        }
    }
}
