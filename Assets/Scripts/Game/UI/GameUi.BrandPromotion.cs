using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _promotionPage = "overview", _promotionMode = "campaign", _promotionAgency;

        private void BuildBrandPromotion(Transform body, Window w, BrandRecord brand, BrandBusinessState state)
        {
            if (_promotionPage != "overview")
            {
                BuildBrandPromotionSelection(body, w, brand);
                return;
            }
            var metrics = Ui.Row(body, 56, 10);
            BrandMetric(metrics, "近7天实际到店客流", S.BrandActualVisits(brand.Id).ToString("N0") + "人次");
            BrandMetric(metrics, "当前推广客流加成", SignedPercent(S.BrandEffectivePromotionTraffic(brand.Id)));
            Para(body, "到店人数包含各来源顾客。推广加成影响来客概率，各人贡献按品牌加成上限同比折算。最终客流还受品牌知名度、口碑、地段和营业情况影响。", 15, Ui.Dim);
            if (S.BrandVisitsSinceDay < 0)
                Line(body, "实际到店统计尚未开始，营业后累计。", 15, Ui.Dim);
            else if (S.BrandVisitsSinceDay > S.BrandStatsFirstDay)
                Line(body, "到店记录从" + new GameDate(S.BrandVisitsSinceDay) + "开始，尚不足7天。", 15, Ui.Dim);

            Line(body, "新增品牌合作", 21, Ui.Accent);
            var actions = Ui.Row(body, 42, 8);
            Ui.Size(Ui.Button(actions, "管理品牌代言人", () => { _brandTab = BrandKolTab; w.Rebuild(); }, 18, Ui.ButtonHot), -1, 42, 1);
            Ui.Size(Ui.Button(actions, "发起短期推广", () => { _promotionMode = "campaign"; _promotionPage = "channels"; w.Rebuild(); }, 18), -1, 42, 1);

            var deals = state.Deals.Where(d => d.BrandId == brand.Id && d.Mode == "campaign" && !d.Ended && d.End > S.Clock.Today.TotalDays).ToArray();
            Line(body, "当前合作 · " + deals.Length + "项", 21, Ui.Accent);
            if (deals.Length == 0) Para(body, "尚未安排短期推广。代言签约、直播排班和展会人员统一在代言人页管理。", 18);
            foreach (var deal in deals)
            {
                var d = deal;
                var card = BrandCard(body, "");
                var row = Ui.Row(card, 120, 10);
                KolAvatar(row, d.PersonId, 75, 100);
                var info = Ui.Rect(row, "PromotionContract"); Ui.Size(info, -1, 120, 1); Ui.VList(info, 3, 0);
                Line(info, S.CityPerson(d.PersonId).Name + " · " + (d.Mode == "endorse" ? "品牌代言" : "短期推广")
                    + (d.Exclusive ? " · 独家" : ""), 20, Ui.Accent);
                Line(info, "剩余" + (d.End - S.Clock.Today.TotalDays) + "天 · 客流加成 " + SignedPercent(S.BrandEffectiveDealTraffic(d)), 17);
                Line(info, "固定费 " + Money.FromCents(d.Fee) + (d.Mode == "endorse" ? " ＋ 品牌营业额2%" : ""), 16);
                Line(info, d.Response == "pause" ? "已暂停推广" : d.Response == "negotiate" ? "核实协商中" : "合作中", 15, Ui.Dim);
                Ui.Size(Ui.Button(row, "查看合约", () => OpenBrandDealDetails(d.Id), 17), 135, 38);
            }
        }

        private static string SignedPercent(double value) => value.ToString("+0.#%;-0.#%;0%");

        private void BuildBrandPromotionSelection(Transform body, Window w, BrandRecord brand)
        {
            bool candidates = _promotionPage == "candidates";
            Ui.Size(Ui.Button(body, candidates ? "返回选择渠道" : "返回短期推广", () =>
            {
                _promotionPage = candidates ? "channels" : "overview"; w.Rebuild();
            }, 17), -1, 36);
            Line(body, (_promotionMode == "endorse" ? "请代言人" : "发起短期推广") + " · " + brand.Name, 21, Ui.Accent);
            if (!candidates)
            {
                Para(body, "三家公司在社区MCN设联合商务接待。选择公司了解旗下人选，未认识的人需要到场安排介绍并交换名片。", 17);
                foreach (var company in BrandAgency.All)
                {
                    var agency = company;
                    var card = BrandCard(body, agency.Name + " · " + agency.Specialty);
                    Para(card, agency.Description, 17);
                    Line(card, "接待地点：社区MCN · 09:00—18:00", 15, Ui.Dim);
                    Ui.Size(Ui.Button(card, "查看旗下人选", () =>
                    {
                        _promotionAgency = agency.Id; _promotionPage = "candidates"; w.Rebuild();
                    }, 17), -1, 36);
                }
                Ui.Size(Ui.Button(body, "从已认识的人中选择", () => { _promotionAgency = null; _promotionPage = "candidates"; w.Rebuild(); }, 18), -1, 40);
                return;
            }
            var selected = BrandAgency.Get(_promotionAgency);
            Line(body, selected?.Name ?? "已认识的人", 19, Ui.Accent);
            var people = selected != null ? selected.People.Where(GameSession.BrandPeople.Contains).ToArray()
                : GameSession.BrandPeople.Where(p => S.KolContactBlocker(p) == null).ToArray();
            if (people.Length == 0) Para(body, "还没有认识可以合作的创作者，可以返回选择MCN公司安排介绍。", 17);
            foreach (string person in people)
            {
                string id = person;
                var card = BrandCard(body, "");
                var row = Ui.Row(card, 105, 10);
                KolAvatar(row, id, 75, 100);
                var info = Ui.Rect(row, "BrandCandidate"); Ui.Size(info, -1, 105, 1); Ui.VList(info, 4, 0);
                Line(info, S.CityPerson(id).Name + (S.KolContactBlocker(id) == null ? " · 已认识" : " · 尚未认识"), 20, Ui.Accent);
                Line(info, "行业适配 " + S.BrandAudienceFit(id, brand.Kind).ToString("0%"), 17);
                double traffic = (_promotionMode == "endorse" ? .12 : .08) * S.BrandAudienceFit(id, brand.Kind);
                Line(info, "基础客流加成 " + SignedPercent(traffic) + " · 动态与争议会调整实际效果", 15, Ui.Dim);
                if (S.KolContactBlocker(id) == null)
                    Ui.Size(Ui.Button(card, "查看合作方案", () => OpenBrandDealProposal(brand.Id, id, _promotionMode), 17, Ui.ButtonHot), -1, 38);
                else
                    Ui.Size(Ui.Button(card, "前往公司安排介绍", () => GoToBrandAgency(BrandAgency.ForPerson(id).Id), 17), -1, 38);
            }
        }

        private void GoToBrandAgency(string agencyId)
        {
            _mcnAgencyId = agencyId;
            var place = S.City.GetPlace(GameSession.CommunityMcnId);
            string blocker = S.DistrictAccessBlocker(place);
            if (blocker != null) { Toast(blocker); return; }
            CloseAllWindows();
            if (S.CurrentPlace == place) OpenCommunityMcn();
            else S.BeginCityTravel(place.Id);
        }

        private void OpenBrandDealProposal(string brandId, string person, string mode)
        {
            if(mode=="endorse"){_brandId=brandId;OpenKolManagement();return;}
            OpenWindow("合作方案", 780, 650, w =>
            {
                var brand = S.BrandBusiness.Brands.First(b => b.Id == brandId);
                var row = Ui.Row(w.Content, 100, 10); KolAvatar(row, person, 75, 100);
                Line(row, S.CityPerson(person).Name + " · " + brand.Name, 22, Ui.Accent);
                Para(w.Content, "短期推广7天，一次支付固定费，不含代言、直播排班或参展资格。", 18);
                Para(w.Content, "暂停推广不退固定费；提前解约支付剩余固定费的30%，重大已核实争议免解约费。", 16, Ui.Dim);
                foreach (bool exclusive in new[] { false })
                {
                    bool chosen = exclusive;
                    long quote = S.BrandDealQuote(person, mode, chosen);
                    double traffic = (mode == "endorse" ? .12 : .08) * (chosen ? 1.25 : 1) * S.BrandAudienceFit(person, brand.Kind);
                    Line(w.Content, (chosen ? "独家代言" : mode == "endorse" ? "普通代言" : "短期推广")
                        + " · " + Money.FromCents(quote) + " · 基础客流 " + SignedPercent(traffic), 18);
                    Ui.Size(Ui.Button(w.Content, "确认这份合作…", () => ConfirmBusiness("确认签约",
                        S.CityPerson(person).Name + " · " + brand.Name + "\n支付固定费 " + Money.FromCents(quote)
                        + "，推广7天。", () =>
                        {
                            if (S.SignBrandDeal(brandId, person, mode, chosen))
                            {
                                w.Close(); _promotionPage = "overview"; RebuildWindows();
                            }
                        }), 17, Ui.ButtonHot), -1, 40);
                }
            });
        }

        private void OpenBrandDealDetails(string dealId)
        {
            var contract=S.BrandBusiness.Deals.FirstOrDefault(d=>d.Id==dealId);
            if(contract!=null&&!string.IsNullOrEmpty(contract.KolContractId))
            {_brandId=contract.BrandId;_brandTab=BrandKolTab;_kolPage="代言人与排班";OpenBrandBusiness();return;}
            OpenWindow("推广合约", 780, 700, w =>
            {
                var d = S.BrandBusiness.Deals.First(x => x.Id == dealId);
                var row = Ui.Row(w.Content, 100, 10); KolAvatar(row, d.PersonId, 75, 100);
                Line(row, S.CityPerson(d.PersonId).Name, 22, Ui.Accent);
                Para(w.Content, new GameDate(d.Start) + "—" + new GameDate(d.End) + "\n固定费 "
                    + Money.FromCents(d.Fee) + " · 已付分成 " + Money.FromCents(d.SharePaid), 18);
                Line(w.Content, "当前客流加成 " + SignedPercent(S.BrandEffectiveDealTraffic(d)) + " · 风波级别 " + d.Severity, 18);
                Para(w.Content, S.BrandBusiness.Careers.FirstOrDefault(c => c.PersonId == d.PersonId)?.News ?? "暂无动态", 17);
                bool active = !d.Ended && d.End > S.Clock.Today.TotalDays;
                BrandButton(w.Content, "继续推广", () => S.RespondBrandDeal(d.Id, "continue"), w, active);
                BrandButton(w.Content, "暂停推广", () => S.RespondBrandDeal(d.Id, "pause"), w, active);
                BrandButton(w.Content, "核实协商 · ¥200", () => S.RespondBrandDeal(d.Id, "negotiate"), w,
                    active && d.Severity > 0 && d.LastNegotiationDay < d.NewsDay);
                long penalty = d.Severity >= 3 ? 0 : d.Fee * Math.Max(0, d.End - S.Clock.Today.TotalDays) / Math.Max(1, d.End - d.Start) * 30 / 100;
                Ui.Size(Ui.Button(w.Content, "解除合约…", () => ConfirmBusiness("解除合约",
                    "解约费用 " + Money.FromCents(penalty) + "，确认后合作结束。", () =>
                    { if (S.RespondBrandDeal(d.Id, "end")) { w.Close(); RebuildWindows(); } }), 17, Ui.Warn, active), -1, 38);
                Ui.Size(Ui.Button(w.Content, "返回短期推广", () => { w.Close(); RebuildWindows(); }, 17), -1, 38);
            });
        }
    }
}
