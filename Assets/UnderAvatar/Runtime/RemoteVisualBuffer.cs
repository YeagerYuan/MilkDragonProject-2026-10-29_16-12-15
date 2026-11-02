using System.Collections.Generic;
using UnityEngine;

namespace UnderAvatar
{
    // Presentation only. Never moves a CharacterController or participates in capture/photo validation.
    public sealed class RemoteVisualBuffer
    {
        struct Sample { public float time;public ActorSnapshot pose; }
        readonly List<Sample> samples=new List<Sample>();
        float cursor,lastArrival;
        bool initialized;
        public bool JustSnapped { get; private set; }
        public void Add(ActorSnapshot pose,float serverTime,float arrival,float delay)
        {
            if(pose==null) return;
            if(samples.Count>0 && serverTime<=samples[samples.Count-1].time) return;
            bool reset=!initialized || samples.Count==0 || pose.hidden!=samples[samples.Count-1].pose.hidden ||
                Vector3.Distance(pose.position,samples[samples.Count-1].pose.position)>3;
            if(reset) { samples.Clear();cursor=serverTime-delay;initialized=true;JustSnapped=true; }
            samples.Add(new Sample{time=serverTime,pose=pose});lastArrival=arrival;
            if(samples.Count>64) samples.RemoveAt(0);
        }
        public ActorSnapshot Evaluate(float dt,float now,float delay,float extrapolation,float staleAfter)
        {
            if(samples.Count==0) return null;
            var latest=samples[samples.Count-1];
            float target=latest.time-delay;
            // Adjust playback gradually instead of restarting the timeline whenever TCP delivers a burst.
            float drift=target-cursor;
            cursor+=Mathf.Max(0,dt)*(drift>.025f?1.1f:drift<-.025f?.9f:1);
            if(Mathf.Abs(drift)>1) { cursor=target;JustSnapped=true; }
            float time=Mathf.Min(cursor,latest.time+Mathf.Max(0,extrapolation));
            ActorSnapshot result;
            if(time<=samples[0].time) result=Copy(samples[0].pose);
            else
            {
                int index=1;while(index<samples.Count && samples[index].time<time) index++;
                if(index<samples.Count)
                {
                    var a=samples[index-1];var b=samples[index];float t=Mathf.InverseLerp(a.time,b.time,time);
                    result=Copy(t<.5f?a.pose:b.pose);
                    result.position=Vector3.Lerp(a.pose.position,b.pose.position,t);
                    result.yaw=Mathf.LerpAngle(a.pose.yaw,b.pose.yaw,t);
                    result.planarVelocity=Vector3.Lerp(a.pose.planarVelocity,b.pose.planarVelocity,t);
                }
                else { result=Copy(latest.pose);result.position+=latest.pose.planarVelocity*Mathf.Clamp(time-latest.time,0,extrapolation); }
            }
            if(now-lastArrival>staleAfter) result.planarVelocity=Vector3.zero;
            return result;
        }
        public void ConsumeSnap() { JustSnapped=false; }
        static ActorSnapshot Copy(ActorSnapshot p) => new ActorSnapshot{position=p.position,yaw=p.yaw,planarVelocity=p.planarVelocity,crouched=p.crouched,sneaking=p.sneaking,hidden=p.hidden};
    }
    public sealed class CharacterMotion
    {
        Vector3 velocity;
        float lastMoved=float.NegativeInfinity;
        public Vector3 Velocity => Time.unscaledTime-lastMoved<=.12f?velocity:Vector3.zero;
        public void Record(Vector3 displacement,float dt,bool replay=false)
        {
            if(replay) return;
            displacement.y=0;
            velocity=dt>.00001f?Vector3.ClampMagnitude(displacement/dt,12):Vector3.zero;lastMoved=Time.unscaledTime;
        }
        public void Reset() { velocity=Vector3.zero;lastMoved=float.NegativeInfinity; }
    }
}
