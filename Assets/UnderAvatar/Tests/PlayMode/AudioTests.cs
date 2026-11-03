using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class AudioTests
    {
        GameObject root;
        PrototypeApp app;
        MatchSession match;
        PrototypeAudio audio;
        void Start(TestRole role,bool authority=true)
        {
            root=new GameObject("Audio verification");app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),role,authority);
            match=app.Session;match.enabled=false;match.Reporter.enabled=false;match.Streamer.Controller.enabled=false;
            typeof(MatchSession).GetField("<InputCaptured>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(match,true);
            match.Reporter.Teleport(new Vector3(-4,0,-4));match.Reporter.transform.rotation=Quaternion.identity;
            audio=match.GetComponent<PrototypeAudio>();
            Assert.That(audio.Profile,Is.Not.Null,"The shipped audio profile must be present in Resources.");
        }
        AudioSource Breath => audio.GetComponentsInChildren<AudioSource>().SingleOrDefault(s=>s.loop);
        [UnityTearDown] public IEnumerator Cleanup() { if(root) Object.Destroy(root);yield return null; }

        [UnityTest] public IEnumerator RealClipsPlaySpatiallyAndImportedAssetsSurviveMatchExit()
        {
            Start(TestRole.Reporter);
            var profile=audio.Profile;
            Assert.That(profile.walkSteps.Length,Is.GreaterThanOrEqualTo(4));
            Assert.That(profile.chaseSteps.Length,Is.GreaterThanOrEqualTo(4));
            var kinds=new[]{NoiseKind.Footstep,NoiseKind.ChaseFootstep,NoiseKind.Door,NoiseKind.DoorClose,NoiseKind.Shutter};
            foreach(var kind in kinds) audio.Play(match.Reporter.transform.position,kind);
            var sources=audio.GetComponentsInChildren<AudioSource>().Where(s=>!s.loop).ToArray();
            Assert.That(sources.Length,Is.EqualTo(5));
            foreach(var source in sources)
            {
                Assert.That(source.clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded));
                Assert.That(source.spatialBlend,Is.EqualTo(1));
                Assert.That(source.isPlaying,Is.True);
                var samples=new float[source.clip.samples*source.clip.channels];Assert.That(source.clip.GetData(samples,0),Is.True);
                Assert.That(samples.Any(x=>Mathf.Abs(x)>.02f),Is.True,"Imported clip is silent.");
            }
            var first=sources[0].clip;audio.Play(Vector3.zero,NoiseKind.Footstep);
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Last().clip,Is.Not.EqualTo(first),"Avoid adjacent identical footsteps.");
            Object.Destroy(root);yield return null;
            Assert.That(profile.breathingLoop,Is.Not.Null);Assert.That(first,Is.Not.Null,"Do not destroy shared imported assets on exit.");
        }

        [UnityTest] public IEnumerator QuietMovementBreathesLocallyWithoutPulsesOrNetworkEventsAndStopsAtRest()
        {
            Start(TestRole.Reporter);yield return null;
            int heard=0;match.NoiseEmitted+=_=>heard++;
            for(int i=0;i<15;i++) { match.Reporter.MoveInput(Vector2.up,true,1f/60);yield return null; }
            Assert.That(Breath.isPlaying,Is.True);Assert.That(Breath.spatialBlend,Is.Zero);
            Assert.That(Breath.volume,Is.EqualTo(audio.Profile.breathingVolume*audio.Profile.quietBreathingMultiplier).Within(.01f));
            Assert.That(heard,Is.Zero);Assert.That(match.CaptureSnapshot().noises,Is.Empty);
            match.Reporter.SetCrouched(true);
            for(int i=0;i<10;i++) { match.Reporter.MoveInput(Vector2.up,false,1f/60);yield return null; }
            Assert.That(Breath.isPlaying,Is.True);Assert.That(match.CaptureSnapshot().noises,Is.Empty);
            match.Reporter.MoveInput(Vector2.zero,false,1f/60);yield return new WaitForSeconds(.3f);
            Assert.That(Breath.isPlaying,Is.False);
            match.Reporter.SetCrouched(false);
            for(int i=0;i<12;i++) { match.Reporter.MoveInput(Vector2.up,false,1f/60);yield return null; }
            Assert.That(Breath.volume,Is.EqualTo(audio.Profile.breathingVolume).Within(.01f));
            Assert.That(heard,Is.GreaterThan(0));
            match.Finish("test");yield return new WaitForSeconds(.3f);Assert.That(Breath.isPlaying,Is.False);
        }

        [UnityTest] public IEnumerator StreamerHostNeverCreatesBreathingForRemoteReporter()
        {
            Start(TestRole.Streamer);
            for(int i=0;i<12;i++) { match.Reporter.ApplyCommand(new PlayerCommand{y=1,sneak=true},1f/60);yield return null; }
            Assert.That(Breath,Is.Null);Assert.That(match.CaptureSnapshot().noises,Is.Empty);
            match.PlayLocalFootstep(Vector3.zero);
            var noises=match.CaptureSnapshot().noises;
            Assert.That(noises.Length,Is.EqualTo(1));Assert.That(noises[0].kind,Is.EqualTo(NoiseKind.ChaseFootstep));
            Assert.That(noises[0].audibleOnly,Is.True);Assert.That(match.Pulses,Is.Empty);
        }

        [UnityTest] public IEnumerator ClientBreathDoesNotWaitForHostAndReplayDoesNotRestartIt()
        {
            Start(TestRole.Reporter,false);yield return null;
            for(int i=0;i<12;i++) { match.Reporter.PredictCommand(new PlayerCommand{y=1,sneak=true},1f/60,false);yield return null; }
            Assert.That(Breath.isPlaying,Is.True,"Reporter client hears their own movement before host acknowledgement.");
            Assert.That(match.Pulses,Is.Empty);Assert.That(match.CaptureSnapshot().noises,Is.Empty);
            match.Reporter.PredictCommand(new PlayerCommand(),1f/60,false);
            yield return new WaitForSeconds(.3f);Assert.That(Breath.isPlaying,Is.False);
            for(int i=0;i<12;i++) { match.Reporter.PredictCommand(new PlayerCommand{y=1,sneak=true},1f/60,true);yield return null; }
            Assert.That(Breath.isPlaying,Is.False,"Replayed historical movement must not generate fresh local breathing.");
        }
    }
}
