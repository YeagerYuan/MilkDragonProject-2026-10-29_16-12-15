using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderAvatar
{
    // Human and AI use the same flap input and collision simulation. Time alone cannot win.
    public sealed class MinigameLevel
    {
        public sealed class Pipe
        {
            public float X { get; internal set; }
            public float GapCenter { get; internal set; }
            internal bool scored;
        }
        public const float BirdX=.25f, BirdRadius=.024f, PipeWidth=.085f, GapSize=.34f;
        const float Gravity=-1.8f, Impulse=.56f, ScrollSpeed=.24f, PipeSpacing=.43f;
        readonly System.Random random;
        readonly List<Pipe> pipes=new List<Pipe>();
        public IReadOnlyList<Pipe> Pipes => pipes;
        public int LevelNumber { get; private set; }
        public int TargetScore { get; private set; }
        public int Score { get; private set; }
        public int Crashes { get; private set; }
        public float BirdHeight { get; private set; }
        public float Velocity { get; private set; }
        public bool Running { get; private set; }
        public bool Crashed { get; private set; }
        public bool Complete => TargetScore>0 && Score>=TargetScore;
        public float Progress => TargetScore>0?(float)Score/TargetScore:0;
        public float NextGapHeight
        {
            get
            {
                foreach(var pipe in pipes) if(pipe.X+PipeWidth>=BirdX-BirdRadius) return pipe.GapCenter;
                return .5f;
            }
        }
        public bool WantsAutoFlap => !Running || Velocity<=0 && BirdHeight<NextGapHeight-.035f;
        public MinigameLevel(int seed) { random=new System.Random(seed); }
        public void Start(int level,int targetScore)
        {
            LevelNumber=level; TargetScore=Math.Max(1,targetScore); Crashes=0; Reset();
        }
        public void Reset()
        {
            Score=0; BirdHeight=.5f; Velocity=0; Running=false; Crashed=false; pipes.Clear();
            float center=.5f;
            for(int i=0;i<4;i++)
            {
                center=NextCenter(center);
                pipes.Add(new Pipe{X=1.05f+i*PipeSpacing,GapCenter=center});
            }
        }
        float NextCenter(float previous) => Mathf.Clamp(previous+((float)random.NextDouble()-.5f)*.20f,.32f,.68f);
        public void Flap()
        {
            if(Complete) return;
            if(Crashed) Reset();
            Running=true; Velocity=Impulse;
        }
        public void Tick(float deltaTime)
        {
            // Bounded substeps prevent tunnelling through pipes during a slow render frame.
            float remaining=Mathf.Clamp(deltaTime,0,.25f);
            while(remaining>0 && Running && !Complete)
            {
                float dt=Mathf.Min(remaining,1f/120); remaining-=dt;
                Velocity+=Gravity*dt; BirdHeight+=Velocity*dt;
                foreach(var pipe in pipes) pipe.X-=ScrollSpeed*dt;
                if(BirdHeight-BirdRadius<=0 || BirdHeight+BirdRadius>=1) { Crash(); break; }
                foreach(var pipe in pipes)
                {
                    bool overlaps=pipe.X<BirdX+BirdRadius && pipe.X+PipeWidth>BirdX-BirdRadius;
                    if(overlaps && (BirdHeight-BirdRadius<pipe.GapCenter-GapSize*.5f || BirdHeight+BirdRadius>pipe.GapCenter+GapSize*.5f))
                    { Crash(); break; }
                    if(!pipe.scored && pipe.X+PipeWidth<BirdX-BirdRadius)
                    {
                        pipe.scored=true; Score++;
                        if(Complete) { Running=false; break; }
                    }
                }
                if(pipes[0].X+PipeWidth<0)
                {
                    var last=pipes[pipes.Count-1];
                    pipes.RemoveAt(0); pipes.Add(new Pipe{X=last.X+PipeSpacing,GapCenter=NextCenter(last.GapCenter)});
                }
            }
        }
        void Crash() { Score=0; Velocity=0; Running=false; Crashed=true; Crashes++; }
        public FlappySnapshot Capture() => new FlappySnapshot
        {
            level=LevelNumber,target=TargetScore,score=Score,crashes=Crashes,height=BirdHeight,velocity=Velocity,running=Running,crashed=Crashed,
            pipeX=pipes.ConvertAll(p=>p.X).ToArray(),gaps=pipes.ConvertAll(p=>p.GapCenter).ToArray(),passed=pipes.ConvertAll(p=>p.scored).ToArray()
        };
        public void Apply(FlappySnapshot state)
        {
            if(state==null) return;
            LevelNumber=state.level;TargetScore=state.target;Score=state.score;Crashes=state.crashes;
            BirdHeight=state.height;Velocity=state.velocity;Running=state.running;Crashed=state.crashed;
            pipes.Clear();
            for(int i=0;i<state.pipeX.Length;i++) pipes.Add(new Pipe{X=state.pipeX[i],GapCenter=state.gaps[i],scored=state.passed[i]});
        }
    }
}
