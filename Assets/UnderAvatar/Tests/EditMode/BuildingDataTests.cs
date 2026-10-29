using System;
using System.Linq;
using NUnit.Framework;

namespace UnderAvatar.Tests
{
    public sealed class BuildingDataTests
    {
        [Test]
        public void DefaultMapRoundTripPreservesAuthoredItemsAndQuests()
        {
            var original=BuildingData.CreateDefault();
            var loaded=BuildingData.FromJson(original.ToJson());
            Assert.That(loaded.floors.Count,Is.EqualTo(2));
            var before=original.floors.SelectMany(f=>f.items).ToArray();
            var after=loaded.floors.SelectMany(f=>f.items).ToArray();
            Assert.That(after.Length,Is.EqualTo(before.Length));
            Assert.That(after.Count(i=>i.type==ItemType.QuestItem),Is.EqualTo(3));
            Assert.That(after.Count(i=>i.type==ItemType.StreamerPC),Is.EqualTo(1));
            Assert.That(after.Count(i=>i.type==ItemType.Door),Is.GreaterThanOrEqualTo(3));
            for(int i=0;i<before.Length;i++)
            {
                Assert.That(after[i].id,Is.EqualTo(before[i].id));
                Assert.That(after[i].type,Is.EqualTo(before[i].type));
                Assert.That(after[i].rotationY,Is.EqualTo(before[i].rotationY));
                Assert.That(after[i].questId,Is.EqualTo(before[i].questId));
                Assert.That(after[i].questDescription,Is.EqualTo(before[i].questDescription));
            }
        }

        [Test]
        public void RemovingMiddleFloorRenumbersUpperFloorWithoutLosingItsContents()
        {
            var data=BuildingData.CreateDefault();
            data.floors.Add(new FloorData{floorIndex=2});
            var topItem=new PlacedItemData{type=ItemType.Wall,gridX=2,gridZ=3};
            data.floors[2].items.Add(topItem);
            data.RemoveFloor(1);
            data.Validate();
            Assert.That(data.floors[1].floorIndex,Is.EqualTo(1));
            Assert.That(data.floors[1].items.Single().id,Is.EqualTo(topItem.id));
            data.RemoveFloor(1);
            Assert.Throws<InvalidOperationException>(()=>data.RemoveFloor(0));
        }

        [Test]
        public void InvalidImportedMapCannotSilentlyClobberOrAliasTasks()
        {
            var data=BuildingData.CreateDefault();
            var quests=data.floors.SelectMany(f=>f.items).Where(i=>i.type==ItemType.QuestItem).ToArray();
            quests[1].questId=quests[0].questId;
            Assert.Throws<ArgumentException>(()=>BuildingData.FromJson(data.ToJson()));
            Assert.Throws<ArgumentException>(()=>BuildingData.FromJson("{}"));
            Assert.Throws<ArgumentException>(()=>BuildingData.FromJson("not JSON"));
        }

        [Test]
        public void InvalidPlacementAndNonContiguousFloorsAreRejected()
        {
            var data=BuildingData.CreateDefault();
            data.floors[0].items[0].rotationY=45;
            Assert.Throws<ArgumentException>(()=>data.Validate());
            data.floors[0].items[0].rotationY=0;
            data.floors[1].floorIndex=5;
            Assert.Throws<ArgumentException>(()=>data.Validate());
        }
    }
}
