using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class NarrativeContentTests
    {
        private static CityLayout City()=>CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
        private static GameSession Session()=>new GameSession(City(),7,Money.FromYuan(10000),GameTimestamp.From(new GameDate(0),9*60));
        [Test] public void PopulationHasStableCompleteDistinctProfiles()
        {
            var people=CityLifeCatalog.People.Where(p=>p.Id!=CityLifeCatalog.InvitationTestPersonId).ToArray();
            Assert.AreEqual(124,people.Length);Assert.AreEqual(124,people.Select(p=>p.Id).Distinct().Count());
            Assert.AreEqual(124,people.Select(p=>p.Name).Distinct().Count());Assert.AreEqual(20,people.Count(p=>p.KeyCharacter));
            Assert.AreEqual(372,people.SelectMany(p=>p.Stories).Distinct().Count(),"No shared filler biography paragraphs");
            var city=City();
            foreach(var p in people)
            {
                Assert.AreEqual(3,p.Stories?.Distinct().Count(),p.Id+" three unique histories");
                Assert.GreaterOrEqual(p.Topics?.Length??0,4,p.Id+" topics");
                Assert.True(p.Stories.All(s=>!string.IsNullOrWhiteSpace(s)),p.Id);
                foreach(var field in new[]{p.Role,p.Interest,p.Goal,p.Experience,p.Circle,p.StoryTitle,p.FirstEncounter})Assert.IsNotEmpty(field,p.Id);
                Assert.True(city.HasPlace(p.HomeId),p.Id+" home "+p.HomeId);
                if(p.WorkId!=null)Assert.True(city.HasPlace(p.WorkId),p.Id+" work "+p.WorkId);
                Assert.IsNotEmpty(p.LeisurePlaces,p.Id);foreach(var place in p.LeisurePlaces)Assert.True(city.HasPlace(place),p.Id+" leisure "+place);
                Assert.That(p.WorkStart,Is.InRange(0,1439));Assert.That(p.WorkEnd,Is.InRange(0,1439));
            }
        }
        [Test] public void RelationsHaveValidEndpointsAndNoIsolatedProductionPerson()
        {
            var edges=CityLifeCatalog.Relations.ToArray();
            foreach(var e in edges){Assert.NotNull(CityLifeCatalog.Person(e.From),e.From);Assert.NotNull(CityLifeCatalog.Person(e.To),e.To);Assert.AreNotEqual(e.From,e.To);Assert.IsNotEmpty(e.Kind);}
            foreach(var p in CityLifeCatalog.People.Where(p=>p.Id!=CityLifeCatalog.InvitationTestPersonId))Assert.True(edges.Any(e=>e.From==p.Id),"Isolated: "+p.Id);
            Assert.True(edges.Any(e=>e.From==CityLifeCatalog.ShenXiao&&e.To=="life_enterprise"&&e.Kind=="父亲"));
            Assert.True(edges.Any(e=>e.From=="life_enterprise"&&e.To==CityLifeCatalog.ShenXiao&&e.Kind=="自己的孩子"));
        }
        [Test] public void EveryStoryNodeHasValidPeoplePlacesAndEarlierDependencies()
        {
            var nodes=DistrictNarrativeCatalog.Nodes;var city=City();var seen=new System.Collections.Generic.HashSet<string>();
            foreach(var n in nodes)
            {
                foreach(var dependency in n.Prerequisites)Assert.Contains(dependency,seen.ToArray(),n.Id);
                Assert.True(seen.Add(n.Id),n.Id);Assert.True(city.HasPlace(n.PlaceId),n.PlaceId);
                foreach(var id in n.People)Assert.NotNull(CityLifeCatalog.Person(id),id);
                foreach(var value in new[]{n.Title,n.Module,n.Entry,n.Objective,n.Dialogue,n.Evidence,n.Result,n.Retry})Assert.IsNotEmpty(value,n.Id);
                Assert.AreEqual(n.Choices.Length,n.Choices.Select(c=>c.Id).Distinct().Count());
            }
            Assert.AreEqual(20,DistrictNarrativeCatalog.SideStories.Count);
            CollectionAssert.AreEquivalent(CityLifeCatalog.People.Where(p=>p.KeyCharacter).Select(p=>p.Id),DistrictNarrativeCatalog.SideStories.Select(a=>a.PersonId));
            foreach(var a in DistrictNarrativeCatalog.SideStories){Assert.NotNull(CityLifeCatalog.Person(a.PersonId));foreach(var id in a.RelatedPeople)Assert.NotNull(CityLifeCatalog.Person(id),id);}
            var plan=DistrictNarrativeCatalog.CreatePlan();foreach(var n in nodes)Assert.True(plan.Any(c=>c.Modules.Contains(n.Module)),n.Module);
        }

        [Test] public void MainlineHasExactlyOneCurrentRoutePerDistrict()
        {
            var plan=DistrictNarrativeCatalog.CreatePlan();
            Assert.AreEqual(3,plan.Length);
            CollectionAssert.AreEqual(new[]{"community_entry","brand_exhibition","enterprise_tender"},plan.Select(c=>c.Modules.Single()));
            Assert.AreEqual(16,DistrictNarrativeCatalog.Nodes.Count);
            Assert.AreEqual(5,DistrictNarrativeCatalog.Nodes.Count(n=>n.Module=="community_entry"));
            Assert.AreEqual(5,DistrictNarrativeCatalog.Nodes.Count(n=>n.Module=="brand_exhibition"));
            Assert.AreEqual(6,DistrictNarrativeCatalog.Nodes.Count(n=>n.Module=="enterprise_tender"));
            Assert.True(plan[1].Gates.Any(g=>g.Fact==NarrativeFact.OwnBrandFormed));
            Assert.AreEqual(NarrativeFact.ClosedSectorMonthlyRevenue,plan[2].Gates.Single().Fact);
        }
        [Test] public void NewPeopleHaveRealSessionsAndAuthoredStoriesSurviveSaveLoad()
        {
            using(var s=Session())
            {
                foreach(var d in CityLifeCatalog.People.Where(p=>p.Id!=CityLifeCatalog.InvitationTestPersonId))Assert.NotNull(s.CityPersonState(d.Id),d.Id);
                Assert.IsNull(s.CityPersonState(CityLifeCatalog.InvitationTestPersonId));
                const string id="resident_yuanan";var p=s.CityPersonState(id);p.Route=null;p.LocationPlaceId=s.CurrentPlace.Id;p.X=s.PlayerPosition.X;p.Y=s.PlayerPosition.Y;
                s.Meet(id,30);var def=s.CityPerson(id);
                StringAssert.Contains(def.Topics[0],s.CityTalk(id,"work"));
                Assert.AreEqual(def.Stories[0],s.CityHearStory(id));Assert.AreEqual(1,p.StoryChapter);
                Assert.AreNotEqual(def.Stories[1],s.CityHearStory(id),"one new chapter per day");
                var saved=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using(var loaded=GameSession.Load(s.City,saved))
                {Assert.AreEqual(1,loaded.CityPersonState(id).StoryChapter);Assert.AreEqual(p.LastStoryDay,loaded.CityPersonState(id).LastStoryDay);Assert.AreEqual(p.HomePlaceId,loaded.CityPersonState(id).HomePlaceId);Assert.AreEqual(def.Stories[0],loaded.CityPerson(id).Stories[0]);}
            }
        }
        [Test] public void PreviewDoesNotRevealStoriesOrMoveSocialState()
        {
            using(var s=Session())
            {
                const string id="resident_yuanan";var p=s.CityPersonState(id);int relation=s.Relation(id);
                var catalogue=CityLifeCatalog.People.ToArray();var nodes=DistrictNarrativeCatalog.Nodes.ToArray();var ties=CityLifeCatalog.Relations.ToArray();
                Assert.AreEqual(0,p.StoryChapter);Assert.AreEqual(relation,s.Relation(id));Assert.False(s.IsMet(id));Assert.IsEmpty(p.KnownRelations);
                Assert.IsNotEmpty(catalogue);Assert.IsNotEmpty(nodes);Assert.IsNotEmpty(ties);
            }
        }
        [Test] public void OldSaveAddsMissingPeopleWithoutResettingExistingIdentity()
        {
            using(var s=Session())
            {
                var person=s.CityPersonState("life_creator");person.StoryChapter=2;person.KnownRelations=new[]{"life_maker"};
                var saved=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                saved.cityPeople=saved.cityPeople.Where(p=>!p.PersonId.StartsWith("resident_")&&!p.PersonId.StartsWith("story_")).ToArray();
                using(var loaded=GameSession.Load(s.City,saved))
                {
                    Assert.AreEqual(2,loaded.CityPersonState("life_creator").StoryChapter);
                    CollectionAssert.Contains(loaded.CityPersonState("life_creator").KnownRelations,"life_maker");
                    Assert.AreEqual(person.HomePlaceId,loaded.CityPersonState("life_creator").HomePlaceId);
                    Assert.NotNull(loaded.CityPersonState(CityLifeCatalog.TangWei));Assert.NotNull(loaded.CityPersonState("resident_yuanan"));
                    Assert.AreEqual(loaded.CityPeople.Count,loaded.CityPeople.Select(p=>p.PersonId).Distinct().Count());
                }
            }
        }
        [Test] public void HearingNewBiographyDoesNotRevealUntoldLegacyStaffAnecdotes()
        {
            using(var s=Session())
            {
                const string id="keeper_agent";s.Meet(id,30);var p=s.CityPersonState(id);
                p.Route=null;p.LocationPlaceId=s.CurrentPlace.Id;p.X=s.PlayerPosition.X;p.Y=s.PlayerPosition.Y;
                Assert.AreEqual(s.CityPerson(id).Stories[0],s.CityHearStory(id));
                Assert.AreEqual(0,s.StaffStoriesHeard(id));
                CollectionAssert.AreEqual(new[]{s.CityPerson(id).Stories[0]},s.LearnedPersonStories(id));
            }
        }
    }
}
