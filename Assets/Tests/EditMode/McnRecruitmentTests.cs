using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class McnRecruitmentTests
    {
        private static GameSession Make()=>GameSession.CreateCityLifePreset(CityLayout.FromData(
            JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text)),"quarter_expo");
        private static void At(GameSession s,string id)=>typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,s.City.GetPlace(id));
        [Test] public void EveryKolBelongsToExactlyOneCompanyAndDigitalReviewerUsesDigitalAgency()
        {
            foreach(var kol in KolMarket.All)Assert.AreEqual(1,BrandAgency.All.Count(a=>a.People.Contains(kol.Id)),kol.Id);
            Assert.AreEqual("chengguang",BrandAgency.ForPerson("qiao").Id);
            Assert.AreEqual("yijian",BrandAgency.ForPerson("life_clothier").Id);
            Assert.AreEqual("wanjie",BrandAgency.ForPerson("resident_ningxi").Id);
        }
        [Test] public void KnownPersonStillNeedsPhysicalOfficeAndTheirOwnAgencyReception()
        {
            using(var s=Make())
            {
                s.Meet("momo",0);long cash=s.Ledger.Balance.Cents;
                At(s,s.Home.Id);
                Assert.False(s.BeginMcnRecruitment("chengguang"));
                Assert.False(s.SignKolLive("acceptance-expo","momo"));
                At(s,GameSession.CommunityMcnId);s.Clock.Restore(s.Clock.Today,600);
                Assert.False(s.SignKolLive("acceptance-expo","momo"));
                Assert.True(s.BeginMcnRecruitment("yijian"));
                Assert.False(s.SignBrandDeal("acceptance-expo","momo","endorse",false));
                Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                Assert.IsEmpty(s.KolStreaming.Contracts);
                Assert.True(s.BeginMcnRecruitment("chengguang"));
                Assert.True(s.SignKolLive("acceptance-expo","momo"));
                var c=s.KolStreaming.Contracts.Single();
                Assert.AreEqual("chengguang",c.AgencyId);
                Assert.AreEqual(cash-s.KolSigningFee("momo")*100,s.Ledger.Balance.Cents);
                Assert.AreEqual(c.Id,s.BrandBusiness.Deals.Single().KolContractId);
                Assert.False(s.SignKolLive("acceptance-expo","momo"));
                using(var loaded=GameSession.Load(s.City,s.CreateSave()))
                    Assert.AreEqual("chengguang",loaded.KolStreaming.Contracts.Single().AgencyId);
            }
        }
        [Test] public void LeavingClosingOrReturningAnotherDayCannotReuseReceptionToSignRemotely()
        {
            using(var s=Make())
            {
                s.Meet("qiao",0);At(s,GameSession.CommunityMcnId);s.Clock.Restore(s.Clock.Today,600);
                Assert.True(s.BeginMcnRecruitment("chengguang"));long cash=s.Ledger.Balance.Cents;
                At(s,s.Home.Id);Assert.False(s.SignKolLive("acceptance-expo","qiao"));
                At(s,GameSession.CommunityMcnId);s.Clock.Restore(s.Clock.Today,1080);
                Assert.False(s.SignKolLive("acceptance-expo","qiao"));
                Assert.False(s.BeginMcnRecruitment("chengguang"));
                s.Clock.Restore(s.Clock.Today.AddDays(1),600);
                Assert.False(s.SignKolLive("acceptance-expo","qiao"));
                Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                Assert.True(s.BeginMcnRecruitment("chengguang"));
                Assert.True(s.SignKolLive("acceptance-expo","qiao"));
            }
        }
        [Test] public void BrandRecruitmentPageSelectsCompaniesWithoutDirectSigningButtons()
        {
            using(var s=Make())
            {
                var root=new GameObject("McnRecruitUiTest");root.SetActive(false);
                var ctrl=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(ctrl,s);
                typeof(GameController).GetProperty("City").SetValue(ctrl,s.City);
                var previous=UnityEngine.EventSystems.EventSystem.current;
                var ui=new GameUi(ctrl);
                try
                {
                    ui.OpenKolManagement();
                    var buttons=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).ToArray();
                    foreach(var company in BrandAgency.All)
                        Assert.True(buttons.Any(b=>b.name=="KolAgencyVisit_"+company.Id));
                    Assert.False(buttons.Any(b=>b.name.StartsWith("KolSign_")));
                    buttons.Single(b=>b.name=="KolAgencyPreview_wanjie").onClick.Invoke();
                    buttons=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).ToArray();
                    Assert.False(buttons.Single(b=>b.name=="McnReception_wanjie").interactable);
                    Assert.IsEmpty(s.KolStreaming.Contracts);
                }
                finally
                {
                    foreach(var canvas in ui.Canvases)if(canvas!=null)Object.DestroyImmediate(canvas.gameObject);
                    if(previous==null&&UnityEngine.EventSystems.EventSystem.current!=null)Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                    Object.DestroyImmediate(root);
                }
            }
        }
    }
}
