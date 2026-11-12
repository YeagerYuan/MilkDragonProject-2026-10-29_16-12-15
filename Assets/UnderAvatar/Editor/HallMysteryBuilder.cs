using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public static class HallMysteryBuilder
    {
        const string Folder="Assets/UnderAvatar/Art/Characters/Mystery/";
        [MenuItem("Tools/Under Avatar/Build Hall Mystery Model")]
        public static void Build()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"MysteryBase.fbx");
            if(!source) throw new System.InvalidOperationException("Missing MysteryBase.fbx");
            var root=new GameObject("MysteryHallStreamer");
            try
            {
                var body=Object.Instantiate(source,root.transform);body.name="Body";
                foreach(var old in body.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(old);
                var black=Material("MysteryBlack",new Color(.008f,.011f,.015f));
                var white=Material("MysteryEyes",new Color(.92f,.95f,1));
                foreach(var renderer in body.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>renderer.name=="Eyes"?white:black).ToArray();
                var bones=body.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
                foreach(string side in new[]{"l","r"})
                {
                    var arm=bones["upperarm_"+side];var lower=bones["lowerarm_"+side];
                    arm.rotation=Quaternion.FromToRotation((lower.position-arm.position).normalized,Vector3.down)*arm.rotation;
                }
                var renderers=body.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                float scale=1.85f/bounds.size.y;body.transform.localScale*=scale;
                body.transform.localPosition=Vector3.up*(-bounds.min.y*scale);
                var idle=Clip(root,bones,false);Save(idle,Folder+"MysteryIdle.anim");
                var walk=Clip(root,bones,true);Save(walk,Folder+"MysteryWalk.anim");
                string controllerPath=Folder+"Mystery.controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if(!controller)
                {
                    controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
                    var machine=controller.layers[0].stateMachine;var state=machine.AddState("Locomotion");machine.defaultState=state;
                    var blend=new BlendTree{name="Idle and walk",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(blend,controller);
                    blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"MysteryIdle.anim"),0);
                    blend.AddChild(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"MysteryWalk.anim"),2.2f);state.motion=blend;
                }
                root.AddComponent<Animator>().runtimeAnimatorController=controller;
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"MysteryHallStreamer.prefab");
                var profile=Resources.Load<CharacterVisualProfile>("CharacterVisuals");
                profile.hallStreamer.prefab=prefab;profile.hallStreamer.animatorController=controller;
                profile.hallStreamer.scale=Vector3.one;profile.hallStreamer.offset=Vector3.zero;profile.hallStreamer.rotation=new Vector3(0,180,0);
                EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            }
            finally {Object.DestroyImmediate(root);}
        }
        static Material Material(string name,Color color)
        {
            var path=Folder+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) {material=new Material(Resources.Load<Material>("PrototypeLit")){name=name};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(material);return material;
        }
        static void Save(AnimationClip clip,string path)
        {var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old) {EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(clip,path);}
        static AnimationClip Clip(GameObject root,System.Collections.Generic.Dictionary<string,Transform> bones,bool walk)
        {
            var clip=new AnimationClip{name=walk?"MysteryWalk":"MysteryIdle",frameRate=30};float duration=walk?.85f:2.5f;
            foreach(string name in new[]{"upperarm_l","upperarm_r","thigh_l","thigh_r","calf_l","calf_r","spine_03"})
            {
                var bone=bones[name];var rest=bone.localRotation;string path=AnimationUtility.CalculateTransformPath(bone,root.transform);
                var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                for(int frame=0;frame<=30;frame++)
                {
                    float phase=frame/30f*Mathf.PI*2+(name.EndsWith("_r")?Mathf.PI:0);
                    float angle=walk?(name.StartsWith("thigh")?25*Mathf.Sin(phase):name.StartsWith("calf")?Mathf.Max(0,30*Mathf.Sin(phase)):name.StartsWith("upperarm")?-18*Mathf.Sin(phase):1.5f*Mathf.Sin(phase)): .7f*Mathf.Sin(phase);
                    var q=Quaternion.AngleAxis(angle,bone.parent.InverseTransformDirection(Vector3.right))*rest;
                    for(int i=0;i<4;i++) curves[i].AddKey(duration*frame/30,q[i]);
                }
                for(int i=0;i<4;i++) clip.SetCurve(path,typeof(Transform),"localRotation."+"xyzw"[i],curves[i]);
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
        }
    }
}
