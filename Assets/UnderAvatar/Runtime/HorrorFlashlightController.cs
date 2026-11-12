using UnityEngine;

namespace UnderAvatar
{
    [DefaultExecutionOrder(310), DisallowMultipleComponent]
    public sealed class HorrorFlashlightController : MonoBehaviour
    {
        [SerializeField, Range(.1f, 50)] float beamIntensity = 22f;
        [SerializeField, Min(.1f)] float swaySpeed = 8f;
        [SerializeField, Range(0, 2)] float breathingDegrees = .24f;
        static readonly Color WarmLight = new Color(1f, .90f, .75f);

        public Light MainBeam { get; private set; }
        public Light BounceHalo { get; private set; }

        VisionLight roleLight;
        Quaternion facing;
        float flickerUntil;

        void Start()
        {
            roleLight = GetComponent<VisionLight>();
            MainBeam = roleLight ? roleLight.Lamp : CreateBeam("MainBeam", 16, 42, 15, beamIntensity, LightShadows.Soft);
            BounceHalo = CreateBeam("BounceHalo", 7, 85, 55, .6f, LightShadows.None);
            BounceHalo.cullingMask = MainBeam.cullingMask;
            facing = transform.rotation;
        }

        Light CreateBeam(string label, float range, float outer, float inner, float intensity, LightShadows shadows)
        {
            var node = new GameObject(label);
            node.transform.SetParent(transform, false);
            var lamp = node.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.renderMode = LightRenderMode.ForcePixel;
            lamp.range = range;
            lamp.spotAngle = outer;
            lamp.innerSpotAngle = inner;
            lamp.intensity = intensity;
            lamp.color = WarmLight;
            lamp.shadows = shadows;
            lamp.shadowNearPlane = .05f;
            return lamp;
        }

        public void TriggerFlicker() => flickerUntil = Time.time + .42f;

        void LateUpdate()
        {
            if (!MainBeam) return;
            float time = Time.time;
            float strength = time < flickerUntil ? Mathf.Lerp(.18f, 1, Mathf.PerlinNoise(time * 42, 9)) : 1;
            var breath = Quaternion.Euler(
                (Mathf.PerlinNoise(time * .7f, 4) - .5f) * breathingDegrees * 2,
                (Mathf.PerlinNoise(8, time * .54f) - .5f) * breathingDegrees * 2, 0);

            if (roleLight)
            {
                if (!roleLight.isActiveAndEnabled)
                {
                    BounceHalo.enabled = false;
                    return;
                }
                // VisionLight updates the synchronized beam and its cookie first.
                MainBeam.intensity *= beamIntensity / 9f * strength;
                transform.rotation *= breath;
            }
            else
            {
                facing = Quaternion.Slerp(facing, transform.rotation, 1 - Mathf.Exp(-swaySpeed * Time.deltaTime));
                MainBeam.transform.rotation = facing * breath;
                BounceHalo.transform.rotation = MainBeam.transform.rotation;
                MainBeam.intensity = beamIntensity * strength;
            }
            MainBeam.color = WarmLight;
            BounceHalo.enabled = MainBeam.enabled;
            BounceHalo.intensity = .6f * strength;
        }

        void OnDisable()
        {
            if (BounceHalo) BounceHalo.enabled = false;
            if (!roleLight && MainBeam) MainBeam.enabled = false;
        }

        void OnEnable()
        {
            if (!roleLight && MainBeam) MainBeam.enabled = true;
        }

        void OnDestroy()
        {
            if (BounceHalo) PrototypeVisuals.Release(BounceHalo.gameObject);
            if (!roleLight && MainBeam) PrototypeVisuals.Release(MainBeam.gameObject);
        }
    }
}
