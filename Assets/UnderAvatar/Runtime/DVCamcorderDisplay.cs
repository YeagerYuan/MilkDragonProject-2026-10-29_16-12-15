using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace UnderAvatar
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class DVCamcorderDisplay : MonoBehaviour
    {
        [SerializeField] Vector2Int targetResolution = new Vector2Int(520, 292);
        [SerializeField, Range(0,3)] float blurRadius = 1.25f;
        [SerializeField, Range(0,.02f)] float chromaOffset = .0045f;
        [SerializeField, Range(1,3)] float overexposure = 1.45f;
        [SerializeField, Range(4,64)] float colorSteps = 14;
        [SerializeField, Range(0,.2f)] float noiseIntensity = .05f;
        [SerializeField, Range(0,1)] float vignetteIntensity = .55f;
        [SerializeField] bool showOsd = true;
        [SerializeField] bool createRuntimeVolume = true;
        [SerializeField] bool renderToTexture;
        [SerializeField] Shader effectShader;
        public Material Preset { get; private set; }
        public float BlurRadius => blurRadius;
        public bool RenderToTexture => renderToTexture;
        public Vector2Int Resolution => new Vector2Int(Mathf.Clamp(targetResolution.x, 16, 4096), Mathf.Clamp(targetResolution.y, 16, 4096));
        public Material EffectMaterial { get; private set; }

        UniversalAdditionalCameraData cameraData;
        bool oldPost, oldHdr, oldMsaa;
        GameObject monitor, volumeRoot;
        LayerMask oldVolumeMask;
        Text timeLabel, timeGhost;
        float started;
        int displayedMinute = -1;

        void OnEnable() => SetupCameraPipeline();

        public void SetupCameraPipeline()
        {
            if (EffectMaterial || !Application.isPlaying) return;
            if (GetComponent<Camera>().targetTexture && !renderToTexture) return;
            var shader = effectShader ? effectShader : Shader.Find("UnderAvatar/DV Camcorder Blur");
            if (!shader || !shader.isSupported) return;

            EffectMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            EffectMaterial.EnableKeyword("DV_BLIT");
            var camera = GetComponent<Camera>();
            cameraData = camera.GetUniversalAdditionalCameraData();
            oldPost = cameraData.renderPostProcessing;
            oldHdr = camera.allowHDR;
            oldMsaa = camera.allowMSAA;
            cameraData.renderPostProcessing = true;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            oldVolumeMask = cameraData.volumeLayerMask;
            var profile = Resources.Load<VolumeProfile>("HorrorDVVolume");
            if (createRuntimeVolume && profile)
            {
                volumeRoot = new GameObject("Global Volume_Horror");
                volumeRoot.transform.SetParent(transform, false);
                var volume = volumeRoot.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 20;
                volume.sharedProfile = profile;
                cameraData.volumeLayerMask = oldVolumeMask.value | 1;
            }
            started = Time.time;
            ApplySettings();
            if (showOsd) CreateOsd();
        }

        void ApplySettings()
        {
            var size = Resolution;
            EffectMaterial.SetVector("_TargetResolution", new Vector4(size.x, size.y, 0, 0));
            EffectMaterial.SetFloat("_BlurRadius", blurRadius);
            EffectMaterial.SetFloat("_ChromaOffset", chromaOffset);
            EffectMaterial.SetFloat("_Overexposure", overexposure);
            EffectMaterial.SetFloat("_ColorSteps", colorSteps);
            EffectMaterial.SetFloat("_NoiseIntensity", noiseIntensity);
            EffectMaterial.SetFloat("_VignetteIntensity", vignetteIntensity);
        }

        public void UsePreset(Material preset)
        {
            Preset=preset;
            if (!ReadPreset()) return;
            ApplySettings();
        }

        bool ReadPreset()
        {
            var preset=Preset;
            if (!preset || !EffectMaterial || preset.shader != EffectMaterial.shader) return false;
            var resolution = preset.GetVector("_TargetResolution");
            targetResolution = new Vector2Int(Mathf.RoundToInt(resolution.x), Mathf.RoundToInt(resolution.y));
            blurRadius = preset.GetFloat("_BlurRadius");
            chromaOffset = preset.GetFloat("_ChromaOffset");
            overexposure = preset.GetFloat("_Overexposure");
            colorSteps = preset.GetFloat("_ColorSteps");
            noiseIntensity = preset.GetFloat("_NoiseIntensity");
            vignetteIntensity = preset.GetFloat("_VignetteIntensity");
            return true;
        }

        // The AI observer uses the same signal as the streamer without a second screen overlay.
        public void EnableObserverOutput()
        {
            renderToTexture=true;
            showOsd=false;
            createRuntimeVolume=false;
            SetupCameraPipeline();
        }

        void CreateOsd()
        {
            monitor = new GameObject("DV_Monitor_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            monitor.transform.SetParent(transform, false);
            var canvas = monitor.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = monitor.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);

            Label("CAM", new Vector2(0,1), TextAnchor.UpperLeft);
            Label("[O]  ▶", new Vector2(1,1), TextAnchor.UpperRight);
            Label("[Tv]", new Vector2(1,0), TextAnchor.LowerRight);
            (timeLabel, timeGhost) = Label("PM 05:20", Vector2.zero, TextAnchor.LowerLeft);
        }

        (Text foreground, Text ghost) Label(string value, Vector2 anchor, TextAnchor alignment)
        {
            Text Make(Color tint, Vector2 shift)
            {
                var node = new GameObject(value, typeof(RectTransform), typeof(Text));
                node.transform.SetParent(monitor.transform, false);
                var rect = (RectTransform)node.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
                rect.sizeDelta = new Vector2(170, 20);
                rect.anchoredPosition = new Vector2(anchor.x == 0 ? 12 : -12, anchor.y == 0 ? 4 : -3) + shift;
                var label = node.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 14;
                label.fontStyle = FontStyle.Bold;
                label.alignment = alignment;
                label.raycastTarget = false;
                label.color = tint;
                label.text = value;
                return label;
            }

            var ghost = Make(new Color(.72f, .32f, .46f, .25f), new Vector2(2,-1));
            return (Make(new Color(.94f,.81f,.84f,.76f), Vector2.zero), ghost);
        }

        void LateUpdate()
        {
            if (!EffectMaterial) return;
            ReadPreset();
            ApplySettings();
            if (!monitor) return;
            monitor.SetActive(!DocumentManager.BlocksGameplayInput);
            int minute = (17 * 60 + 20 + Mathf.FloorToInt((Time.time - started) / 60f)) % 1440;
            if (minute == displayedMinute) return;
            displayedMinute = minute;
            int hour = minute / 60;
            timeLabel.text = timeGhost.text = $"{(hour >= 12 ? "PM" : "AM")} {(hour + 11) % 12 + 1:00}:{minute % 60:00}";
        }

        void OnDisable()
        {
            if (monitor)
            {
                monitor.SetActive(false);
                PrototypeVisuals.Release(monitor);
            }
            if (volumeRoot)
            {
                volumeRoot.SetActive(false);
                PrototypeVisuals.Release(volumeRoot);
            }
            if (cameraData)
            {
                cameraData.renderPostProcessing = oldPost;
                cameraData.volumeLayerMask = oldVolumeMask;
                var camera = GetComponent<Camera>();
                camera.allowHDR = oldHdr;
                camera.allowMSAA = oldMsaa;
            }
            if (EffectMaterial) PrototypeVisuals.Release(EffectMaterial);
            EffectMaterial = null;
        }
    }
}
