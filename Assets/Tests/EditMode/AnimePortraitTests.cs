using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class AnimePortraitTests
    {
        [Test] public void EveryNamedNpcLoadsItsOwnAnimeTextureWithBoundedMemorySettings()
        {
            var people = CityLifeCatalog.People.Where(p => p.Id != CityLifeCatalog.InvitationTestPersonId).ToArray();
            Assert.IsNotEmpty(people);
            var distinct = new HashSet<Texture2D>(); GameUi.InvalidatePortraitCache();
            foreach (var p in people)
            {
                string path = "Assets/Resources/PortraitsAnime/" + p.Id + ".png";
                var actual = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.NotNull(actual, p.Name + " " + p.Id);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(1024), p.Id);
                Assert.False(importer.mipmapEnabled, p.Id); Assert.False(importer.isReadable, p.Id);
                Assert.AreEqual(TextureImporterCompression.Compressed, importer.textureCompression, p.Id);
                Assert.That(actual.width, Is.LessThanOrEqualTo(1024)); Assert.That(actual.height, Is.LessThanOrEqualTo(1024));
                float aspect = (float)actual.width / actual.height;
                Assert.That(aspect, Is.InRange(.73f, .77f), p.Id + " portrait aspect ratio");
                var privatePortrait = Resources.Load<Texture2D>("RelationshipPortraits/Private/" + p.Id);
                if (GameSession.IsRomanceCandidate(p.Id))
                {
                    Assert.NotNull(privatePortrait, p.Id + " 缺少已确认的私服头像");
                    var privateImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(privatePortrait));
                    Assert.AreEqual(512, privateImporter.maxTextureSize, p.Id);
                    Assert.False(privateImporter.mipmapEnabled || privateImporter.isReadable, p.Id);
                    Assert.AreEqual(privatePortrait.width, privatePortrait.height, p.Id);
                    Assert.That(privatePortrait.width, Is.LessThanOrEqualTo(512));
                }
                var preferred = privatePortrait ?? actual;
                Assert.AreSame(preferred, GameUi.PortraitOf(p.Id), p.Id);
                Assert.True(distinct.Add(preferred), p.Id + " 各自使用独立头像");
            }
        }
        [Test] public void UnknownDynamicAndDebugPeopleUseCachedAnimeFallback()
        {
            GameUi.InvalidatePortraitCache(); var fallback = Resources.Load<Texture2D>("PortraitsAnime/default");
            Assert.NotNull(fallback);
            Assert.AreSame(fallback, GameUi.PortraitOf("employee_unknown_portrait_test"));
            Assert.AreSame(fallback, GameUi.PortraitOf("employee_unknown_portrait_test"));
            Assert.AreSame(fallback, GameUi.PortraitOf(CityLifeCatalog.InvitationTestPersonId));
            Assert.AreSame(fallback, GameUi.PortraitOf(null));
            GameUi.InvalidatePortraitCache(); Assert.AreSame(fallback, GameUi.PortraitOf("employee_unknown_portrait_test"));
        }
        [Test] public void RetirementAndStudentAgesAreConsistentWithTheirPortraitBriefs()
        {
            Assert.AreEqual(67, CityLifeCatalog.Person("res_grandpa").Age);
            Assert.AreEqual(21, CityLifeCatalog.Person("res_student").Age);
            Assert.AreEqual(14, CityLifeCatalog.Person("res_pupil").Age);
            Assert.AreEqual(11, CityLifeCatalog.Person(CityLifeCatalog.ShenXiao).Age);
        }
    }
}
