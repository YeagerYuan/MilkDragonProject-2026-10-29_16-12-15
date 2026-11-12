using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnderAvatar.Editor
{
    public static class HorrorSceneSetupWindow
    {
        const string ProfilePath = "Assets/Settings/Resources/HorrorDVVolume.asset";
        const string SignalPath = "Assets/Settings/Resources/HorrorDVSignal.mat";

        [MenuItem("Tools/Horror Setup/1. Initialize DV Blurry Atmosphere & Post-Processing")]
        public static void Initialize()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipeline)
            {
                Debug.LogError("DV 设置需要使用 URP。");
                return;
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/DV_Camcorder_Blur.shader");
            if (!shader)
            {
                Debug.LogError("找不到 DV 摄像机 Shader。");
                return;
            }

            var renderers = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            for (int i = 0; i < renderers.arraySize; i++)
            {
                if (renderers.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData renderer)
                    ConfigureRenderer(renderer, shader);
            }

            var profile = GetOrCreateProfile();
            if (!AssetDatabase.LoadAssetAtPath<Material>(SignalPath))
                AssetDatabase.CreateAsset(new Material(shader), SignalPath);
            DVRenderSettingsWindow.EnsurePresets();
            var app = Object.FindFirstObjectByType<PrototypeApp>();
            if (app)
            {
                // This scene spawns both roles and the tutorial cameras at runtime.
                var settings = new SerializedObject(app);
                settings.FindProperty("useDvAtmosphere").boolValue = true;
                settings.ApplyModifiedProperties();
            }
            else
            {
                ConfigureStaticScene(profile);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            Debug.Log("DV 渲染配置完成。原型场景进入正式对局后应用。");
        }

        static void ConfigureRenderer(ScriptableRendererData renderer, Shader shader)
        {
            var feature = renderer.rendererFeatures.OfType<DVRenderFeature>().FirstOrDefault();
            if (!feature)
            {
                feature = ScriptableObject.CreateInstance<DVRenderFeature>();
                feature.name = "DV Camcorder";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                var data = new SerializedObject(renderer);
                var map = data.FindProperty("m_RendererFeatureMap");
                map.arraySize = renderer.rendererFeatures.Count;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long id))
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            feature.EffectShader = shader;
            feature.SetActive(true);
            renderer.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
        }

        static VolumeProfile GetOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile) return profile;

            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            if (!AssetDatabase.IsValidFolder("Assets/Settings/Resources")) AssetDatabase.CreateFolder("Assets/Settings", "Resources");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(.15f);
            color.contrast.Override(28f);
            color.saturation.Override(-25f);
            var wheels = profile.Add<LiftGammaGain>();
            wheels.lift.Override(new Vector4(.94f,.96f,1.04f,-.01f));
            wheels.gain.Override(new Vector4(1.05f,1f,.90f,.05f));
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(.85f);
            bloom.intensity.Override(.85f);
            bloom.scatter.Override(.75f);

            foreach (var component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void ConfigureStaticScene(VolumeProfile profile)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = DarkWorldLighting.HorrorAmbient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = DarkWorldLighting.HorrorFog;
            RenderSettings.fogDensity = .045f;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                Undo.RecordObject(light, "Disable directional light for horror");
                light.enabled = false;
            }

            var volumeNode = GameObject.Find("Global Volume_Horror");
            if (!volumeNode)
            {
                volumeNode = new GameObject("Global Volume_Horror");
                Undo.RegisterCreatedObjectUndo(volumeNode, "Create horror volume");
            }
            if (!volumeNode.TryGetComponent<Volume>(out var volume)) volume = Undo.AddComponent<Volume>(volumeNode);
            Undo.RecordObject(volume, "Configure horror volume");
            volume.isGlobal = true;
            volume.priority = 20;
            volume.sharedProfile = profile;
            var camera = Camera.main;
            if (camera)
            {
                if (!camera.TryGetComponent<DVCamcorderDisplay>(out var display)) display = Undo.AddComponent<DVCamcorderDisplay>(camera.gameObject);
                var data = new SerializedObject(display);
                data.FindProperty("createRuntimeVolume").boolValue = false;
                data.ApplyModifiedProperties();
                if (!camera.TryGetComponent<HorrorFlashlightController>(out _)) Undo.AddComponent<HorrorFlashlightController>(camera.gameObject);
            }
            else Debug.LogWarning("场景没有标记为 MainCamera 的相机。");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
