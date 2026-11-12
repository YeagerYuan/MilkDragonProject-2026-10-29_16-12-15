using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    [Serializable]
    public sealed class DetectionRules
    {
        [Range(.1f,5)] public float rangeMultiplier=1.5f;
        [Range(.1f,30)] public float outlineSeconds=3;
        [Range(.1f,30)] public float laughCapacity=5;
        [Range(.1f,20)] public float recoverySecondsPerLaughSecond=2;
        [Range(0,100)] public int initialInvisibilityCharges=2;
        [Range(.1f,120)] public float invisibilitySeconds=10;
        [Range(0,100)] public int initialDecoyCharges=1;
        public bool Valid => initialDecoyCharges>=0 && initialDecoyCharges<=100 && rangeMultiplier>=.1f && rangeMultiplier<=5 && outlineSeconds>=.1f && outlineSeconds<=30 &&
            laughCapacity>=.1f && laughCapacity<=30 && recoverySecondsPerLaughSecond>=.1f && recoverySecondsPerLaughSecond<=20 &&
            initialInvisibilityCharges>=0 && initialInvisibilityCharges<=100 && invisibilitySeconds>=.1f && invisibilitySeconds<=120;
    }

    [DefaultExecutionOrder(200)]
    public sealed partial class PlayerAbilities : MonoBehaviour
    {
        MatchSession session;
        bool held,exhaustedUntilRelease;
        float lastLaughInput=float.NegativeInfinity,aiLaughUntil;
        AudioSource laughter;
        FloorSpatialAudio laughterSpatial;
        public float LaughEnergy { get; private set; }
        public bool Laughing { get; private set; }
        public float OutlineRemaining { get; private set; }
        public float InvisibleRemaining { get; private set; }
        public int InvisibleCharges { get; private set; }
        public bool Invisible => InvisibleRemaining>0;
        public bool Revealed => OutlineRemaining>0 && !session.Reporter.Hidden;
        public float DetectionDistance => session.Settings.streamerVision.range*Rules.rangeMultiplier;
        public DetectionRules Rules => session.Settings.detection;
        public void Initialize(MatchSession match)
        {
            session=match;LaughEnergy=Rules.laughCapacity;InvisibleCharges=Rules.initialInvisibilityCharges;DecoyCharges=Rules.initialDecoyCharges;
            laughter=session.Streamer.gameObject.AddComponent<AudioSource>();laughter.playOnAwake=false;laughter.loop=true;
            laughter.spatialBlend=1;laughter.dopplerLevel=0;laughter.rolloffMode=AudioRolloffMode.Linear;laughter.minDistance=1.5f;
            laughterSpatial=laughter.gameObject.AddComponent<FloorSpatialAudio>();
            laughterSpatial.Initialize(match,laughter,match.App.AudioProfile,match.App.AudioProfile?match.App.AudioProfile.laughVolume:.8f);
            if(match.IsStreamerPlayer)
            {
                detectionOutline=match.Streamer.View.gameObject.AddComponent<DetectionOutline>();detectionOutline.Initialize(match,()=>Revealed);
                detectionOutline.AddTarget(match.Streamer.Computer.transform,()=>match.Streamer.State==StreamerState.Returning &&
                    Mathf.Repeat(Time.time,match.Settings.returnComputerPulseInterval)<match.Settings.returnComputerPulseSeconds);
            }
        }
        public void ApplyCommand(TestRole role,PlayerCommand input)
        {
            if(!session.IsAuthority || session.Finished) return;
            if(role==TestRole.Streamer) {held=input.laugh;lastLaughInput=Time.unscaledTime;}
            else {if(input.invisible) TryInvisibility();if(input.decoy) TryPlaceDecoy();}
        }
        public bool TryInvisibility()
        {
            if(!session.IsAuthority || session.Finished || session.ReporterDying || Invisible || (!session.IsTraining && InvisibleCharges<=0) || session.Reporter.Hidden) return false;
            if(!session.IsTraining) InvisibleCharges--;InvisibleRemaining=Rules.invisibilitySeconds;return true;
        }
        public void ClearReporterEffects() {InvisibleRemaining=OutlineRemaining=0;}
        public void HearForAi(SoundPulse pulse)
        {
            if(session.IsAuthority && !session.Streamer.Controller.IsHuman && session.Streamer.State==StreamerState.Roaming &&
                pulse.Kind!=NoiseKind.Chair && LaughEnergy>=1 && Vector3.Distance(session.Streamer.transform.position,pulse.Position)<=DetectionDistance)
                aiLaughUntil=Time.time+.8f;
        }
        public bool ReporterInDetectionCone()
        {
            if(session.Reporter.Hidden || session.ReporterDying) return false;
            var eye=session.Streamer.View.transform;Vector3 delta=session.Reporter.View.transform.position-eye.position;
            return delta.magnitude<=DetectionDistance && Vector3.Angle(eye.forward,delta)<=session.Settings.streamerVision.outerAngle*.5f;
        }
        void Update()
        {
            if(!session) return;
            if(session.Finished) {Laughing=false;OutlineRemaining=InvisibleRemaining=0;UpdateSound();return;}
            if(session.SimulationPaused) {laughter.Pause();return;}
            if(session.IsAuthority)
            {
                if(!session.IsNetwork)
                {
                    bool input=session.InputCaptured && !session.App.GameplayInputBlocked && !DocumentManager.BlocksGameplayInput;
                    if(session.IsStreamerPlayer) held=input && Keyboard.current?.qKey.isPressed==true;
                    else
                    {
                        held=Time.time<aiLaughUntil;
                        if(input && Keyboard.current?.gKey.wasPressedThisFrame==true) TryInvisibility();
                        if(input && Keyboard.current?.bKey.wasPressedThisFrame==true) TryPlaceDecoy();
                    }
                }
                else if(Time.unscaledTime-lastLaughInput>.35f) held=false;
                Tick(Time.deltaTime,held);
            }
            else
            {
                InvisibleRemaining=Mathf.Max(0,InvisibleRemaining-Time.deltaTime);
                OutlineRemaining=Mathf.Max(0,OutlineRemaining-Time.deltaTime);TickDecoys(Time.deltaTime,false);
            }
            if(session.Reporter.Hidden) OutlineRemaining=0;
            UpdateSound();
        }
        public void Tick(float dt,bool laughingInput)
        {
            if(!session.IsAuthority || session.Finished || session.SimulationPaused || dt<=0) return;
            InvisibleRemaining=Mathf.Max(0,InvisibleRemaining-dt);OutlineRemaining=Mathf.Max(0,OutlineRemaining-dt);
            bool allowed=session.Streamer.State==StreamerState.Streaming || session.Streamer.State==StreamerState.Roaming || session.Streamer.State==StreamerState.Returning;
            if(!laughingInput) exhaustedUntilRelease=false;
            Laughing=laughingInput && allowed && !exhaustedUntilRelease && LaughEnergy>0;
            if(Laughing)
            {
                LaughEnergy=Mathf.Max(0,LaughEnergy-dt);
                if(ReporterInDetectionCone()) OutlineRemaining=Rules.outlineSeconds;
                if(LaughEnergy<=0) {exhaustedUntilRelease=true;Laughing=false;}
            }
            else if(!laughingInput) LaughEnergy=Mathf.Min(Rules.laughCapacity,LaughEnergy+dt/Rules.recoverySecondsPerLaughSecond);
            TickDecoys(dt,Laughing);
            if(session.Reporter.Hidden) OutlineRemaining=0;
        }
        public void ApplySnapshot(MatchSnapshot state)
        {
            if(session.IsAuthority) return;
            LaughEnergy=Mathf.Clamp(state.laughEnergy,0,Rules.laughCapacity);Laughing=state.laughing;
            InvisibleRemaining=Mathf.Max(0,state.invisibleRemaining);InvisibleCharges=Mathf.Max(0,state.invisibleCharges);
            OutlineRemaining=Mathf.Max(0,state.outlineRemaining);ApplyDecoys(state);
        }
        void UpdateSound()
        {
            var profile=session.App.AudioProfile;var clip=profile?profile.streamerLaugh:null;
            if(laughter.clip!=clip) {laughter.Stop();laughter.clip=clip;}
            laughter.maxDistance=Mathf.Max(DetectionDistance,profile?profile.maxDistance:28);
            if(Laughing && clip)
            {
                laughterSpatial.BaseVolume=profile.laughVolume;
                laughter.UnPause();if(!laughter.isPlaying) laughter.Play();
            }
            else if(laughter.isPlaying) laughter.Stop();
        }
        void OnDisable() {if(laughter) laughter.Stop();}
    }
}
