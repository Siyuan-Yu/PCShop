using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;

namespace PCShopSell.Tests
{
    public sealed class IndustrialArtTests
    {
        [TestCase("shop")]
        [TestCase("oldhome")]
        [TestCase("warehouse")]
        [TestCase("office")]
        public void ArchitecturalDressingDoesNotAddMovementOrInteractionObstacles(string model)
        {
            var lot=new GameObject("Existing lot");
            try
            {
                var obstacle=lot.AddComponent<BoxCollider>();obstacle.size=new Vector3(12,8,10);
                CityArchitecture.IndustrialDetails(lot.transform,model,12,10,8,1);
                Assert.AreEqual(1,lot.GetComponentsInChildren<Collider>(true).Length);
                Assert.AreEqual(new Vector3(12,8,10),obstacle.size);
                Assert.IsEmpty(lot.GetComponentsInChildren<PlaceMarker>(true));
                Assert.Greater(lot.GetComponentsInChildren<MeshRenderer>().Length,0);
            }
            finally { Object.DestroyImmediate(lot); }
        }

        [Test]
        public void NavigationArtworkPreservesTextActionAndHitTarget()
        {
            var top=new GameObject("TopBar");
            try
            {
                var row=Ui.Rect(top.transform,"Row");int calls=0;
                var button=Ui.Button(row,"\u7ecf\u8425",()=>calls++);
                Assert.AreEqual("\u7ecf\u8425",button.GetComponentInChildren<Text>().text);
                var icon=button.transform.Find("NavigationIcon");
                Assert.NotNull(icon);Assert.False(icon.GetComponent<Image>().raycastTarget);
                button.onClick.Invoke();Assert.AreEqual(1,calls);
                Assert.True(button.GetComponent<Image>().raycastTarget);
            }
            finally { Object.DestroyImmediate(top); }
        }
    }
}
