using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class StageOneTests
    {
        GameObject root;
        PrototypeApp app;
        BuildingAsset asset;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            asset=ScriptableObject.CreateInstance<BuildingAsset>();
            var data=BuildingData.CreateDefault();
            data.name="作者保存的关卡";
            data.floors[0].items.First(i=>i.type==ItemType.QuestItem).questDescription="作者配置的取证任务";
            asset.SetData(data);
            root=new GameObject("StageOneTest"); root.SetActive(false);
            app=root.AddComponent<PrototypeApp>(); app.Building=asset; root.SetActive(true);
            app.EnterPlay();
            yield return null;
            Assert.That(app.Session,Is.Not.Null,app.StartupError);
            app.Session.Streamer.Controller.enabled=false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root); Object.Destroy(asset);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayAutomaticallyLoadsAuthoredAssetAndRestartDoesNotMutateIt()
        {
            string original=asset.Data.ToJson();
            Assert.That(app.World.Data.name,Is.EqualTo("作者保存的关卡"));
            Assert.That(app.Session.Quests.Any(q=>q.Description=="作者配置的取证任务"),Is.True);
            Assert.That(app.World.Items.All(i=>i.gameObject.activeInHierarchy),Is.True);
            Assert.That(root.transform.Find("EditorCamera"),Is.Null);
            app.World.Data.name="临时运行状态";
            app.World.Items.First(i=>i.Data.type==ItemType.Door).SetDoorOpen(true);
            Assert.That(asset.Data.ToJson(),Is.EqualTo(original));
            app.RestartGame();
            Assert.That(app.Session,Is.Not.Null,app.StartupError);
            Assert.That(app.World.Data.ToJson(),Is.EqualTo(original));
            Assert.That(app.World.Items.Where(i=>i.Data.type==ItemType.Door).All(i=>!i.IsOpen),Is.True);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ClosedDoorBlocksPhotoRaysButOpenDoorDoesNot()
        {
            var door=app.World.Items.First(i=>i.Floor==0 && i.Data.type==ItemType.Door);
            Vector3 origin=door.transform.position-door.transform.forward*1.3f+Vector3.up*1.3f;
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(origin,door.transform.forward,2.6f,PrototypeLayers.PhotoMask),Is.True);
            door.SetDoorOpen(true); Physics.SyncTransforms();
            Assert.That(Physics.Raycast(origin,door.transform.forward,2.6f,PrototypeLayers.PhotoMask),Is.False);
            door.SetDoorOpen(false); Physics.SyncTransforms();
            Assert.That(Physics.Raycast(origin,door.transform.forward,2.6f,PrototypeLayers.PhotoMask),Is.True);
            yield return null;
        }
    }
}
