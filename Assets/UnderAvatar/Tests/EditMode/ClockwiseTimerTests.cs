using NUnit.Framework;
namespace UnderAvatar.Tests
{
    public sealed class ClockwiseTimerTests
    {
        [TestCase(0,1,0)] [TestCase(1,0,.25f)] [TestCase(0,-1,.5f)] [TestCase(-1,0,.75f)]
        public void RingSweepStartsAtTopAndMovesClockwise(float x,float y,float expected)
        {Assert.That(BroadcastOverlay.ClockwisePhase(x,y),Is.EqualTo(expected).Within(.0001f));}
        [Test] public void FractionUsesConfiguredDurationAndClampsExpiredTime()
        {
            Assert.That(BroadcastOverlay.RemainingFraction(15,60),Is.EqualTo(.25f));
            Assert.That(BroadcastOverlay.RemainingFraction(-1,45),Is.Zero);
            Assert.That(BroadcastOverlay.RemainingFraction(99,45),Is.EqualTo(1));
            Assert.That(BroadcastOverlay.RemainingFraction(10,0),Is.Zero);
        }
    }
}
