using NUnit.Framework;
using UnityEngine;

namespace UnderAvatar.Tests
{
    public sealed class RemoteVisualTests
    {
        static ActorSnapshot Pose(float x,float speed=2,float yaw=0) => new ActorSnapshot{position=new Vector3(x,0,0),planarVelocity=Vector3.right*speed,yaw=yaw};
        [Test] public void DelayedBurstsKeepPresentationContinuousAndNeverMoveSimulation()
        {
            var b=new RemoteVisualBuffer();float previous=0,maxStep=0;int packet=0;
            for(int frame=0;frame<240;frame++)
            {
                float now=frame/60f;
                // 150ms initial delay, then six 30Hz snapshots arrive together every 200ms.
                if(frame>=9 && (frame-9)%12==0)
                    for(int n=0;n<6;n++,packet++) b.Add(Pose(packet/30f*2),packet/30f,now,.1f);
                var pose=b.Evaluate(1f/60,now,.1f,.08f,.35f);
                if(pose==null) continue;
                Assert.That(pose.position.x,Is.GreaterThanOrEqualTo(previous-.0001f));
                maxStep=Mathf.Max(maxStep,pose.position.x-previous);previous=pose.position.x;
            }
            Assert.That(maxStep,Is.LessThan(.1f),"Burst delivery must not directly jump presentation by the packet batch distance.");
            Assert.That(previous,Is.GreaterThan(6));
        }
        [Test] public void PacketLossBoundsExtrapolationThenStopsAnimation()
        {
            var b=new RemoteVisualBuffer();var original=Pose(10);b.Add(original,10,20,.1f);
            ActorSnapshot last=null;for(int i=0;i<180;i++) last=b.Evaluate(1f/60,20+i/60f,.1f,.08f,.35f);
            Assert.That(last.position.x,Is.EqualTo(10.16f).Within(.001f));Assert.That(last.planarVelocity,Is.EqualTo(Vector3.zero));
            Assert.That(original.position.x,Is.EqualTo(10),"Rendering must not modify the authoritative snapshot.");
        }
        [Test] public void TeleportsAndHidingResetButOutOfOrderPacketsCannotUndoThem()
        {
            var b=new RemoteVisualBuffer();b.Add(Pose(0),0,0,.1f);b.ConsumeSnap();
            b.Add(Pose(20),1,1,.1f);Assert.That(b.JustSnapped,Is.True);
            b.Add(Pose(-50),.5f,1.1f,.1f);Assert.That(b.Evaluate(0,1.1f,.1f,0,.35f).position.x,Is.EqualTo(20));
            b.ConsumeSnap();var hide=Pose(20);hide.hidden=true;b.Add(hide,2,2,.1f);
            Assert.That(b.JustSnapped,Is.True);Assert.That(b.Evaluate(0,2,.1f,0,.35f).hidden,Is.True);
        }
        [Test] public void YawInterpolatesAcrossZeroUsingShortArc()
        {
            var b=new RemoteVisualBuffer();b.Add(Pose(0,0,359),0,0,0);b.Add(Pose(0,0,1),.1f,.1f,0);
            var p=b.Evaluate(.05f,.1f,0,0,.35f);Assert.That(Mathf.Abs(Mathf.DeltaAngle(p.yaw,0)),Is.LessThan(.3f));
        }
    }
}
