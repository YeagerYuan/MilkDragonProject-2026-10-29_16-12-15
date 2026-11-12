using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class PhoneTaskFlowTests
    {
        GameObject root,guestRoot;MatchSession m;
        void Start()
        {
            root=new GameObject("Phone task flow");var app=root.AddComponent<PrototypeApp>();
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Reporter,true);
            m=app.Session;Assert.That(m,Is.Not.Null,app.StartupError);m.enabled=false;m.Reporter.enabled=false;m.Streamer.Controller.enabled=false;
            m.Reporter.SetPhone(true);
        }
        void Submit(int id,string task,bool correct)
        {
            typeof(MatchSession).GetProperty("PendingPhoto").SetValue(m,new PhotoEvidence{id=id,taskId=task,objectCorrect=correct,regionCorrect=true});
            m.UploadPhoto(id);
        }
        [UnityTearDown] public IEnumerator Cleanup() {if(root) Object.Destroy(root);if(guestRoot) Object.Destroy(guestRoot);yield return null;}
        [UnityTest] public IEnumerator StartsAtFirstTaskAndSkipsCompletedTasksWhenAdvancing()
        {
            Start();yield return null;
            Assert.That(m.ActivePhonePage,Is.EqualTo(PhonePage.Tasks));Assert.That(m.SelectedTaskId,Is.EqualTo(m.Quests[0].Id));
            Submit(1,m.Quests[0].Id,true);Assert.That(m.SelectedTaskId,Is.EqualTo(m.Quests[1].Id));
            m.SelectTask(m.Quests[2].Id);Submit(2,m.Quests[2].Id,true);
            Assert.That(m.SelectedTaskId,Is.EqualTo(m.Quests[1].Id));
            Submit(3,m.Quests[1].Id,true);Assert.That(m.SelectedTaskId,Is.EqualTo(InvestigationData.LiveTask));
        }
        [UnityTest] public IEnumerator FailureCancellationAndPhoneToggleKeepCurrentSelection()
        {
            Start();yield return null;string selected=m.SelectedTaskId;
            Submit(1,selected,false);Assert.That(m.SelectedTaskId,Is.EqualTo(selected));
            typeof(MatchSession).GetProperty("PendingPhoto").SetValue(m,new PhotoEvidence{id=2,taskId=selected});
            m.ApplyPhoneCommand(new PlayerCommand{cancelPhoto=true});Assert.That(m.SelectedTaskId,Is.EqualTo(selected));
            m.Reporter.SetPhone(true);Assert.That(m.ActivePhonePage,Is.EqualTo(PhonePage.Tasks));
            Assert.That(m.SelectedTaskId,Is.EqualTo(selected));
        }
        [UnityTest] public IEnumerator NextTaskAndCompletionAreReplicatedTogether()
        {
            Start();yield return null;
            guestRoot=new GameObject("Phone task guest");var app=guestRoot.AddComponent<PrototypeApp>();
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Reporter,false);
            Submit(1,m.Quests[0].Id,true);
            var wire=JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(m.CaptureSnapshot()));
            app.Session.ApplySnapshot(wire);
            Assert.That(app.Session.Quests[0].Complete,Is.True);
            Assert.That(app.Session.SelectedTaskId,Is.EqualTo(m.Quests[1].Id));
        }
    }
}
