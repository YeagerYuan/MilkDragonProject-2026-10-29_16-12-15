using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace UnderAvatar.Tests
{
    public sealed class GameplayDataTests
    {
        [Test]
        public void FlappyStaysLockedWithoutFlapsAndCrashesResetScore()
        {
            var level=new MinigameLevel(123); level.Start(1,15);
            Assert.That(level.Complete,Is.False);
            for(int i=0;i<3600;i++) level.Tick(1f/60);
            Assert.That(level.Score,Is.Zero); Assert.That(level.Running,Is.False);
            for(int i=0;i<3600 && level.Score==0;i++)
            { if(level.WantsAutoFlap) level.Flap(); level.Tick(1f/60); }
            Assert.That(level.Score,Is.EqualTo(1));
            for(int i=0;i<600 && !level.Crashed;i++) level.Tick(1f/60);
            Assert.That(level.Crashed,Is.True); Assert.That(level.Score,Is.Zero); Assert.That(level.Complete,Is.False);
            level.Flap(); Assert.That(level.Running,Is.True); Assert.That(level.Crashed,Is.False);
            Assert.That(level.TargetScore,Is.EqualTo(15)); Assert.That(level.Score,Is.Zero);
        }

        [TestCase(1)] [TestCase(42)] [TestCase(123)]
        public void FlappyCanReachFifteenOnlyByPassingPipes(int seed)
        {
            var level=new MinigameLevel(seed); level.Start(2,15);
            int previous=0;
            for(int i=0;i<7200 && !level.Complete;i++)
            {
                if(level.WantsAutoFlap) level.Flap();
                level.Tick(1f/60);
                Assert.That(level.Score,Is.LessThanOrEqualTo(previous+1));
                previous=level.Score;
                if(level.Score<15) Assert.That(level.Complete,Is.False);
            }
            Assert.That(level.Complete,Is.True,"Flap-driven simulation did not finish.");
            Assert.That(level.Score,Is.EqualTo(15)); Assert.That(level.Progress,Is.EqualTo(1));
            level.Tick(10); level.Flap(); Assert.That(level.Score,Is.EqualTo(15));
        }

        [Test]
        public void NavigationCrossesFloorsOnlyThroughPhysicalStairEntrances()
        {
            var data=BuildingData.CreateDefault(); var navigation=new BuildingNavigation(data);
            var path=navigation.FindPath(new Vector3(-8,0,-4),new Vector3(8,4,6));
            Assert.That(path,Is.Not.Null);
            Assert.That(path.Last().y,Is.EqualTo(1));
            bool crossed=false;
            for(int i=1;i<path.Count;i++)
            {
                if(path[i].y==path[i-1].y) continue;
                crossed=true;
                var staircase=navigation.StairBetween(path[i-1],path[i]);
                Assert.That(staircase,Is.Not.Null);
                Assert.That(staircase.Waypoints(true).Any(p=>p.y>0 && p.y<data.floorHeight),Is.True);
            }
            Assert.That(crossed,Is.True);
            data.floors[0].items.RemoveAll(i=>i.type==ItemType.Stairs);
            navigation=new BuildingNavigation(data);
            Assert.That(navigation.FindPath(new Vector3(-8,0,-4),new Vector3(8,4,6)),Is.Null);
        }
    }
}
