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
        readonly List<PlayerCommand> history=new List<PlayerCommand>();
        readonly PlayerCommand accumulated=new PlayerCommand();
        float remainder;
        Vector3 cameraOffset;
        bool initialized;
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
                if(history.Count>300) throw new InvalidOperationException("房主长时间未确认输入，请重新连接。");
            }
            cameraOffset*=Mathf.Exp(-18*Mathf.Max(0,dt));
            SetCameraOffset();
        }
        public void Receive(MatchSnapshot snapshot)
        {
            Vector3 before=Owner.position;
            bool hidden=Match.Reporter.Hidden;
            Match.ApplySnapshot(snapshot);
            history.RemoveAll(input=>input.sequence<=snapshot.inputAck);
            foreach(var input in history) Simulate(input,true);
            Look(new Vector2(accumulated.lookX,accumulated.lookY));
            LastCorrection=Vector3.Distance(before,Owner.position);
            if(initialized && hidden==Match.Reporter.Hidden && LastCorrection<.8f)
                cameraOffset+=before-Owner.position;
            else cameraOffset=Vector3.zero;
            initialized=true;SetCameraOffset();
        }
        void SetCameraOffset()
        {
            float height=Match.IsStreamerPlayer?(Match.Streamer.Controller.Crouched?.88f:1.7f):(Match.Reporter.Crouched?.88f:1.65f);
            View.transform.localPosition=Vector3.up*height+Owner.InverseTransformVector(cameraOffset);
        }
    }
}
