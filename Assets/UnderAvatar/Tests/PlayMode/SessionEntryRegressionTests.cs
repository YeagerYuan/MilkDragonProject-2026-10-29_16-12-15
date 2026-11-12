using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class SessionEntryRegressionTests
    {
        GameObject root;
        BuildingAsset asset;
        [UnityTearDown] public IEnumerator Cleanup() {if(root) Object.Destroy(root);if(asset) Object.Destroy(asset);yield return null;}
        [UnityTest] public IEnumerator InvalidFormalMapKeepsHallAndExplainsFailure()
        {
            root=new GameObject("Entry regression");var app=root.AddComponent<PrototypeApp>();
            var data=BuildingData.CreateDefault();data.useTaskEditor=true;data.requireLiveEvidence=false;data.tasks.Add(new InvestigationTaskData());
            asset=ScriptableObject.CreateInstance<BuildingAsset>();asset.SetData(data);app.Building=asset;
            app.StartSingle(TestRole.Streamer);yield return null;var hall=app.Session;
            Assert.That(hall,Is.Not.Null);app.SetSingleReady(true);app.StartSingleMatch();
            Assert.That(app.Session,Is.SameAs(hall));Assert.That(app.Session.IsTraining,Is.True);
            Assert.That(app.StartupError,Does.Contain("目标"));Assert.That(Cursor.visible,Is.True);
        }
        [UnityTest] public IEnumerator NetworkPhoneReconciliationCannotHideDebugMenuCursor()
        {
            root=new GameObject("Cursor regression");var app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Reporter,false);
            var m=app.Session;yield return null;
            m.DebugOpen=true;m.Reporter.SetCursor(true);
            var snapshot=m.CaptureSnapshot();m.ApplySnapshot(snapshot);m.SetPhoneMenu(false);
            Assert.That(Cursor.visible,Is.True);Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
            m.DebugOpen=false;m.Reporter.SetCursor(false);Assert.That(Cursor.visible,Is.False);
        }
    }
}
