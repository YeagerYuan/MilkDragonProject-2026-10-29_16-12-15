using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class PhysicalStairTests
    {
        GameObject root;
        PrototypeApp app;
        BuildingAsset asset;

        IEnumerator Start(float height)
        {
            asset=ScriptableObject.CreateInstance<BuildingAsset>();
            var data=BuildingData.CreateDefault(); data.floorHeight=height;
            data.floors[1].items.First(i=>i.questId=="item_contract").gridX=4;
            asset.SetData(data);
            root=new GameObject("StairPlayTest"); root.SetActive(false);
            app=root.AddComponent<PrototypeApp>(); app.Building=asset; root.SetActive(true);
            app.EnterPlay();
            yield return null;
            Assert.That(app.Session,Is.Not.Null,app.StartupError);
            app.Session.Streamer.Controller.enabled=false;
            app.Session.Reporter.enabled=false;
            app.Settings.reporterSpeed=5;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(root) Object.Destroy(root); if(asset) Object.Destroy(asset); yield return null;
        }

        [UnityTest] public IEnumerator ReporterWalksBothFlightsAndReturnsWithoutTeleport()
        {
            yield return Start(4);
            yield return WalkUpAndDown();
        }
        [UnityTest] public IEnumerator TallerWallsStillProduceWalkableStairsAndMatchingOpenings()
        {
            yield return Start(6);
            yield return WalkUpAndDown();
        }
        IEnumerator WalkUpAndDown()
        {
            var stair=app.World.Stairs[0]; var reporter=app.Session.Reporter;
            reporter.Teleport(stair.Waypoints(true)[0]); reporter.transform.rotation=Quaternion.identity;
            yield return Walk(stair.Waypoints(true));
            Assert.That(reporter.Floor,Is.EqualTo(stair.Floor+1));
            Assert.That(reporter.transform.position.y,Is.EqualTo(stair.Height).Within(.12f));
            yield return Walk(stair.Waypoints(false));
            Assert.That(reporter.Floor,Is.EqualTo(stair.Floor));
            Assert.That(reporter.transform.position.y,Is.EqualTo(0).Within(.12f));
        }
        IEnumerator Walk(IReadOnlyList<Vector3> points)
        {
            var reporter=app.Session.Reporter;
            foreach(var target in points)
            {
                float deadline=Time.time+4;
                while(Time.time<deadline)
                {
                    Vector3 delta=target-reporter.transform.position;
                    if(new Vector2(delta.x,delta.z).magnitude<.1f && Mathf.Abs(delta.y)<.35f) break;
                    Vector3 before=reporter.transform.position;
                    var horizontal=new Vector2(delta.x,delta.z);
                    // Reduce speed near each waypoint so a frame cannot skip a turn.
                    float magnitude=Mathf.Min(1,horizontal.magnitude/(app.Settings.reporterSpeed*Mathf.Max(.001f,Time.deltaTime)));
                    reporter.MoveInput(horizontal.normalized*magnitude,false,Time.deltaTime);
                    Assert.That(Mathf.Abs(reporter.transform.position.y-before.y),Is.LessThan(.65f),"Unexpected floor teleport");
                    yield return null;
                }
                Assert.That(Time.time,Is.LessThan(deadline),"Blocked on stair at "+reporter.transform.position+" towards "+target);
            }
        }

        [UnityTest] public IEnumerator ReporterCameraRendersDeskAndStreamerInTheirRoom()
        {
            yield return Start(4);
            var match=app.Session; var pc=match.Streamer.Computer;
            match.Streamer.Controller.BeginStreaming();
            match.Reporter.Teleport(pc.transform.position-pc.transform.forward*4+pc.transform.right*2);
            match.Reporter.SetLookAt(pc.transform.position+Vector3.up*1.25f);
            yield return null;
            var view=match.Reporter.View; var rt=new RenderTexture(320,200,24);
            var image=new Texture2D(320,200,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                view.targetTexture=rt; view.Render(); RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,320,200),0,0); image.Apply();
                var pixels=image.GetPixels();
                // Compare with the actual character hidden; model replacement may change its color.
                var renderers=match.Streamer.Appearance.GetComponentsInChildren<Renderer>();
                var visible=renderers.Select(r=>r.enabled).ToArray();
                foreach(var r in renderers) r.enabled=false;
                view.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,320,200),0,0);image.Apply();
                var background=image.GetPixels();
                for(int i=0;i<renderers.Length;i++) renderers[i].enabled=visible[i];
                int actor=pixels.Where((c,i)=>Mathf.Abs(c.r-background[i].r)+Mathf.Abs(c.g-background[i].g)+Mathf.Abs(c.b-background[i].b)>.05f).Count();
                var deskRenderers=pc.GetComponentsInChildren<Renderer>();var deskVisible=deskRenderers.Select(r=>r.enabled).ToArray();
                foreach(var renderer in deskRenderers) renderer.enabled=false;
                view.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,320,200),0,0);image.Apply();
                var withoutDesk=image.GetPixels();
                for(int i=0;i<deskRenderers.Length;i++) deskRenderers[i].enabled=deskVisible[i];
                int desk=pixels.Where((c,i)=>Mathf.Abs(c.r-withoutDesk[i].r)+Mathf.Abs(c.g-withoutDesk[i].g)+Mathf.Abs(c.b-withoutDesk[i].b)>.05f).Count();
                Assert.That(desk,Is.GreaterThan(100),"Computer desk disappeared from reporter camera");
                Assert.That(actor,Is.GreaterThan(100),"Streamer disappeared from reporter camera");
            }
            finally
            {
                view.targetTexture=null; RenderTexture.active=previous; rt.Release();
                Object.Destroy(rt); Object.Destroy(image);
            }
        }
    }
}
