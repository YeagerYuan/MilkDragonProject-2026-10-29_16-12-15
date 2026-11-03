using System.Collections.Generic;
using UnityEngine;

namespace UnderAvatar
{
    public sealed class PrototypeAudio : MonoBehaviour
    {
        readonly Dictionary<NoiseKind,int> previous=new Dictionary<NoiseKind,int>();
        readonly List<AudioClip> generated=new List<AudioClip>();
        MatchSession session;
        AudioClip chair,success;
        AudioSource breath;
        public GameAudioProfile Profile { get; private set; }
        public float WalkInterval => Profile?Mathf.Max(.1f,Profile.walkInterval):.48f;
        public float ChaseInterval => Profile?Mathf.Max(.1f,Profile.chaseInterval):.3f;

        public void Initialize(MatchSession match,GameAudioProfile profile)
        {
            session=match; Profile=profile?profile:Resources.Load<GameAudioProfile>("GameAudio");
            chair=Profile && Profile.chair?Profile.chair:Create("ChairTurn",.6f,260,.55f);
            success=Profile && Profile.success?Profile.success:Create("Upload",.18f,880,0);
            // Private, local-only channel: breathing must never become a NoiseKind or network event.
            if(session.Role==TestRole.Reporter)
            {
                var go=new GameObject("ReporterPrivateBreathing");go.transform.SetParent(transform,false);
                breath=go.AddComponent<AudioSource>();breath.playOnAwake=false;
                breath.loop=true;breath.spatialBlend=0;breath.volume=0;
            }
        }

        void LateUpdate()
        {
            if(!breath || !session) return;
            var reporter=session.Reporter;
            var clip=Profile?Profile.breathingLoop:null;
            if(breath.clip!=clip) { breath.Stop();breath.clip=clip;breath.volume=0; }
            bool moving=clip && reporter && reporter.IsHuman && reporter.MovedLocallyRecently &&
                !reporter.Hidden && !session.Finished && !session.DebugOpen && session.InputCaptured;
            float target=moving?Profile.breathingVolume*(reporter.Sneaking?Profile.quietBreathingMultiplier:1):0;
            float fade=Profile?Mathf.Max(.01f,Profile.breathingFadeSeconds):.15f;
            breath.volume=Mathf.MoveTowards(breath.volume,target,Time.unscaledDeltaTime/fade);
            if(moving && !breath.isPlaying) breath.Play();
            if(!moving && breath.volume<=0) breath.Stop();
        }
        AudioClip Create(string name,float length,float frequency,float noise)
        {
            const int rate=22050;
            float[] samples=new float[(int)(rate*length)];
            var random=new System.Random(17);
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;
                float envelope=Mathf.Min(1,t/.008f)*Mathf.Pow(1-t/length,2);
                samples[i]=envelope*.45f*(Mathf.Sin(2*Mathf.PI*frequency*t)*(1-noise)+((float)random.NextDouble()*2-1)*noise);
            }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0);
            generated.Add(clip);return clip;
        }

        AudioClip Pick(AudioClip[] clips,NoiseKind kind)
        {
            if(clips==null || clips.Length==0) return null;
            previous.TryGetValue(kind,out int last);
            int index=clips.Length==1?0:(last+1+Random.Range(0,clips.Length-1))%clips.Length;
            for(int i=0;i<clips.Length;i++)
            {
                int candidate=(index+i)%clips.Length;
                if(clips[candidate]) { previous[kind]=candidate;return clips[candidate]; }
            }
            return null;
        }
        public void Play(Vector3 position,NoiseKind kind)
        {
            AudioClip clip=null;float volume=.7f,pitch=1;
            if(kind==NoiseKind.Chair) clip=chair;
            else if(Profile)
            {
                switch(kind)
                {
                    case NoiseKind.Footstep: clip=Pick(Profile.walkSteps,kind);volume=Profile.walkVolume;break;
                    case NoiseKind.ChaseFootstep: clip=Pick(Profile.chaseSteps,kind);volume=Profile.chaseVolume;break;
                    case NoiseKind.Door: clip=Pick(Profile.doorOpen,kind);volume=Profile.doorVolume;break;
                    case NoiseKind.DoorClose: clip=Pick(Profile.doorClose,kind);volume=Profile.doorVolume;break;
                    case NoiseKind.Shutter: clip=Pick(Profile.shutter,kind);volume=Profile.shutterVolume;break;
                    case NoiseKind.HideBed: clip=Pick(Profile.bedHide!=null && Profile.bedHide.Length>0?Profile.bedHide:Profile.doorClose,kind);volume=Profile.bedHideVolume;pitch=.75f;break;
                }
                if(kind==NoiseKind.Footstep || kind==NoiseKind.ChaseFootstep)
                    pitch+=Random.Range(-Profile.stepPitchVariation,Profile.stepPitchVariation);
            }
            PlayClip(position,clip,true,volume,pitch,kind==NoiseKind.HideBed && Profile?Profile.bedHideDistanceMultiplier:1);
        }
        public void Success() => PlayClip(transform.position,success,false,.7f,1);
        void PlayClip(Vector3 position,AudioClip clip,bool spatial,float volume,float pitch,float distanceMultiplier=1)
        {
            if(!clip || volume<=0) return;
            var go=new GameObject("Audio_"+clip.name); go.transform.SetParent(transform,false); go.transform.position=position;
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;
            source.clip=clip; source.spatialBlend=spatial?1:0;source.dopplerLevel=0;
            source.minDistance=Profile?Mathf.Max(.1f,Profile.minDistance):1.5f;
            source.maxDistance=Profile?Mathf.Max(source.minDistance,Profile.maxDistance):28;
            source.maxDistance*=Mathf.Max(1,distanceMultiplier);
            source.rolloffMode=AudioRolloffMode.Linear;source.volume=volume;source.pitch=Mathf.Clamp(pitch,.5f,2);
            source.Play();Destroy(go,clip.length/source.pitch+.1f);
        }
        void OnDestroy() { foreach(var clip in generated) PrototypeVisuals.Release(clip); }
    }
}
