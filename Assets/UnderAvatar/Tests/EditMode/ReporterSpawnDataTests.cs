using System;
using NUnit.Framework;
using UnityEngine;

namespace UnderAvatar.Tests
{
    public sealed class ReporterSpawnDataTests
    {
        static BuildingData Data()
        {
            var data=BuildingData.CreateDefault();data.useCustomReporterSpawn=true;
            data.reporterSpawnCell=new Vector3Int(-2,1,-2);data.reporterSpawnYaw=125;return data;
        }
        [Test] public void SpawnSurvivesSerializationAndFollowsFloorHeight()
        {
            var data=Data();data.Validate();var copy=data.Copy();
            Assert.That(copy.useCustomReporterSpawn,Is.True);Assert.That(copy.reporterSpawnCell,Is.EqualTo(data.reporterSpawnCell));
            Assert.That(copy.reporterSpawnYaw,Is.EqualTo(125));
            copy.floorHeight=5;Assert.That(copy.ReporterSpawnPosition,Is.EqualTo(new Vector3(-4,5.05f,-4)));
        }
        [Test] public void InvalidSpawnCannotBeSaved()
        {
            var data=Data();data.reporterSpawnCell=new Vector3Int(-7,0,-5);
            Assert.Throws<ArgumentException>(()=>data.Validate());
            data.reporterSpawnCell=new Vector3Int(0,4,0);Assert.Throws<ArgumentException>(()=>data.Validate());
            data.reporterSpawnCell=new Vector3Int(100,0,0);Assert.Throws<ArgumentException>(()=>data.Validate());
        }
        [Test] public void DeletingSpawnFloorFailsAndDeletingLowerFloorMovesSpawnWithItsFloor()
        {
            var data=Data();Assert.Throws<InvalidOperationException>(()=>data.RemoveFloor(1));
            Assert.That(data.reporterSpawnCell.y,Is.EqualTo(1));
            data.RemoveFloor(0);Assert.That(data.reporterSpawnCell,Is.EqualTo(new Vector3Int(-2,0,-2)));
        }
    }
}
