using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class DVRenderingTests
    {
        GameObject root;
        Material custom;
        PrototypeApp Start(TestRole role)
        {
            root=new GameObject("DVRoleRenderTest");
            var app=root.AddComponent<PrototypeApp>();app.StartSingle(role);if(app.ComicOpen) app.Comic.Finish();
            Assert.That(app.Session.IsTraining,Is.True);
            Assert.That(root.GetComponentsInChildren<DVCamcorderDisplay>(true),Is.Empty);
            app.SetSingleReady(true);app.StartSingleMatch();
            Assert.That(app.Session,Is.Not.Null,app.StartupError);
            app.Session.Streamer.Controller.enabled=false;
            if(app.Session.ReporterAI) app.Session.ReporterAI.enabled=false;
            return app;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(root) Object.Destroy(root);
            if(custom) Object.Destroy(custom);
            yield return null;
        }
        [UnityTest] public IEnumerator ReporterAndAiObserverUseIndependentPresetsAndLiveChanges()
        {
            var app=Start(TestRole.Reporter);yield return null;
            var reporter=app.Session.Reporter.View.GetComponent<DVCamcorderDisplay>();
            var observer=app.Session.Streamer.View.GetComponent<DVCamcorderDisplay>();
            Assert.That(reporter.Preset,Is.SameAs(Resources.Load<Material>("HorrorDVSignal_Reporter")));
            Assert.That(observer.Preset,Is.SameAs(Resources.Load<Material>("HorrorDVSignal_Streamer")));
            Assert.That(reporter.Preset,Is.Not.SameAs(observer.Preset));
            Assert.That(observer.RenderToTexture,Is.True);Assert.That(observer.EffectMaterial,Is.Not.Null);
            Assert.That(observer.GetComponentsInChildren<Canvas>(true),Is.Empty);
            Assert.That(reporter.RenderToTexture,Is.False,"Evidence captures must keep bypassing DV signal processing.");
            float unchanged=observer.BlurRadius;
            custom=new Material(reporter.Preset);custom.SetFloat("_BlurRadius",.2f);
            reporter.UsePreset(custom);yield return null;
            Assert.That(reporter.BlurRadius,Is.EqualTo(.2f).Within(.001f));
            custom.SetFloat("_BlurRadius",2.7f);custom.SetVector("_TargetResolution",new Vector4(960,540,0,0));
            yield return null;
            Assert.That(reporter.BlurRadius,Is.EqualTo(2.7f).Within(.001f));
            Assert.That(reporter.EffectMaterial.GetFloat("_BlurRadius"),Is.EqualTo(2.7f).Within(.001f));
            Assert.That(reporter.Resolution,Is.EqualTo(new Vector2Int(960,540)));
            Assert.That(observer.BlurRadius,Is.EqualTo(unchanged));
        }
        [UnityTest] public IEnumerator StreamerPlayerUsesItsOwnScreenEffectAndKeepsItAfterRematch()
        {
            var app=Start(TestRole.Streamer);yield return null;
            var display=app.Session.Streamer.View.GetComponent<DVCamcorderDisplay>();
            Assert.That(app.Session.Streamer.View.targetTexture,Is.Null);
            Assert.That(display.EffectMaterial,Is.Not.Null);
            Assert.That(display.Preset,Is.SameAs(Resources.Load<Material>("HorrorDVSignal_Streamer")));
            Assert.That(display.GetComponentsInChildren<Canvas>(true).Length,Is.EqualTo(1));
            var preset=display.Preset;
            app.ReturnToMenu();app.StartSingle(TestRole.Streamer);app.SetSingleReady(true);app.StartSingleMatch();
            yield return null;
            Assert.That(app.Session.Streamer.View.GetComponents<DVCamcorderDisplay>().Length,Is.EqualTo(1));
            Assert.That(app.Session.Streamer.View.GetComponent<DVCamcorderDisplay>().Preset,Is.SameAs(preset));
        }
    }
}
