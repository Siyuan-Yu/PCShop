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
        [Test] public void SignedThroughPhoneThenAssignedAfterSaveWithoutChargingASecondFee()
        {
            using (var s = Make())
            {
                s.Meet("momo", 0);
                At(s, s.Home.Id);
                s.CityPersonState("momo").LocationPlaceId = GameSession.CommunityMcnId;
                long cash = s.Ledger.Balance.Cents;
                Assert.False(s.SignKolAmbassador("momo", 60, 7));
                Assert.True(s.SignKolAmbassador("momo", 60, 7, remote: true, phoneCall: true));
                var pending = s.ActiveKolAmbassador("momo");
                Assert.IsNull(pending.BrandId);
                Assert.AreEqual(60 * 1440L, pending.EndAt - pending.SignedAt);
                Assert.AreEqual(7, pending.FrequencyDays);
                Assert.False(pending.Enabled);
                Assert.False(s.SetKolSchedule(pending.Id, 1080, 120, true));
                Assert.AreEqual(0, s.BrandKolCount("acceptance-expo"));
                Assert.AreEqual(cash - s.KolSigningFee("momo", 60) * 100, s.Ledger.Balance.Cents);
                var save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using (var loaded = GameSession.Load(s.City, save))
                {
                    Assert.AreEqual("chengguang", loaded.ActiveKolAmbassador("momo").AgencyId);
                    Assert.True(loaded.AssignKolBrand(pending.Id, "acceptance-expo"));
                    Assert.False(loaded.AssignKolBrand(pending.Id, "acceptance-expo"));
                    Assert.AreEqual(s.Ledger.Balance.Cents, loaded.Ledger.Balance.Cents);
                    Assert.AreEqual(pending.Id, loaded.BrandBusiness.Deals.Single().KolContractId);
                    Assert.AreEqual(1, loaded.BrandKolCount("acceptance-expo"));
                    Assert.True(loaded.SetKolSchedule(pending.Id, 1080, 120, true));
                    Assert.AreEqual(7, loaded.ActiveKolAmbassador("momo").FrequencyDays);
                }
            }
        }

        [Test] public void RemovedShortPromotionInOldSaveHasNoTrafficEffectOrNewCharge()
        {
            using (var s = Make())
            {
                var save = s.CreateSave();
                save.brandBusiness.Deals = new[] { new BrandDeal { Id = "old-campaign", BrandId = "acceptance-expo",
                    PersonId = "momo", Mode = "campaign", End = s.Clock.Today.TotalDays + 7, Fee = 10000 } };
                using (var loaded = GameSession.Load(s.City, save))
                {
                    Assert.True(loaded.BrandBusiness.Deals.Single().Ended);
                    Assert.Zero(loaded.BrandDealTraffic(loaded.BrandBusiness.Deals.Single()));
                    Assert.AreEqual(s.Ledger.Balance.Cents, loaded.Ledger.Balance.Cents);
                    Assert.True(loaded.IsMet("momo"));
                }
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
                    Assert.False(buttons.Any(b => b.name.StartsWith("McnReception_")));
                    Assert.False(buttons.Any(b => b.name.StartsWith("McnKolProposal_")));
                    Assert.True(buttons.Any(b => b.name.StartsWith("McnIntroduce_") || b.name.StartsWith("McnContact_")));
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
