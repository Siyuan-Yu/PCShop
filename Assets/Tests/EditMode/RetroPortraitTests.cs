using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class RetroPortraitTests
    {
        [Test] public void EveryNamedNpcLoadsItsOwnRetroTextureWithBoundedMemorySettings()
        {
            var people = CityLifeCatalog.People.Where(p => p.Id != CityLifeCatalog.InvitationTestPersonId).ToArray();
            Assert.IsNotEmpty(people);
            var distinct = new HashSet<Texture2D>(); GameUi.InvalidatePortraitCache();
            foreach (var p in people)
            {
                string path = "Assets/Resources/PortraitsRetro/" + p.Id + ".png";
                var actual = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.NotNull(actual, p.Name + " " + p.Id);
                Assert.AreSame(actual, GameUi.PortraitOf(p.Id), p.Id + " uses its own art, not the fallback");
                Assert.True(distinct.Add(actual), p.Id + " does not reuse another NPC texture");
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(512, importer.maxTextureSize, p.Id);
                Assert.False(importer.mipmapEnabled, p.Id); Assert.False(importer.isReadable, p.Id);
                Assert.AreEqual(TextureImporterCompression.Compressed, importer.textureCompression, p.Id);
                Assert.That(actual.width, Is.LessThanOrEqualTo(512)); Assert.That(actual.height, Is.LessThanOrEqualTo(512));
                float aspect = (float)actual.width / actual.height;
                Assert.That(aspect, Is.InRange(.73f, .77f), p.Id + " portrait aspect ratio");
            }
        }
        [Test] public void UnknownDynamicAndDebugPeopleUseCachedRetroFallback()
        {
            GameUi.InvalidatePortraitCache(); var fallback = Resources.Load<Texture2D>("PortraitsRetro/default");
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
