using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class DocumentIntegrationTests
    {
        GameObject root,wall;
        PrototypeApp app;
        Keyboard keyboard;
        MatchSession match;
        DocumentManager reader;
        void Start(TestRole role=TestRole.Reporter,bool network=false)
        {
            keyboard=InputSystem.AddDevice<Keyboard>();
            root=new GameObject("DocumentIntegrationTest");app=root.AddComponent<PrototypeApp>();
            if(network) app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),role,true);
            else { app.Role=role;app.EnterPlay(); }
            match=app.Session;Assert.That(match,Is.Not.Null,app.StartupError);
            match.Streamer.Controller.Driver=null;if(match.ReporterAI) match.ReporterAI.enabled=false;
            reader=DocumentManager.Instance;Assert.That(reader,Is.Not.Null,"Reader prefab must be included in the build.");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(root) Object.Destroy(root);if(wall) Object.Destroy(wall);
            if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
            yield return null;
        }
        [UnityTest] public IEnumerator ImportedDocumentsHavePagesAndHallDoesNotSpawnFormalProps()
        {
            Start();yield return null;
            var docs=match.GetComponentsInChildren<DocumentInteractable>();
            Assert.That(docs.Length,Is.GreaterThanOrEqualTo(4));
            foreach(var doc in docs) { Assert.That(doc.Data,Is.Not.Null);Assert.That(doc.Data.pages.All(p=>p),Is.True); }
            app.StartSingle(TestRole.Reporter);yield return null;
            Assert.That(app.Session.IsTraining,Is.True);
            Assert.That(app.Session.GetComponentsInChildren<DocumentInteractable>(),Is.Empty);
            app.SetSingleReady(true);app.StartSingleMatch();yield return null;
            Assert.That(app.Session.GetComponentsInChildren<DocumentInteractable>().Length,Is.EqualTo(docs.Length));
        }
        [UnityTest] public IEnumerator EscapeClosesReaderWithoutExitingMatchOrTriggeringPhoto()
        {
            Start();yield return null;
            var document=match.GetComponentsInChildren<DocumentInteractable>().First(d=>d.Data.documentType==DocumentType.SinglePage);
            reader.OpenDocument(document.Data,match);yield return null;
            Assert.That(reader.IsOpen,Is.True);Assert.That(match.InputCaptured,Is.False);
            int score=match.Score,photos=match.StreamerPhotos;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;
            Assert.That(reader.IsOpen,Is.False);Assert.That(app.Session,Is.SameAs(match));
            Assert.That(match.Score,Is.EqualTo(score));Assert.That(match.StreamerPhotos,Is.EqualTo(photos));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        }
        [UnityTest] public IEnumerator DiaryHasPageCurlAndSessionEndRestoresCursor()
        {
            Start();yield return null;
            var data=match.GetComponentsInChildren<DocumentInteractable>().First(d=>d.Data.documentType==DocumentType.Diary).Data;
            reader.OpenDocument(data,match);yield return null;
            var book=reader.GetComponentsInChildren<Book>().Single();
            Assert.That(book.currentPage,Is.EqualTo(1));Assert.That(book.bookPages.Length,Is.GreaterThanOrEqualTo(data.pages.Length));
            Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
            match.Finish("对局结束");Assert.That(reader.IsOpen,Is.False);
            Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
            app.ReturnToMenu();yield return null;Assert.That(reader.IsOpen,Is.False);
        }
        [UnityTest] public IEnumerator DocumentsRespectWallsAndPhoneAndAiCannotRead()
        {
            Start();yield return null;
            var reporter=match.Reporter;
            var doc=match.GetComponentsInChildren<DocumentInteractable>().First(d=>d.Data.documentType==DocumentType.SinglePage);
            doc.transform.position=new Vector3(-4,1.65f,-2);doc.transform.rotation=Quaternion.identity;
            reporter.Teleport(new Vector3(-4,0,-4));reporter.SetLookAt(doc.transform.position);Physics.SyncTransforms();
            Assert.That(reporter.Documents.FindTarget(),Is.EqualTo(doc));
            reporter.SetPhone(true);Assert.That(reporter.Documents.CanRead,Is.False);Assert.That(reporter.Documents.TryRead(),Is.False);
            reporter.SetPhone(false);
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=PrototypeLayers.Get("UA_Wall");
            wall.transform.position=new Vector3(-4,1.6f,-3);wall.transform.localScale=new Vector3(2,3,.2f);Physics.SyncTransforms();
            Assert.That(reporter.Documents.FindTarget(),Is.Null);
            app.ReturnToMenu();app.Role=TestRole.Streamer;app.EnterPlay();yield return null;
            Assert.That(app.Session.Reporter.Documents.CanRead,Is.False);
        }
        [UnityTest] public IEnumerator NetworkReadingKeepsSessionAndClosesOnRestart()
        {
            Start(TestRole.Reporter,true);yield return null;
            var document=match.GetComponentsInChildren<DocumentInteractable>().First();
            reader.OpenDocument(document.Data,match);yield return null;
            Assert.That(match.IsNetwork,Is.True);Assert.That(match.InputCaptured,Is.False);Assert.That(reader.IsOpen,Is.True);
            Assert.That(match.Streamer.Controller.Driver,Is.Null);
            app.StartNetworkSession(BuildingData.CreateDefault(),new MatchSettings(),TestRole.Reporter,true);yield return null;
            Assert.That(reader.IsOpen,Is.False);Assert.That(app.Session.Finished,Is.False);
        }
    }
}
