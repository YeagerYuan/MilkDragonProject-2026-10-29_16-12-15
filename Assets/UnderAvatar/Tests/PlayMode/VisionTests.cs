using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace UnderAvatar.Tests
{
    public sealed class VisionTests
    {
        GameObject root;
        RenderTexture target;
        Texture2D pixels;
        Material material;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(root) Object.Destroy(root);
            if(target) { target.Release();Object.Destroy(target); }
            if(pixels) Object.Destroy(pixels);
            if(material) Object.Destroy(material);
            yield return null;
        }
        [UnityTest] public IEnumerator LightsFollowWithoutDelayingCameraAndUseReceivedSettingsEvenForDisabledRemoteCamera()
        {
            root=new GameObject("Vision settings verification");var app=root.AddComponent<PrototypeApp>();
            var rules=new MatchSettings();rules.streamerVision.range=9;rules.reporterVision.range=17;
            rules.reporterVision.followSeconds=.1f;
            rules=JsonUtility.FromJson<LanMessage>(JsonUtility.ToJson(new LanMessage{type="welcome",settings=rules})).settings;
            app.StartNetworkSession(BuildingData.CreateDefault(),rules,TestRole.Reporter,false);
            var reporter=app.Session.Reporter;var streamer=app.Session.Streamer;
            yield return null;
            Assert.That(reporter.Vision.Lamp.range,Is.EqualTo(17));
            Assert.That(streamer.Vision.Lamp.range,Is.EqualTo(9));
            Assert.That(streamer.View.enabled,Is.False);
            Assert.That(streamer.Vision.Lamp.enabled,Is.True,"An opponent's physical lamp must work without rendering their camera.");
            reporter.Vision.Step(0,true);
            var before=reporter.Vision.transform.rotation;
            reporter.transform.Rotate(0,90,0);var cameraAfter=reporter.View.transform.rotation;
            reporter.Vision.Step(.01f);
            Assert.That(Quaternion.Angle(cameraAfter,before),Is.GreaterThan(89));
            Assert.That(Quaternion.Angle(cameraAfter,reporter.Vision.transform.rotation),Is.GreaterThan(40));
            Assert.That(reporter.View.transform.rotation,Is.EqualTo(cameraAfter));
            app.RuntimeSettings.reporterVision.followSeconds=0;
            app.RuntimeSettings.reporterVision.range=14;
            reporter.Vision.Step(.01f);
            Assert.That(Quaternion.Angle(cameraAfter,reporter.Vision.transform.rotation),Is.LessThan(.01f));
            Assert.That(reporter.Vision.Lamp.range,Is.EqualTo(14));
            var pose=reporter.CapturePose();pose.hidden=true;reporter.ApplyPose(pose);reporter.Vision.Step(.01f);
            Assert.That(reporter.Vision.Lamp.enabled,Is.False,"A hiding reporter must not leak light through the wardrobe.");
            pose.hidden=false;reporter.ApplyPose(pose);reporter.Vision.Step(.01f);
            Assert.That(reporter.Vision.Lamp.enabled,Is.True);
        }
        [UnityTest] public IEnumerator DarknessRestoresExistingEnvironmentAndDirectionalLightOnExit()
        {
            var oldAmbient=RenderSettings.ambientLight;var oldMode=RenderSettings.ambientMode;
            var oldReflection=RenderSettings.reflectionIntensity;var oldSky=RenderSettings.skybox;
            root=new GameObject("Lighting lifecycle verification");
            var sun=root.AddComponent<Light>();sun.type=LightType.Directional;sun.enabled=true;
            var darkness=root.AddComponent<DarkWorldLighting>();
            Assert.That(sun.enabled,Is.False);Assert.That(RenderSettings.ambientLight,Is.EqualTo(Color.black));
            Assert.That(RenderSettings.reflectionIntensity,Is.Zero);
            Object.Destroy(darkness);yield return null;
            Assert.That(sun.enabled,Is.True);Assert.That(RenderSettings.ambientLight,Is.EqualTo(oldAmbient));
            Assert.That(RenderSettings.ambientMode,Is.EqualTo(oldMode));
            Assert.That(RenderSettings.reflectionIntensity,Is.EqualTo(oldReflection));Assert.That(RenderSettings.skybox,Is.EqualTo(oldSky));
        }
        [UnityTest] public IEnumerator RealSpotIlluminatesSurfacesAndOccluderCastsShadowWithoutScreenMask()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null),"Run this render check with a graphics device.");
            root=new GameObject("Physical spotlight verification");root.AddComponent<DarkWorldLighting>();
            var eye=new GameObject("Light direction");eye.transform.SetParent(root.transform);
            eye.transform.position=new Vector3(400,2,0);var source=eye.AddComponent<Camera>();source.enabled=false;
            var settings=VisionLightSettings.Reporter();settings.intensity=20;settings.cookieStrength=0;settings.followSeconds=0;
            var beam=VisionLight.Create(source,()=>settings,"UA_Reporter");
            material=new Material(Resources.Load<Material>("PrototypeLit"));material.SetColor("_BaseColor",Color.white);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);
            wall.transform.position=new Vector3(400,2,6);wall.transform.localScale=new Vector3(12,10,.2f);
            wall.GetComponent<Renderer>().sharedMaterial=material;
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(root.transform);
            blocker.transform.position=new Vector3(400,2,3);blocker.transform.localScale=new Vector3(.7f,2,.3f);
            blocker.GetComponent<Renderer>().sharedMaterial=material;blocker.SetActive(false);
            var cameraObject=new GameObject("Observer");cameraObject.transform.SetParent(root.transform);
            var camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(398,2,0);
            Vector3 sample=new Vector3(400,2,5.89f);camera.transform.LookAt(sample);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.farClipPlane=30;
            target=new RenderTexture(256,256,24);target.Create();camera.targetTexture=target;
            pixels=new Texture2D(256,256,TextureFormat.RGB24,false);
            yield return null;yield return null;
            float lit=Read(camera,sample);
            blocker.SetActive(true);yield return null;yield return null;
            float shadow=Read(camera,sample);
            blocker.SetActive(false);beam.Lamp.enabled=false;beam.enabled=false;yield return null;yield return null;
            float dark=Read(camera,sample);
            Debug.Log($"Spotlight render verification: lit={lit:F4}, shadow={shadow:F4}, dark={dark:F4}");
            Assert.That(lit,Is.GreaterThan(.08f),"A surface inside the physical light cone must be visible.");
            Assert.That(shadow,Is.LessThan(lit*.3f),"An object outside the camera ray must still block light and cast a real shadow.");
            Assert.That(dark,Is.LessThan(.015f),"No ambient or sky reflection should illuminate unlit geometry.");
        }
        float Read(Camera camera,Vector3 point)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();RenderTexture.active=previous;
            var uv=camera.WorldToViewportPoint(point);float sum=0;
            int x=Mathf.RoundToInt(uv.x*255),y=Mathf.RoundToInt(uv.y*255);
            for(int dx=-2;dx<=2;dx++) for(int dy=-2;dy<=2;dy++) sum+=pixels.GetPixel(x+dx,y+dy).grayscale;
            return sum/25;
        }
    }
}
