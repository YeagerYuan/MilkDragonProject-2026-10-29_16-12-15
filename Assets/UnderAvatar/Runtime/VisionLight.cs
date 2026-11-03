using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnderAvatar
{
    [Serializable]
    public sealed class VisionLightSettings
    {
        [Range(1,40), Tooltip("光线自然衰减到黑暗的距离，单位米。")] public float range=6.5f;
        [Range(0,150), Tooltip("光斑中心的内锥角。")] public float innerAngle=10;
        [Range(5,160), Tooltip("光斑边缘的外锥角；越大视野越宽。")] public float outerAngle=45;
        [Range(.1f,50)] public float intensity=6;
        [Range(0,.3f), Tooltip("仅影响灯光跟随；0 为立即跟随，不影响鼠标视角。")] public float followSeconds=.065f;
        [Range(0,1), Tooltip("羽化和微弱不规则光斑的强度；0 关闭 Cookie。")] public float cookieStrength=.45f;
        public static VisionLightSettings Reporter() => new VisionLightSettings{range=12,innerAngle=20,outerAngle=75,intensity=9};
        public bool Valid => In(range,1,40) && In(innerAngle,0,150) && In(outerAngle,5,160) && innerAngle<=outerAngle &&
            In(intensity,.1f,50) && In(followSeconds,0,.3f) && In(cookieStrength,0,1);
        static bool In(float value,float min,float max) => value>=min && value<=max;
    }

    // Both cameras see the surfaces this real light illuminates and the shadows it casts.
    [DefaultExecutionOrder(300)]
    public sealed class VisionLight : MonoBehaviour
    {
        public Light Lamp { get; private set; }
        Camera view;
        Func<VisionLightSettings> settings;
        Func<bool> visible;
        Quaternion facing;
        Vector3 lastEye;
        Texture2D cookie;
        float cookieStrength=-1;
        public static VisionLight Create(Camera view,Func<VisionLightSettings> settings,string ownerLayer,Func<bool> visible=null)
        {
            var root=new GameObject("视野聚光灯");root.transform.SetParent(view.transform.parent,false);
            var result=root.AddComponent<VisionLight>();
            result.view=view;result.settings=settings;result.visible=visible;
            result.facing=view.transform.rotation;result.lastEye=view.transform.position;
            result.Lamp=root.AddComponent<Light>();result.Lamp.type=LightType.Spot;
            result.Lamp.renderMode=LightRenderMode.ForcePixel;result.Lamp.shadows=LightShadows.Soft;
            result.Lamp.shadowStrength=1;result.Lamp.shadowBias=.03f;result.Lamp.shadowNormalBias=.02f;
            result.Lamp.shadowNearPlane=.05f;
            var additional=result.Lamp.GetUniversalAdditionalLightData();
            additional.usePipelineSettings=false;
            result.Lamp.cullingMask=~LayerMask.GetMask(ownerLayer,"UA_Sound");
            result.Step(0,true);return result;
        }
        void LateUpdate() { Step(Time.deltaTime); }
        public void Step(float deltaTime,bool snap=false)
        {
            if(!view || Lamp==null || settings==null) return;
            var value=settings();if(value==null) return;
            Lamp.range=Mathf.Clamp(value.range,1,40);Lamp.spotAngle=Mathf.Clamp(value.outerAngle,5,160);
            Lamp.innerSpotAngle=Mathf.Clamp(value.innerAngle,0,Lamp.spotAngle);Lamp.intensity=Mathf.Clamp(value.intensity,.1f,50);
            bool shown=visible==null || visible();
            snap|=!Lamp.enabled && shown || Vector3.Distance(lastEye,view.transform.position)>2;
            Lamp.enabled=shown;
            float follow=Mathf.Clamp(value.followSeconds,0,.3f);
            facing=snap || follow<=0?view.transform.rotation:
                Quaternion.Slerp(facing,view.transform.rotation,1-Mathf.Exp(-Mathf.Max(0,deltaTime)/follow));
            lastEye=view.transform.position;transform.SetPositionAndRotation(lastEye+Vector3.down*.06f,facing);
            // Illumination determines visibility; camera clipping does not cut through nearby geometry.
            view.farClipPlane=Mathf.Max(150,Lamp.range+10);
            float strength=Mathf.Clamp01(value.cookieStrength);
            if(!Mathf.Approximately(strength,cookieStrength)) UpdateCookie(strength);
        }
        void UpdateCookie(float strength)
        {
            cookieStrength=strength;
            if(strength<=0) { Lamp.cookie=null;return; }
            const int size=128;
            if(!cookie) cookie=new Texture2D(size,size,TextureFormat.RGBA32,true,true)
            {name="羽化视野 Cookie",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float u=x/(size-1f)*2-1,v=y/(size-1f)*2-1;
                float radial=Mathf.Sqrt(u*u+v*v),noise=Mathf.PerlinNoise(u*3.1f+19.3f,v*3.1f+8.7f);
                float edge=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,.99f,radial+(noise-.5f)*.07f));
                byte b=(byte)Mathf.RoundToInt(Mathf.Lerp(1,edge*(.92f+.08f*noise),strength)*255);
                pixels[y*size+x]=new Color32(b,b,b,255);
            }
            cookie.SetPixels32(pixels);cookie.Apply(true,false);Lamp.cookie=cookie;
        }
        void OnDestroy() { if(cookie) PrototypeVisuals.Release(cookie); }
    }
}
