using System.Collections.Generic;
using UnityEngine;

namespace UnderAvatar
{
    public sealed class PrototypeAudio : MonoBehaviour
    {
        readonly Dictionary<NoiseKind,AudioClip> clips=new Dictionary<NoiseKind,AudioClip>();
        AudioClip success;
        void Awake()
        {
            clips[NoiseKind.Footstep]=Create("Footstep",.16f,100,.65f);
            clips[NoiseKind.Door]=Create("Door",.35f,170,.3f);
            clips[NoiseKind.Shutter]=Create("Shutter",.18f,1400,.9f);
            clips[NoiseKind.Chair]=Create("ChairTurn",.6f,260,.55f);
            success=Create("Upload",.18f,880,0);
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
            var clip=AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0); return clip;
        }
        public void Play(Vector3 position,NoiseKind kind) => PlayClip(position,clips[kind],true);
        public void Success() => PlayClip(transform.position,success,false);
        void PlayClip(Vector3 position,AudioClip clip,bool spatial)
        {
            var go=new GameObject("Audio_"+clip.name); go.transform.SetParent(transform,false); go.transform.position=position;
            var source=go.AddComponent<AudioSource>(); source.clip=clip; source.spatialBlend=spatial?1:0;
            source.minDistance=1.5f; source.maxDistance=35; source.rolloffMode=AudioRolloffMode.Linear; source.volume=.7f; source.Play();
            Destroy(go,clip.length+.05f);
        }
        void OnDestroy() { foreach(var clip in clips.Values) PrototypeVisuals.Release(clip); PrototypeVisuals.Release(success); }
    }
}
