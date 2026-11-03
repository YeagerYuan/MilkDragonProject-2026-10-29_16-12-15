using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnderAvatar.Editor
{
    // Author clips against this FBX's existing bind pose. These are Generic clips, not universal retargeting data.
    public static class CharacterModelSetup
    {
        const string Folder="Assets/UnderAvatar/Characters";
        public static void RebuildGeneratedClips()
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Reporter_DouJie.prefab"));
            try { Clip(root,"Idle",2,0,false);Clip(root,"Walk",.78f,1,false);Clip(root,"QuietWalk",1.15f,.48f,false);Clip(root,"CrouchIdle",2,0,true);Clip(root,"CrouchWalk",1.2f,.5f,true);AssetDatabase.SaveAssets(); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        public static void CreateProvidedModels()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var profile=Resources.Load<CharacterVisualProfile>("CharacterVisuals");
            if(!profile) { profile=ScriptableObject.CreateInstance<CharacterVisualProfile>();AssetDatabase.CreateAsset(profile,"Assets/UnderAvatar/Resources/CharacterVisuals.asset"); }
            profile.reporter.prefab=Create("你豆姐","Reporter_DouJie",1.65f,0,true);
            profile.streamer.prefab=Create("恐怖奶龙","Streamer_HorrorMilkDragon",1.9f,-90,false);
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
        }
        static GameObject Create(string sourceName,string name,float height,float yaw,bool animate)
        {
            string file=AssetDatabase.FindAssets("t:Model",new[]{"Assets/Models/"+sourceName}).Select(AssetDatabase.GUIDToAssetPath).First();
            if(!animate && AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/HorrorMilkDragon_Game.fbx")) file=Folder+"/HorrorMilkDragon_Game.fbx";
            var root=new GameObject(name);
            try
            {
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(file),root.transform);
                model.name="Model";model.transform.localRotation=Quaternion.Euler(0,yaw,0)*model.transform.localRotation;
                foreach(var a in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(a);
                var bounds=Bounds(model);float scale=height/bounds.size.y;model.transform.localScale*=scale;
                bounds=Bounds(model);model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                // Imported metallic/roughness channels vary across exporters. Use a matte, lit material with original base/normal maps.
                string matPath=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(!material) { material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,matPath); }
                var textures=AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Models/"+sourceName}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
                var color=textures.First(p=>p.Contains("basecolor"));var normal=textures.FirstOrDefault(p=>p.Contains("normal"));
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(color));material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.22f);
                if(normal!=null)
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(normal);
                    if(importer.textureType!=TextureImporterType.NormalMap) { importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport(); }
                    material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));material.SetFloat("_BumpScale",.65f);material.EnableKeyword("_NORMALMAP");
                }
                foreach(var r in model.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();
                    if(r is SkinnedMeshRenderer skin) { skin.updateWhenOffscreen=true;skin.quality=SkinQuality.Bone4; }
                }
                EditorUtility.SetDirty(material);
                if(animate)
                {
                    var animator=root.AddComponent<Animator>();animator.applyRootMotion=false;
                    animator.runtimeAnimatorController=Controller(root);
                }
                return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static Bounds Bounds(GameObject g)
        {
            Bounds result=default;bool first=true;
            foreach(var renderer in g.GetComponentsInChildren<Renderer>())
            {
                var mesh=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if(!mesh) continue;
                // Imported SkinnedMeshRenderer bounds include animation padding and are not the actual feet.
                var b=mesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var p=renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first) {result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);
                }
            }
            return result;
        }
        static AnimatorController Controller(GameObject root)
        {
            string path=Folder+"/ReporterLocomotion.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller) return controller; // Preserve subsequent manual animation edits.
            controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach(string p in new[]{"Speed","MoveX","MoveZ"}) controller.AddParameter(p,AnimatorControllerParameterType.Float);
            controller.AddParameter("Crouched",AnimatorControllerParameterType.Bool);controller.AddParameter("Sneaking",AnimatorControllerParameterType.Bool);
            var idle=Clip(root,"Idle",2,0,false);var walk=Clip(root,"Walk",.78f,1,false);
            var quiet=Clip(root,"QuietWalk",1.15f,.48f,false);var crouch=Clip(root,"CrouchIdle",2,0,true);var crouchWalk=Clip(root,"CrouchWalk",1.2f,.5f,true);
            var machine=controller.layers[0].stateMachine;
            var standing=State(controller,machine,"Walking",idle,walk,2.2f,new Vector3(250,0));
            var sneaking=State(controller,machine,"Quiet",idle,quiet,.99f,new Vector3(250,80));
            var crouching=State(controller,machine,"Crouching",crouch,crouchWalk,.99f,new Vector3(250,160));machine.defaultState=standing;
            Transition(standing,crouching,"Crouched",true);Transition(sneaking,crouching,"Crouched",true);
            var t=Transition(standing,sneaking,"Sneaking",true);t.AddCondition(AnimatorConditionMode.IfNot,0,"Crouched");
            t=Transition(sneaking,standing,"Sneaking",false);t.AddCondition(AnimatorConditionMode.IfNot,0,"Crouched");
            t=Transition(crouching,sneaking,"Crouched",false);t.AddCondition(AnimatorConditionMode.If,0,"Sneaking");
            t=Transition(crouching,standing,"Crouched",false);t.AddCondition(AnimatorConditionMode.IfNot,0,"Sneaking");
            return controller;
        }
        static AnimatorState State(AnimatorController controller,AnimatorStateMachine machine,string name,AnimationClip idle,AnimationClip moving,float speed,Vector3 position)
        {
            var tree=new BlendTree{name=name+" Blend",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};
            tree.AddChild(idle,0);tree.AddChild(moving,speed);AssetDatabase.AddObjectToAsset(tree,controller);
            var state=machine.AddState(name,position);state.motion=tree;return state;
        }
        static AnimatorStateTransition Transition(AnimatorState a,AnimatorState b,string flag,bool value)
        { var t=a.AddTransition(b);t.hasExitTime=false;t.hasFixedDuration=true;t.duration=.12f;t.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,flag);return t; }
        static AnimationClip Clip(GameObject root,string name,float length,float stride,bool crouched)
        {
            var clip=new AnimationClip{name=name,frameRate=30};
            var bones=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("mixamorig:")).ToDictionary(t=>t.name.Substring(10));
            var rotations=bones.ToDictionary(p=>p.Key,p=>p.Value.localRotation);
            float footHeight=Mathf.Min(bones["LeftFoot"].position.y,bones["RightFoot"].position.y);
            foreach(var pair in bones)
            {
                string n=pair.Key;var bone=pair.Value;Quaternion rest=bone.localRotation;
                if(!new[]{"LeftUpLeg","RightUpLeg","LeftLeg","RightLeg","LeftFoot","RightFoot","LeftArm","RightArm","Spine","Neck"}.Contains(n)) continue;
                int keys=(stride>0 && n!="Neck" || n=="Spine")?40:1;
                var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                for(int i=0;i<=keys;i++)
                {
                    float phase=(float)i/keys*Mathf.PI*2,swing=Mathf.Sin(phase)*(n.StartsWith("Right")?-1:1);
                    float angle=0;
                    if(n.EndsWith("UpLeg")) angle=(crouched?-70:0)-swing*26*stride;
                    else if(n=="LeftLeg" || n=="RightLeg") angle=(crouched?130:0)+Mathf.Max(0,swing)*40*stride;
                    else if(n=="LeftFoot" || n=="RightFoot") angle=(crouched?-60:0)+swing*26*stride-Mathf.Max(0,swing)*40*stride;
                    else if(n=="Spine") angle=(crouched?55:0)+Mathf.Sin(phase)*1.3f;
                    else if(n=="Neck") angle=crouched?-25:0;
                    else if(n=="LeftArm" || n=="RightArm") angle=swing*9*stride;
                    Quaternion q=rest*Quaternion.AngleAxis(angle,bone.InverseTransformDirection(root.transform.right));
                    float time=length*i/keys;curves[0].AddKey(time,q.x);curves[1].AddKey(time,q.y);curves[2].AddKey(time,q.z);curves[3].AddKey(time,q.w);
                }
                string bind=AnimationUtility.CalculateTransformPath(bone,root.transform);
                for(int j=0;j<4;j++) AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(bind,typeof(Transform),"m_LocalRotation."+"xyzw"[j]),curves[j]);
            }
            var hips=bones["Hips"];Vector3 hipRest=hips.localPosition;var pc=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
            for(int i=0;i<=40;i++)
            {
                float phase=i/40f*Mathf.PI*2;
                clip.SampleAnimation(root,length*i/40);hips.localPosition=hipRest;
                float lowest=Mathf.Min(bones["LeftFoot"].position.y,bones["RightFoot"].position.y);
                Vector3 delta=Vector3.up*(footHeight-lowest+.006f*(1-Mathf.Cos(phase*2))*stride);
                Vector3 p=hipRest+hips.parent.InverseTransformVector(delta);
                for(int j=0;j<3;j++) pc[j].AddKey(length*i/40,p[j]);
            }
            hips.localPosition=hipRest;foreach(var b in bones) b.Value.localRotation=rotations[b.Key];
            for(int j=0;j<3;j++) AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(hips,root.transform),typeof(Transform),"m_LocalPosition."+"xyz"[j]),pc[j]);
            clip.EnsureQuaternionContinuity();var config=AnimationUtility.GetAnimationClipSettings(clip);config.loopTime=true;config.loopBlend=true;AnimationUtility.SetAnimationClipSettings(clip,config);
            string path=Folder+"/"+name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing) { EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);EditorUtility.SetDirty(existing);return existing; }
            AssetDatabase.CreateAsset(clip,path);return clip;
        }
    }
}
