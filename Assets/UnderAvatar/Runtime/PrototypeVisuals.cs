using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnderAvatar
{
    public static class PrototypeLayers
    {
        public static readonly string[] Names = { "UA_Wall", "UA_ClosedDoor", "UA_OpenDoor", "UA_Props", "UA_Target", "UA_Player", "UA_Sound", "UA_Reporter" };
        public static int Get(string name)
        {
            int value = LayerMask.NameToLayer(name);
            if (value < 0) throw new System.InvalidOperationException("缺少 Layer: " + name + "。请执行 Tools > Under Avatar > Prepare Prototype。");
            return value;
        }
        public static int PhotoMask => LayerMask.GetMask("UA_Wall", "UA_ClosedDoor", "UA_Props");
        public static int SolidMask => LayerMask.GetMask("UA_Wall", "UA_ClosedDoor", "UA_Props", "UA_Target");
    }

    public sealed class PrototypeVisuals : System.IDisposable
    {
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        public static readonly Color[] ItemColors =
        {
            new Color(.53f,.60f,.66f), new Color(.67f,.40f,.20f), new Color(.33f,.40f,.48f),
            new Color(.96f,.66f,.20f), new Color(.30f,.81f,.80f), new Color(.36f,.59f,.90f)
        };

        public Material Material(Color color, bool transparent = false)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + transparent;
            if (materials.TryGetValue(key, out var material)) return material;
            Shader shader = transparent ? Resources.Load<Shader>("PlacementPreview") : Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var template = Resources.Load<Material>("PrototypeLit");
            material = template && !transparent ? new Material(template) : new Material(shader);
            material.name = "UA_" + key;
            material.SetColor("_BaseColor", color);
            material.color = color;
            if(!transparent) material.SetFloat("_Smoothness", .25f);
            if (transparent)
            {
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            materials.Add(key, material);
            return material;
        }

        public GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Color color,
            string layer = "UA_Props", bool collider = true, bool ghost = false)
        {
            if (ghost) color.a = Mathf.Min(color.a, .4f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.layer = ghost ? 2 : PrototypeLayers.Get(layer);
            go.GetComponent<Collider>().enabled = collider && !ghost;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = Material(color, ghost);
            renderer.shadowCastingMode = ghost ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return go;
        }

        public void Dispose()
        {
            foreach (var material in materials.Values) Release(material);
            materials.Clear();
        }

        public static void Release(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
