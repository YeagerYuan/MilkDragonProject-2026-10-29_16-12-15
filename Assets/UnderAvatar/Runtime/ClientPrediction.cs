using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderAvatar
{
    // Predict only the local actor. Confirmed host poses rewind and replay unacknowledged input ticks.
    public sealed class ClientPrediction
    {
        public const float Step=1f/60;
        public MatchSession Match { get; }
        public int PendingCount => history.Count;
        public int LastSequence { get; private set; }
        public float LastCorrection { get; private set; }
        public const int MaxReplayTicks=90;
        public int LastReplayCount { get; private set; }
        public bool CorrectionDeferred { get; private set; }
        readonly List<PlayerCommand> history=new List<PlayerCommand>();
        readonly PlayerCommand accumulated=new PlayerCommand();
        float remainder;
        Vector3 cameraOffset;
        bool initialized;
        int lastAck;
        ActorSnapshot lastHostPose;
        Transform Owner => Match.IsStreamerPlayer?Match.Streamer.transform:Match.Reporter.transform;
        Camera View => Match.IsStreamerPlayer?Match.Streamer.View:Match.Reporter.View;
        public ClientPrediction(MatchSession match) { Match=match; }
        void Look(Vector2 delta)
        {
            if(Match.IsStreamerPlayer) Match.Streamer.Controller.LookInput(delta);
            else Match.Reporter.PredictLook(delta);
        }
        void Simulate(PlayerCommand command,bool replay)
        {
            if(Match.IsStreamerPlayer) Match.Streamer.Controller.PredictCommand(command,Step,replay);
            else Match.Reporter.PredictCommand(command,Step,replay);
        }
        public void Tick(PlayerCommand input,float dt,Action<PlayerCommand> send)
        {
            if(!initialized || Match.Finished) return;
            input.reporterLifeEpoch=Match.ReporterLifeEpoch;
            accumulated.Merge(input);
            // Mouse response uses render cadence, independent of snapshots or the 60 Hz input tick.
            Look(new Vector2(input.lookX,input.lookY));
            remainder+=Mathf.Clamp(dt,0,.1f);
            while(remainder>=Step)
            {
                remainder-=Step;
                var command=accumulated.Copy();command.sequence=++LastSequence;
                accumulated.ConsumeEdges();
                Simulate(command,false);history.Add(command);send(command);
                // Bound memory without stopping local input during a brief network outage.
                if(history.Count>1800) history.RemoveAt(0);
            }
            cameraOffset*=Mathf.Exp(-18*Mathf.Max(0,dt));
            SetCameraOffset();
        }
        public void Receive(MatchSnapshot snapshot)
        {
            if(snapshot==null || initialized && snapshot.inputAck<lastAck) return;
            Vector3 before=Owner.position;
            bool phoneMenu=Match.PhoneMenuOpen;
            bool hidden=Match.Reporter.Hidden;int lifeEpoch=Match.ReporterLifeEpoch;
            var beforePose=Match.IsStreamerPlayer?Match.Streamer.Controller.CapturePose():Match.Reporter.CapturePose();
            var flappy=Match.IsStreamerPlayer?Match.Streamer.Controller.Level.Capture():null;
            var state=Match.Streamer.State;int broadcast=Match.Streamer.Controller.BroadcastNumber;
            var hostPose=Match.IsStreamerPlayer?snapshot.streamer:snapshot.reporter;
            bool hostUnchanged=lastHostPose!=null && Vector3.SqrMagnitude(hostPose.position-lastHostPose.position)<.000001f;
            Match.ApplySnapshot(snapshot);
            history.RemoveAll(input=>input.sequence<=snapshot.inputAck);
            bool sameMode=initialized && (Match.IsStreamerPlayer || lifeEpoch==Match.ReporterLifeEpoch) && hidden==Match.Reporter.Hidden && (!Match.IsStreamerPlayer ||
                state==Match.Streamer.State && broadcast==Match.Streamer.Controller.BroadcastNumber);
            CorrectionDeferred=sameMode && (history.Count>MaxReplayTicks || snapshot.inputAck==lastAck && hostUnchanged);
            LastReplayCount=0;
            if(CorrectionDeferred)
            {
                if(Match.IsStreamerPlayer)
                { Match.Streamer.Controller.RestorePredictedPose(beforePose);Match.Streamer.Controller.Level.Apply(flappy); }
                else Match.Reporter.ApplyPose(beforePose);
            }
            else
            {
                if(initialized && !sameMode)
                {
                    history.Clear();accumulated.ConsumeEdges();accumulated.x=accumulated.y=0;
                    remainder=0;cameraOffset=Vector3.zero;
                }
                foreach(var input in history) { Simulate(input,true);LastReplayCount++; }
                Look(new Vector2(accumulated.lookX,accumulated.lookY));
            }
            if(sameMode)
            {
                if(Match.IsStreamerPlayer) Match.Streamer.Controller.RestorePredictedLook(beforePose);
                else Match.Reporter.RestorePredictedLook(beforePose);
            }
            if(!Match.IsStreamerPlayer)
                Match.SetPhoneMenu(Match.Reporter.PhoneOpen && !Match.ReporterDying && (Match.PendingPhoto!=null || sameMode && phoneMenu));
            lastAck=snapshot.inputAck;lastHostPose=hostPose;
            LastCorrection=Vector3.Distance(before,Owner.position);
            if(sameMode && LastCorrection<.8f)
                cameraOffset=Vector3.ClampMagnitude(cameraOffset+before-Owner.position,.4f);
            else cameraOffset=Vector3.zero;
            initialized=true;SetCameraOffset();
        }
        void SetCameraOffset()
        {
            if(!Match.IsStreamerPlayer && Match.ReporterDying) return;
            float height=Match.IsStreamerPlayer?(Match.Streamer.Controller.Crouched?.88f:1.7f):(Match.Reporter.Crouched?.88f:1.65f);
            View.transform.localPosition=Vector3.up*height+Owner.InverseTransformVector(cameraOffset);
        }
    }
}
