using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    [Serializable] public sealed class PlayerCommand
    {
        public int sequence;
        public float x,y,lookX,lookY,aspect=1.777778f;
        public bool sneak,crouch,interact,phone,action;
        public static PlayerCommand Read(bool captured)
        {
            var c=new PlayerCommand{aspect=(float)Screen.width/Mathf.Max(1,Screen.height)};
            var k=Keyboard.current;
            if(!captured || k==null) return c;
            c.x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);
            c.y=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);
            var mouse=Mouse.current?.delta.ReadValue()??Vector2.zero;
            c.lookX=mouse.x; c.lookY=mouse.y; c.sneak=k.shiftKey.isPressed; c.crouch=k.ctrlKey.isPressed;
            c.interact=k.eKey.wasPressedThisFrame; c.phone=k.fKey.wasPressedThisFrame;
            c.action=k.spaceKey.wasPressedThisFrame || Mouse.current?.leftButton.wasPressedThisFrame==true;
            return c;
        }
        public bool Valid => Finite(x)&&Finite(y)&&Finite(lookX)&&Finite(lookY)&&Finite(aspect)&&Mathf.Abs(x)<=1&&Mathf.Abs(y)<=1&&
            Mathf.Abs(lookX)<=4000&&Mathf.Abs(lookY)<=4000&&aspect>=.3f&&aspect<=5;
        static bool Finite(float f) => !float.IsNaN(f)&&!float.IsInfinity(f);
        public void Merge(PlayerCommand c)
        {
            x=c.x;y=c.y;sneak=c.sneak;crouch=c.crouch;aspect=c.aspect;
            lookX=Mathf.Clamp(lookX+c.lookX,-4000,4000);lookY=Mathf.Clamp(lookY+c.lookY,-4000,4000);
            interact|=c.interact;phone|=c.phone;action|=c.action;
        }
        public void ConsumeEdges() { lookX=lookY=0;interact=phone=action=false; }
        public PlayerCommand Copy() => (PlayerCommand)MemberwiseClone();
    }
    [Serializable] public sealed class ActorSnapshot
    {
        public Vector3 position;
        public float yaw,pitch,vertical;
        public bool crouched,hidden,phone,sneaking;
    }
    [Serializable] public sealed class FlappySnapshot
    {
        public int level,target,score,crashes;
        public float height,velocity;
        public bool running,crashed;
        public float[] pipeX,gaps;
        public bool[] passed;
    }
    [Serializable] public sealed class DoorSnapshot { public string id; public bool open; }
    [Serializable] public sealed class NoiseSnapshot { public int id; public Vector3 position; public NoiseKind kind; public bool audibleOnly; }
    [Serializable] public sealed class MatchSnapshot
    {
        public ActorSnapshot reporter,streamer;
        public StreamerState state;
        public int broadcast,score,photos,photographedRound,messageId,inputAck;
        public float hostFps;
        public float roamRemaining,stateRemaining,progress,alertRemaining,flashRemaining,messageRemaining;
        public string result,message;
        public string[] completedQuests;
        public DoorSnapshot[] doors;
        public NoiseSnapshot[] noises;
        public FlappySnapshot flappy;
    }
    [Serializable] public sealed class LanMessage
    {
        public const int Version=2;
        public int protocol=Version;
        public string type,text,map;
        public TestRole role;
        public bool hostReady,guestReady;
        public float stamp;
        public MatchSettings settings;
        public PlayerCommand input;
        public MatchSnapshot snapshot;
    }
}
