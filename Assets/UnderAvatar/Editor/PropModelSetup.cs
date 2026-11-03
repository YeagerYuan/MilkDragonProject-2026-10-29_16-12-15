using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    // The FBX files stay intact; reusable wrappers and URP materials live in Props.
    public static class PropModelSetup
    {
        public const string Folder="Assets/UnderAvatar/Props";
        [MenuItem("Tools/Under Avatar/Import Provided Prop Models")]
        public static void Import()
        {
            Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
            var catalog=LevelObjectCatalogEditor.EnsureCatalog();Undo.RecordObject(catalog,"替换场景模型");
            Bind(catalog,"床","Bed",ItemType.Bed,false,90);
            Bind(catalog,"柜子","Wardrobe",ItemType.Furniture,true,180);
            Bind(catalog,"电脑","ComputerDesk",ItemType.StreamerPC,false,180);
            Bind(catalog,"门","DoorLeaf",ItemType.Door,false,180);
            Bind(catalog,"桌子","Table",ItemType.Furniture,false,0);
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();BuildingEditorPreview.Rebuild();
            Selection.activeObject=catalog;EditorGUIUtility.PingObject(catalog);
            Debug.Log("[UnderAvatar] 床、衣柜、电脑桌、门扇和桌子模型已接入物体库。");
        }
        static void Bind(LevelObjectCatalog catalog,string sourceName,string name,ItemType type,bool wardrobe,float yaw)
        {
            string path=Directory.GetFiles("Assets/Models/"+sourceName,"*.fbx").Single();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!source) throw new InvalidOperationException("模型未导入："+path);
            var material=MakeMaterial(sourceName,name,Path.ChangeExtension(path,null)+".fbm");
            var wrapper=new GameObject(name);
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(wrapper.transform,false);
                model.transform.localRotation=Quaternion.Euler(0,yaw,0)*source.transform.localRotation;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();
                var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Folder+"/"+name+".prefab");
                var definition=catalog.objects.Single(d=>d.builtIn && d.type==type && (type!=ItemType.Furniture || d.wardrobe==wardrobe));
                definition.prefab=prefab;definition.fitToPrototype=true;definition.rotation=Vector3.zero;
                definition.offset=Vector3.zero;definition.scale=Vector3.one;
                if(type==ItemType.StreamerPC)
                {
                    // The supplied model includes its desk, but no seat. Fit to the original desk
                    // footprint rather than stretching it over the old decorative chair's bounds.
                    definition.scale.z=1.05f/1.8f;definition.offset.z=.375f;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        }
        static Material MakeMaterial(string source,string name,string textures)
        {
            string TexturePath(string suffix)=>Directory.GetFiles(textures,source+"_"+suffix+".*").Single(p=>!p.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)).Replace('\\','/');
            string normalPath=TexturePath("normal");
            var normalImporter=(TextureImporter)AssetImporter.GetAtPath(normalPath);
            if(normalImporter.textureType!=TextureImporterType.NormalMap)
            { normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.SaveAndReimport(); }
            string metallicPath=TexturePath("metallic"),roughnessPath=TexturePath("roughness");
            foreach(var path in new[]{metallicPath,roughnessPath})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer.sRGBTexture) { importer.sRGBTexture=false;importer.SaveAndReimport(); }
            }
            var metallic=ReadTexture(AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath));
            var roughness=ReadTexture(AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath));
            string packedPath=Folder+"/Materials/"+name+"_MetallicSmoothness.png";
            try
            {
                var colors=metallic.GetPixels32();var rough=roughness.GetPixels32();
                for(int i=0;i<colors.Length;i++) colors[i]=new Color32(colors[i].r,0,0,(byte)(255-rough[i].r));
                metallic.SetPixels32(colors);metallic.Apply();File.WriteAllBytes(packedPath,metallic.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(metallic);UnityEngine.Object.DestroyImmediate(roughness); }
            AssetDatabase.ImportAsset(packedPath);
            var packedImporter=(TextureImporter)AssetImporter.GetAtPath(packedPath);
            packedImporter.sRGBTexture=false;packedImporter.alphaSource=TextureImporterAlphaSource.FromInput;packedImporter.SaveAndReimport();
            string materialPath=Folder+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material) { material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath); }
            material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath("basecolor")));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));material.SetFloat("_BumpScale",1);material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Smoothness",1);material.SetFloat("_Metallic",1);material.SetFloat("_SmoothnessTextureChannel",0);
            EditorUtility.SetDirty(material);return material;
        }
        static Texture2D ReadTexture(Texture texture)
        {
            // Resample both data maps to the same size without enabling Read/Write on source assets.
            var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1024,1024,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            try
            {
                Graphics.Blit(texture,rt);RenderTexture.active=rt;
                var result=new Texture2D(1024,1024,TextureFormat.RGBA32,false,true);
                result.ReadPixels(new Rect(0,0,1024,1024),0,0);result.Apply();return result;
            }
            finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt); }
        }
    }
}
