using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnderAvatar.Editor
{
    public sealed class ComicBuilderWindow : EditorWindow
    {
        public const string Folder="Assets/UnderAvatar/Art/Comics/Paparazzi";
        public const string PrefabPath="Assets/UnderAvatar/Resources/PaparazziComicCanvas.prefab";
        Texture2D source;
        int darkThreshold=35;
        float borderCoverage=.68f;
        [MenuItem("Tools/Under Avatar/Comic Builder")]
        static void Open() => GetWindow<ComicBuilderWindow>("狗仔漫画工具");
        void OnEnable() { source=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Source.jpg"); }
        void OnGUI()
        {
            EditorGUILayout.LabelField("双页漫画 · 5 幕",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("识别深色横框：左页三排、右页两排。左页底部两个小格合成第 3 幕。生成独立 PNG、遮去剧情的纸张背景、1920×1080 坐标记录和 UGUI 预制体。重新生成会更新同名生成资源。",MessageType.Info);
            source=(Texture2D)EditorGUILayout.ObjectField("漫画原图",source,typeof(Texture2D),false);
            darkThreshold=EditorGUILayout.IntSlider("深色阈值",darkThreshold,10,80);
            borderCoverage=EditorGUILayout.Slider("横框覆盖率",borderCoverage,.5f,.95f);
            using(new EditorGUI.DisabledScope(!source || EditorApplication.isPlaying))
                if(GUILayout.Button("自动切图并生成预制体"))
                {
                    try { Generate(AssetDatabase.GetAssetPath(source),darkThreshold,borderCoverage);Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
                    catch(Exception e) { Debug.LogException(e);EditorUtility.DisplayDialog("漫画生成失败",e.Message,"确定"); }
                }
            if(GUILayout.Button("选中漫画预制体")) Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        [Serializable] public sealed class PanelRecord { public string name;public RectInt sourcePixelsTopLeft;public Vector2 canvasCenter,canvasSize; }
        [Serializable] public sealed class Layout { public Vector2Int sourceSize;public Vector2 canvasSize=new Vector2(1920,1080);public PanelRecord[] panels; }
        static bool Dark(Color32 c,int threshold) => c.r<threshold && c.g<threshold && c.b<threshold;

        public static RectInt[] Detect(Texture2D texture,int threshold=35,float coverage=.68f)
        {
            int w=texture.width,h=texture.height;var pixels=texture.GetPixels32();var result=new List<RectInt>();
            for(int page=0;page<2;page++)
            {
                int l=(int)((page*.5f+.025f)*w),r=(int)((page*.5f+.465f)*w),start=-1;
                var bands=new List<Vector2Int>();
                for(int y=0;y<=h;y++)
                {
                    int dark=0;
                    if(y<h) for(int x=l;x<r;x++) if(Dark(pixels[(h-1-y)*w+x],threshold)) dark++;
                    bool edge=y<h && dark>(r-l)*coverage;
                    if(edge && start<0) start=y;
                    if(!edge && start>=0) { if(y-start>=2) bands.Add(new Vector2Int(start,y-1));start=-1; }
                }
                int expected=page==0?6:4;
                if(bands.Count!=expected) throw new InvalidOperationException((page==0?"左":"右")+"页检测到 "+bands.Count+" 条横框，需要 "+expected+" 条。请调整深色阈值或横框覆盖率；本工具适用于左三排、右两排的双页布局。");
                int sampleY=(bands[0].x+bands[0].y)/2;
                int left=(int)((page*.5f+.005f)*w),right=(int)((page*.5f+.485f)*w);
                while(left<r && !Dark(pixels[(h-1-sampleY)*w+left],threshold)) left++;
                while(right>l && !Dark(pixels[(h-1-sampleY)*w+right],threshold)) right--;
                for(int i=0;i<bands.Count;i+=2)
                {
                    // Include text boxes/hair that protrude above a frame, without stealing the previous row.
                    int previous=i==0?0:bands[i-1].y+3;
                    int top=Mathf.Max(previous,bands[i].x-Mathf.RoundToInt(h*.012f));
                    int bottom=Mathf.Min(h,bands[i+1].y+3);
                    result.Add(new RectInt(Mathf.Max(0,left-2),top,Mathf.Min(w,right+3)-Mathf.Max(0,left-2),bottom-top));
                }
            }
            return result.ToArray();
        }

        public static void Generate(string sourcePath,int threshold=35,float coverage=.68f)
        {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            GameObject root=null;
            try
            {
                if(!source.LoadImage(File.ReadAllBytes(sourcePath))) throw new InvalidOperationException("无法读取漫画图片。");
                var rects=Detect(source,threshold,coverage);Directory.CreateDirectory(Folder);
                var pixels=source.GetPixels32();var background=(Color32[])pixels.Clone();
                var records=new PanelRecord[rects.Length];var sprites=new Sprite[rects.Length];
                float scale=Mathf.Min(1920f/source.width,1080f/source.height);
                for(int i=0;i<rects.Length;i++)
                {
                    var rect=rects[i];var crop=new Texture2D(rect.width,rect.height,TextureFormat.RGBA32,false);
                    crop.SetPixels(source.GetPixels(rect.x,source.height-rect.yMax,rect.width,rect.height));crop.Apply();
                    string path=Folder+"/Panel_"+(i+1)+".png";File.WriteAllBytes(path,crop.EncodeToPNG());DestroyImmediate(crop);
                    sprites[i]=ImportSprite(path);
                    // Scatter samples from the empty top paper margin to avoid repeated scanline stripes.
                    for(int y=rect.y;y<rect.yMax;y++) for(int x=rect.x;x<rect.xMax;x++)
                    {
                        uint hash=unchecked((uint)(x*73856093 ^ y*19349663));
                        int px=32+(int)(hash%(uint)(source.width/2-80));
                        int py=3+(int)((hash>>16)%22);
                        background[(source.height-1-y)*source.width+x]=pixels[(source.height-1-py)*source.width+px];
                    }
                    records[i]=new PanelRecord{name="Panel_"+(i+1),sourcePixelsTopLeft=rect,
                        canvasCenter=new Vector2(rect.center.x-source.width*.5f,source.height*.5f-rect.center.y)*scale,
                        canvasSize=new Vector2(rect.width,rect.height)*scale};
                }
                source.SetPixels32(background);source.Apply();string bgPath=Folder+"/Background.png";File.WriteAllBytes(bgPath,source.EncodeToPNG());
                var bg=ImportSprite(bgPath);
                File.WriteAllText(Folder+"/Layout.json",JsonUtility.ToJson(new Layout{sourceSize=new Vector2Int(source.width,source.height),panels=records},true));
                root=new GameObject("PaparazziComicCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
                var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                root.AddComponent<Image>().color=Color.black;
                var sheet=Rect("Artwork",root.transform);Stretch(sheet);
                var fit=sheet.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=(float)source.width/source.height;
                var bgImage=sheet.gameObject.AddComponent<Image>();bgImage.sprite=bg;bgImage.raycastTarget=false;
                var groups=new CanvasGroup[5];
                for(int i=0;i<rects.Length;i++)
                {
                    var r=rects[i];var panel=Rect("Panel_"+(i+1),sheet);
                    panel.anchorMin=new Vector2((float)r.x/source.width,1f-(float)r.yMax/source.height);
                    panel.anchorMax=new Vector2((float)r.xMax/source.width,1f-(float)r.y/source.height);panel.offsetMin=panel.offsetMax=Vector2.zero;
                    var image=panel.gameObject.AddComponent<Image>();image.sprite=sprites[i];image.raycastTarget=false;
                    groups[i]=panel.gameObject.AddComponent<CanvasGroup>();groups[i].alpha=0;groups[i].blocksRaycasts=false;
                }
                var skip=Rect("Skip",root.transform);skip.anchorMin=skip.anchorMax=new Vector2(1,1);skip.pivot=new Vector2(1,1);skip.anchoredPosition=new Vector2(-18,-8);skip.sizeDelta=new Vector2(130,32);
                skip.gameObject.AddComponent<Image>().color=new Color(.08f,.07f,.06f,.92f);
                var button=skip.gameObject.AddComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};
                Label("Label",skip,"跳过",22);
                var hint=Rect("Hint",root.transform);hint.anchorMin=hint.anchorMax=new Vector2(.5f,0);hint.pivot=new Vector2(.5f,0);hint.anchoredPosition=new Vector2(0,8);hint.sizeDelta=new Vector2(350,30);
                hint.gameObject.AddComponent<Image>().color=new Color(.08f,.07f,.06f,.92f);hint.GetComponent<Image>().raycastTarget=false;
                var progress=Label("Progress",hint,"点击继续  ·  0 / 5",20);
                root.AddComponent<ComicPlayerUI>().Configure(groups,button,progress);
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                Debug.Log("已生成 5 幕漫画及 UGUI 预制体："+PrefabPath);
            }
            finally { if(root) DestroyImmediate(root);DestroyImmediate(source); }
        }
        static Sprite ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static RectTransform Rect(string name,Transform parent)
        { var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect; }
        static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
        static Text Label(string name,Transform parent,string value,int size)
        {
            var rect=Rect(name,parent);Stretch(rect);var text=rect.gameObject.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text=value;text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.96f,.91f,.79f);text.raycastTarget=false;return text;
        }
    }
}
