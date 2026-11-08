#if UNITY_EDITOR
using System.Linq;
using PCShopSell.Core.Session;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenExpoAcceptance()
        {
            OpenWindow("品牌展会验收 · 作弊工具", 760, 700, w =>
            {
                var body = Ui.Scroll(w.Content, 490);
                Para(body, "品牌创建测试档已准备门店、经营记录、小电驴，以及社区主住所和城郊住处；每家店有3种同业商品，各至少100件。进入后可亲手创建品牌。", 18);
                Ui.Size(Ui.Button(body, "可创建两个品牌（电脑＋服装）", () => OpenBrandCreationPreset("brand_ready_two"), 18, Ui.ButtonHot), -1, 46);
                Ui.Size(Ui.Button(body, "可创建三个品牌（电脑＋服装＋模型手办）", () => OpenBrandCreationPreset("brand_ready_three"), 18, Ui.ButtonHot), -1, 46);
                if (S.IsBrandCreationAcceptance || S.IsBrandExpoAcceptance)
                    Ui.Size(Ui.Button(body, "补齐测试门店商品 · 3种各100件", () =>
                    {
                        S.DebugPrepareBrandAcceptanceStock();
                        w.Rebuild();
                    }, 18, Ui.ButtonHot), -1, 46).name = "PrepareBrandAcceptanceStock";
                if (S.IsBrandCreationAcceptance && S.BrandCreationSuburbHomeBlocker == null)
                    Ui.Size(Ui.Button(body, "给当前品牌测试档补回城郊住处", () =>
                    {
                        if (S.DebugRestoreBrandCreationSuburbHome()) RefreshShortcutMenus();
                        w.Rebuild();
                    }, 18, Ui.ButtonHot), -1, 46).name = "RestoreBrandCreationSuburbHome";

                Para(body, "准备城郊5家、社区1家实际开业门店，配好库存和店员，并租住社区住宅。完成社区前置条件与引荐背景，使用独立测试档，可返回原进度。", 19, Ui.Warn);
                Ui.Size(Ui.Button(body, "创建展会测试档（6家店＋社区住处）", () =>
                {
                    CloseAllWindows(); _ctrl.StartCityLifePreset("quarter_expo");
                    if (_ctrl.Session.IsBrandExpoAcceptance) _ctrl.Ui.OpenExpoAcceptance();
                }, 19, Ui.ButtonHot), -1, 50).name = "CreateBrandExpoPreset";
                if (S.IsBrandExpoAcceptance)
                {
                    var exhibit = S.BrandBusiness.Exhibits.OrderByDescending(e => e.Day).FirstOrDefault();
                    Line(body, "城郊开业 "+S.DistrictOperatingShops(0)+" / 5家 · 社区开业 "+S.DistrictOperatingShops(1)+"家", 18, Ui.Accent);
                    Line(body, "住处："+S.HomeTitle+" · 周租 "+S.CurrentHomeRent, 17);
                    Line(body, "社区前置："+(S.DistrictPrerequisiteBlocker(1)??"已达成（5家城郊店＋100万元现金）"), 17, Ui.Good);
                    if(!S.DebugExpoBusinessBaseReady)
                    {
                        Ui.Size(Ui.Button(body,"补齐当前验收档的门店与社区住处",()=>{S.DebugPrepareExpoBusinessBase();w.Rebuild();},18,Ui.ButtonHot,S.DebugExpoBusinessBaseBlocker==null),-1,46).name="RepairExpoBusinessBase";
                        if(S.DebugExpoBusinessBaseBlocker!=null)Para(body,S.DebugExpoBusinessBaseBlocker,16,Ui.Dim);
                    }
                    Line(body,"晨湾科技 · 新测试档8种展品各150件",17);
                    Para(body, exhibit == null ? "准备：先签约KOL，选择小/中/大摊位，每种展品100件；中大型另委托海报。"
                        : exhibit.Finished ? "交付：回季度展会页，安排20件订单发货，等到货结算。"
                        : "现场：推进到开幕，进入会展中心报到，连续4天处理24段事件与舞台互动。", 18);
                    Ui.Size(Ui.Button(body, "打开品牌展会（摊位 / KOL / 现场 / 订单）", () =>
                    {
                        w.Close(); _brandTab = BrandExpoTab; _brandId = "acceptance-expo"; _brandShop = S.MainShop.Place.Id;
                        OpenBrandBusiness();
                    }, 18, Ui.ButtonHot), -1, 46).name = "OpenAcceptanceBrandExpo";
                    string blocker = S.DebugExpoOpeningBlocker;
                    Ui.Size(Ui.Button(body, "报名后推进到展会当天09:00", () =>
                    {
                        S.DebugAdvanceToExpoOpening(); w.Rebuild();
                    }, 18, Ui.ButtonBg, blocker == null), -1, 46).name = "AdvanceAcceptanceExpo";
                    if (blocker != null) Line(body, blocker, 16, Ui.Dim);
                    Line(body, "快进照常结算，补足身体状态并略过过程弹窗；不代做接待或交货。", 16, Ui.Dim);
                    Ui.Size(Ui.Button(body, "到场等待：推进30分钟", () => { S.AdvanceClock(30); w.Rebuild(); }, 18, Ui.ButtonBg,
                        !S.IsWorking && S.Trip == null && !S.CityActivityExclusive), -1, 42);
                    Ui.Size(Ui.Button(body, "保存当前展会测试档", () => Toast(_ctrl.SaveGame() ? "测试档已保存。" : "请先处理当前事件。"), 18), -1, 42);
                }
                if (S.IsCityLifeTest)
                    Ui.Size(Ui.Button(w.Content, "返回进入测试前的正式进度", () => { CloseAllWindows(); _ctrl.ReturnFromCityLifePreset(); }, 18, Ui.Warn), -1, 46).name = "ReturnFromBrandExpoAcceptance";
            });
        }

        private void OpenBrandCreationPreset(string scenario)
        {
            if (!S.CanSave) { Toast("请先处理当前事件，再进入独立测试档。"); return; }
            CloseAllWindows();
            _ctrl.StartCityLifePreset(scenario);
            _ctrl.Ui._brandTab = 0;
            _ctrl.Ui.OpenBrandBusiness();
        }
    }
}
#endif
