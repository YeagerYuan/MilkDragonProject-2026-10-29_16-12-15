using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnderAvatar
{
    // Restore the scene's lighting when the running prototype is destroyed or disabled.
    public sealed class DarkWorldLighting : MonoBehaviour
    {
        readonly List<Light> directionalLights=new List<Light>();
        AmbientMode ambientMode;
        Color ambient;
        float ambientIntensity,reflectionIntensity;
        Material skybox;
        SphericalHarmonicsL2 probe;
        bool fog;
        public bool IsApplied { get; private set; }
        void OnEnable()
        {
            ambientMode=RenderSettings.ambientMode;ambient=RenderSettings.ambientLight;
            ambientIntensity=RenderSettings.ambientIntensity;reflectionIntensity=RenderSettings.reflectionIntensity;
            skybox=RenderSettings.skybox;probe=RenderSettings.ambientProbe;fog=RenderSettings.fog;
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.enabled && light.type==LightType.Directional) { directionalLights.Add(light);light.enabled=false; }
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;
            RenderSettings.ambientIntensity=0;RenderSettings.reflectionIntensity=0;
            RenderSettings.skybox=null;RenderSettings.ambientProbe=new SphericalHarmonicsL2();RenderSettings.fog=false;
            IsApplied=true;
        }
        void OnDisable()
        {
            IsApplied=false;
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;
            RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.reflectionIntensity=reflectionIntensity;
            RenderSettings.skybox=skybox;RenderSettings.ambientProbe=probe;RenderSettings.fog=fog;
            foreach(var light in directionalLights) if(light) light.enabled=true;
            directionalLights.Clear();
        }
    }
}
