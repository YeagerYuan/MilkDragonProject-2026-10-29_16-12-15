using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public static class HorrorPBRMaterialAutoLinker
    {
        static readonly string[] Suffixes = { "BaseMap", "Albedo", "Normal", "Roughness", "Smoothness", "Metallic", "AO" };

        [MenuItem("Tools/Horror Setup/2. Auto-Build PBR Materials from Folder")]
        public static void BuildSelectedFolder()
        {
            var folder = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!AssetDatabase.IsValidFolder(folder) || !folder.StartsWith("Assets", StringComparison.Ordinal))
            {
                string selected = EditorUtility.OpenFolderPanel("选择 PBR 贴图文件夹", Application.dataPath, "");
                if (string.IsNullOrEmpty(selected)) return;
                selected = Path.GetFullPath(selected).Replace('\\', '/');
                string assets = Application.dataPath.Replace('\\', '/');
                if (selected != assets && !selected.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("请选择本项目 Assets 下的贴图文件夹。");
                    return;
                }
                folder = "Assets" + selected.Substring(assets.Length);
            }
            var materials = Build(folder);
            Selection.objects = materials.ToArray();
            Debug.Log($"已组装 {materials.Count} 个 URP/Lit 材质。");
        }

        public static List<Material> Build(string folder)
        {
            var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string stem = Path.GetFileNameWithoutExtension(path);
                foreach (var suffix in Suffixes)
                {
                    if (!stem.EndsWith("_" + suffix, StringComparison.OrdinalIgnoreCase)) continue;
                    string key = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + stem.Substring(0, stem.Length - suffix.Length - 1);
                    if (!groups.TryGetValue(key, out var maps))
                        groups[key] = maps = new Dictionary<string, string>();
                    maps[suffix] = path;
                    break;
                }
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var materials = new List<Material>();
            foreach (var group in groups)
            {
                var maps = group.Value;
                if (!maps.ContainsKey("BaseMap") && !maps.ContainsKey("Albedo")) continue;
                Texture2D Get(string suffix) => maps.TryGetValue(suffix, out var path)
                    ? ConfigureTexture(path, suffix == "Normal", suffix == "BaseMap" || suffix == "Albedo") : null;

                var albedo = Get("BaseMap") ?? Get("Albedo");
                var normal = Get("Normal");
                var occlusion = Get("AO");
                var metal = Get("Metallic");
                var smoothness = Get("Smoothness");
                var roughness = Get("Roughness");
                var packed = Pack(group.Key, metal, smoothness, roughness);

                string materialPath = group.Key + "_DV.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                Undo.RecordObject(material, "Build DV PBR material");
                material.shader = shader;
                material.SetTexture("_BaseMap", albedo);
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", .65f);
                material.SetTexture("_OcclusionMap", occlusion);
                material.SetTexture("_MetallicGlossMap", packed);
                material.SetFloat("_Metallic", 0);
                material.SetFloat("_SmoothnessTextureChannel", 0);
                material.SetFloat("_Smoothness", smoothness || roughness ? 1 : .25f);
                Keyword(material, "_NORMALMAP", normal);
                Keyword(material, "_OCCLUSIONMAP", occlusion);
                Keyword(material, "_METALLICSPECGLOSSMAP", packed);
                material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                EditorUtility.SetDirty(material);
                materials.Add(material);
            }
            AssetDatabase.SaveAssets();
            return materials;
        }

        static void Keyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        static Texture2D ConfigureTexture(string path, bool normal, bool srgb)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                Undo.RecordObject(importer, "Configure DV texture");
                if (normal) importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = srgb;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D Pack(string key, Texture2D metal, Texture2D smoothness, Texture2D roughness)
        {
            if (!metal && !smoothness && !roughness) return null;
            var basis = smoothness ? smoothness : roughness ? roughness : metal;
            int width = basis.width, height = basis.height;
            var m = Read(metal, width, height);
            var s = Read(smoothness ? smoothness : roughness, width, height);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte metallic = m == null ? (byte)0 : m[i].r;
                byte gloss = s == null ? (byte)255 : smoothness ? s[i].r : (byte)(255 - s[i].r);
                pixels[i] = new Color32(metallic, metallic, metallic, gloss);
            }

            string path = key + "_DV_MetallicSmoothness.png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return ConfigureTexture(path, false, false);
        }

        static Color32[] Read(Texture2D source, int width, int height)
        {
            if (!source) return null;
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }
    }
}
