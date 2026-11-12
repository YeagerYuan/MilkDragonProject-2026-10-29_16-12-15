using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class SettingsMenuTests
    {
        GameObject root;
        Keyboard keyboard;
        float volume,scale,preference;
        bool hadPreference;
        InputSettings.BackgroundBehavior background;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
#endif
        [SetUp] public void Setup()
        {
            volume=AudioListener.volume;scale=Time.timeScale;
            hadPreference=PlayerPrefs.HasKey(PrototypeApp.VolumePreference);
            preference=PlayerPrefs.GetFloat(PrototypeApp.VolumePreference,1);
            background=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard=InputSystem.AddDevice<Keyboard>();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(root) Object.Destroy(root);yield return null;
            InputSystem.RemoveDevice(keyboard);InputSystem.settings.backgroundBehavior=background;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
#endif
            if(hadPreference) PlayerPrefs.SetFloat(PrototypeApp.VolumePreference,preference);else PlayerPrefs.DeleteKey(PrototypeApp.VolumePreference);
            PlayerPrefs.Save();AudioListener.volume=volume;Time.timeScale=scale;
        }
        PrototypeApp App()
        {
            root=new GameObject("SettingsTest");return root.AddComponent<PrototypeApp>();
        }
        [UnityTest] public IEnumerator EscapePausesSinglePlayerAndResumeKeepsTheSameMatch()
        {
            var app=App();app.StartSingle(TestRole.Reporter);app.Comic.Finish();var match=app.Session;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;
            Assert.That(app.SettingsOpen,Is.True);Assert.That(app.Session,Is.SameAs(match));
            Assert.That(Time.timeScale,Is.Zero);Assert.That(match.InputCaptured,Is.False);
            Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;
            Assert.That(app.SettingsOpen,Is.False);Assert.That(app.Session,Is.SameAs(match));
            Assert.That(Time.timeScale,Is.EqualTo(scale));
        }
        [UnityTest] public IEnumerator NetworkMenuDoesNotPauseAuthorityAndReturningRestoresTime()
        {
            var app=App();app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Reporter,true,true);
            app.OpenSettings();yield return null;
            Assert.That(Time.timeScale,Is.EqualTo(scale));Assert.That(app.Session.InputCaptured,Is.False);
            var actor=app.Session.Streamer;var start=actor.transform.position;
            app.Session.SimulateNetworkActor(TestRole.Streamer,new PlayerCommand{y=1},.1f);
            Assert.That(Vector3.Distance(start,actor.transform.position),Is.GreaterThan(.05f),"Remote player must still run while local settings are open.");
            app.ReturnToMenu();yield return null;
            Assert.That(app.Session,Is.Null);Assert.That(app.SettingsOpen,Is.False);Assert.That(Time.timeScale,Is.EqualTo(scale));
        }
        [UnityTest] public IEnumerator VolumePersistsAcrossMatchesAndAppRecreation()
        {
            var app=App();app.StartSingle(TestRole.Streamer);app.OpenSettings();app.SetMasterVolume(.23f);app.CloseSettings();
            app.ReturnToMenu();Object.Destroy(root);yield return null;
            app=App();Assert.That(app.MasterVolume,Is.EqualTo(.23f).Within(.001f));
            app.SetMasterVolume(2);Assert.That(app.MasterVolume,Is.EqualTo(1));
            app.SetMasterVolume(-1);Assert.That(app.MasterVolume,Is.Zero);
        }
    }
}
