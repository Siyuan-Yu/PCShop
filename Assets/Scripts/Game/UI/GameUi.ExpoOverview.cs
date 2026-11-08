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
        private string _expoPage = "overview";

        private void BuildQuarterlyExpo(Transform body, Window w, BrandRecord brand, BrandBusinessState state)
        {
            if (_expoPage != "overview")
            {
                Ui.Size(Ui.Button(body, "返回展会总览", () => { _expoPage = "overview"; w.Rebuild(); }, 17), -1, 36);
                if (_expoPage == "booking") { BuildQuarterlyExpoBooking(body, w, brand, state); return; }
                if (_expoPage == "history")
                {
                    foreach (var past in state.Exhibits.Where(e => e.BrandId == brand.Id && (e.Finished || e.Missed)).OrderByDescending(e => e.Day))
                        BuildExpoResult(body, past);
                    return;
                }
            }
            var exhibit = state.Exhibits.Where(e => e.BrandId == brand.Id && !e.Finished && !e.Missed).OrderBy(e => e.Day).FirstOrDefault();
            if (exhibit != null && exhibit.Format != 2) { BuildLegacyExpoCard(body, w, exhibit); return; }
            int day = exhibit?.Day ?? S.NextBrandExpoDay;
            var heading = BrandCard(body, "本季度展会 · 云帆会展中心");
            Line(heading, new GameDate(day) + "—" + new GameDate(day + 3) + " · 每日09:00—17:00", 18);
            Line(heading, exhibit == null ? "尚未报名 · 报名截止 " + new GameDate(day - 7)
                : exhibit.Attending ? "现场进行中 · 第" + (exhibit.Decision / 6 + 1) + "天"
                : "已报名 · " + (day > S.Clock.Today.TotalDays ? "距开幕" + (day - S.Clock.Today.TotalDays) + "天" : "已到展期"), 18, Ui.Accent);
            Para(heading, "连续参加4天，每天6段现场互动；门店和房租照常结算。", 15, Ui.Dim);
            if (exhibit != null && !exhibit.Attending)
                Para(heading, "请于" + new GameDate(exhibit.Day) + "09:00—10:00亲自到云帆会展中心报到。已加入日程，前一天及出发前会提醒。", 18, Ui.Good);
            if (exhibit == null)
            {
                bool bookingOpen = S.Clock.Today.TotalDays <= day - 7;
                Ui.Size(Ui.Button(body, "报名参展 · 选择摊位、展品与KOL", () => { _expoPage = "booking"; w.Rebuild(); }, 18, Ui.ButtonHot, bookingOpen), -1, 44);
                if (!bookingOpen) Para(body, "本届报名已截止，可以到会展中心参观，下一季度再报名。", 17);
                var latest = state.Exhibits.Where(e => e.BrandId == brand.Id).OrderByDescending(e => e.Day).FirstOrDefault();
                if (latest != null) BuildExpoResult(body, latest);
                ExpoTravelButton(body);
            }
            else if (_expoPage != "overview")
            {
                BuildExpoPreparationDetail(body, w, exhibit);
            }
            else
            {
                var e = exhibit;
                Line(body, "参展筹备", 21, Ui.Accent);
                var summary = Ui.Row(body, 62, 8);
                BrandMetric(summary, "摊位", GameSession.ExpoBoothName(e.Booth) + " · 已预订");
                BrandMetric(summary, "展品", e.Products.Length + "种 · 每种100件");
                var details = Ui.Row(body, 34, 8);
                ExpoDetailButton(details, w, "查看报名与费用", "fees");
                ExpoDetailButton(details, w, "查看展品", "goods");
                var available = S.ExpoAvailableKols(e.BrandId, e.Day);
                var people = BrandCard(body, "站台KOL · 已安排" + e.KolIds.Length + "人 / 至少" + GameSession.ExpoMinKols(e.Booth) + "人");
                var row = Ui.Row(people, 64, 8);
                foreach (string person in e.KolIds.Take(3))
                {
                    var content = Ui.Row(row, 64, 5);
                    Ui.Size(content, -1, 64, 1);
                    KolAvatar(content, person, 48, 64);
                    var info = Ui.Rect(content, "ExpoPersonText"); Ui.Size(info, -1, 64, 1); Ui.VList(info, 3, 0);
                    Line(info, S.CityPerson(person).Name, 16, Ui.Accent);
                    Line(info, available.Contains(person) ? "合约有效" : "需替换", 14, available.Contains(person) ? Ui.Good : Ui.Warn);
                }
                int endorserCount = S.ExpoEndorsers(e).Length;
                if (endorserCount > 0) Line(people, "以上受邀代言人参加本届展会，不另加人。", 16, Ui.Dim);
                ExpoDetailButton(people, w, "查看人员安排", "people");
                var poster = BrandCard(body, "海报 · " + (e.Booth == 0 ? "小型摊位无需海报" : e.DesignerId == null
                    ? "尚未委托" : e.PosterReadyAt <= S.Clock.Now.TotalMinutes ? "已完成" : "制作中"));
                if (e.DesignerId != null)
                {
                    var person = Ui.Row(poster, 64, 8); KolAvatar(person, e.DesignerId, 48, 64);
                    Line(person, S.CityPerson(e.DesignerId).Name + (e.PosterReadyAt > S.Clock.Now.TotalMinutes
                        ? " · 还需" + (e.PosterReadyAt - S.Clock.Now.TotalMinutes) + "分钟" : " · 制作完成"), 17);
                }
                else if (e.Booth > 0)
                {
                    Line(poster, "制作需24小时 · ¥" + GameSession.ExpoPosterFee(e.Booth), 16, Ui.Dim);
                    ExpoDetailButton(poster, w, "选择制作人", "poster");
                }
                Line(body, "摊位已付 ¥" + GameSession.ExpoBoothFee(e.Booth).ToString("N0")
                    + " · 4天KOL总日酬约 ¥" + (e.KolDayQuotes.Sum() * 4).ToString("N0"), 16, Ui.Dim);
                if (e.Attending)
                    Ui.Size(Ui.Button(body, "继续展会现场", () => { CloseAllWindows(); OpenQuarterlyExpo(e.Id); }, 18, Ui.ButtonHot), -1, 42);
                else
                {
                    string blocker = S.ExpoStartBlocker(e.Id);
                    Line(body, e.Booth > 0 && e.DesignerId == null ? "下一步：选择制作人，委托海报。" : blocker ?? "准备齐全，可以报到参展。", 17, blocker == null ? Ui.Good : Ui.Warn);
                    var actions = Ui.Row(body, 40, 8);
                    ExpoTravelButton(actions);
                    Ui.Size(Ui.Button(actions, S.ExpoJointBrandCount(e.Day)>1?"同届品牌联合报到…":"报到并参加4天展会…", () => ConfirmBusiness("连续4天参展",
                        "本届"+S.ExpoJointBrandCount(e.Day)+"个品牌联合报到，各自处理24段现场互动，共用4天时间。餐宿由展务安排，库存、站台费和结算各自独立。本品牌4天KOL总日酬约¥"
                        + (e.KolDayQuotes.Sum() * 4).ToString("N0") + "；其他品牌按各自报名报价结算，可中途保存并继续。", () =>
                        { if (S.StartQuarterlyExpo(e.Id)) { CloseAllWindows(); OpenQuarterlyExpo(e.Id); } else w.Rebuild(); }),
                        18, Ui.ButtonHot, blocker == null), -1, 40, 1).name = "ExpoStart_" + e.Id;
                }
            }
            if (state.Exhibits.Any(e => e.BrandId == brand.Id && (e.Finished || e.Missed)))
                ExpoDetailButton(body, w, "历届参展记录", "history");
        }

        private void ExpoDetailButton(Transform parent, Window w, string label, string page)
        {
            Ui.Size(Ui.Button(parent, label, () => { _expoPage = page; w.Rebuild(); }, 17), -1, 34, 1);
        }

        private void ExpoTravelButton(Transform parent)
        {
            var place = S.City.GetPlace("life_expo");
            string why = S.DistrictAccessBlocker(place);
            Ui.Size(Ui.Button(parent, "前往会展中心", () => { CloseAllWindows(); S.BeginCityTravel(place.Id); }, 17, Ui.ButtonHot, why == null), -1, 40, 1);
        }

        private void BuildExpoResult(Transform body, BrandExhibit e)
        {
            var card = BrandCard(body, new GameDate(e.Day) + " · " + (e.Finished ? "已闭展" : "未完成参展"));
            Line(card, "现场营收 " + Money.FromCents(e.RevenueCents) + " · 售出" + e.Products.Sum(p => p.Sold) + "件 · 得分" + e.Score, 18);
            Line(card, "未售库存" + (e.StockReturned ? "已退回原门店" : "尚未退回"), 16, Ui.Dim);
            foreach (string entry in e.Journal.TakeLast(3)) Para(card, entry, 15, Ui.Dim);
            if (e.Format == 2) Ui.Size(Ui.Button(card, "查看本届结算", () => OpenExpoSettlement(e.Id), 17), -1, 36);
        }

        private void BuildExpoPreparationDetail(Transform body, Window w, BrandExhibit e)
        {
            if (_expoPage == "goods")
            {
                Line(body, "展品 · 来自 " + S.PlaceTitle(S.City.GetPlace(e.ShopId)), 20, Ui.Accent);
                foreach (var product in e.Products)
                    Line(body, S.BrandProductName(product.ProductId) + " · 移交" + product.Reserved + "件 · 已售" + product.Sold + "件", 18);
                Para(body, "展品已由展务保管，闭展后剩余库存退回原门店。", 16, Ui.Dim);
            }
            else if (_expoPage == "fees")
            {
                Line(body, GameSession.ExpoBoothName(e.Booth) + " · 摊位已付 ¥" + GameSession.ExpoBoothFee(e.Booth).ToString("N0"), 20, Ui.Accent);
                Line(body, "海报 ¥" + GameSession.ExpoPosterFee(e.Booth) + (e.Booth == 0 ? " · 无需制作" : e.DesignerId == null ? " · 尚未支付" : " · 已支付"), 18);
                Line(body, "KOL每日总日酬 ¥" + e.KolDayQuotes.Sum().ToString("N0") + " · 4天约 ¥" + (e.KolDayQuotes.Sum() * 4).ToString("N0"), 18);
                Para(body, "参展期间按报价每日结算站台费。已结算站台费 " + Money.FromCents(e.ExpoWagesCents), 17);
            }
            else if (_expoPage == "poster")
            {
                Line(body, "委托海报 · 24小时 · ¥" + GameSession.ExpoPosterFee(e.Booth), 20, Ui.Accent);
                foreach (string person in new[] { "life_designer", "resident_dengyue", "life_creator" })
                {
                    string id = person;
                    var row = Ui.Row(body, 90, 10); KolAvatar(row, id, 60, 80);
                    var info = Ui.Rect(row, "PosterPerson"); Ui.Size(info, -1, 90, 1); Ui.VList(info, 3, 0);
                    Line(info, S.CityPerson(id).Name, 19, Ui.Accent);
                    string why = S.ExpoPosterBlocker(e.Id, id);
                    if (why != null) Line(info, why, 15, Ui.Dim);
                    Ui.Size(Ui.Button(info, "委托制作…", () => ConfirmBusiness("委托海报",
                        S.CityPerson(id).Name + "制作24小时，支付¥" + GameSession.ExpoPosterFee(e.Booth) + "。", () =>
                        { if (S.CommissionExpoPoster(e.Id, id)) _expoPage = "overview"; w.Rebuild(); }), 17, Ui.ButtonHot, why == null), -1, 34);
                }
            }
            else if (_expoPage == "people")
            {
                BuildExpoEndorsers(body, e, false);
                Line(body, "站台 KOL · 4 天", 20, Ui.Accent);
                var available = S.ExpoAvailableKols(e.BrandId, e.Day);
                for (int index = 0; index < e.KolIds.Length; index++)
                {
                    string person = e.KolIds[index];
                    var row = Ui.Row(body, 90, 10); KolAvatar(row, person, 60, 80);
                    var info = Ui.Rect(row, "ExpoKolDetails"); Ui.Size(info, -1, 90, 1); Ui.VList(info, 3, 0);
                    Line(info, S.CityPerson(person).Name + " · 日酬¥" + e.KolDayQuotes[index], 18, Ui.Accent);
                    Line(info, available.Contains(person) ? "合约覆盖展期" : "合约失效，请替换", 16);
                    if (available.Contains(person) || e.Attending) continue;
                    foreach (string candidate in available.Except(e.KolIds))
                    {
                        string replacement = candidate;
                        var choice = Ui.Row(body, 65, 8); KolAvatar(choice, replacement, 45, 60);
                        Ui.Size(Ui.Button(choice, "替换为" + S.CityPerson(replacement).Name, () =>
                        { S.ReplaceExpoKol(e.Id, person, replacement); w.Rebuild(); }, 17), -1, 36, 1);
                    }
                }
            }
        }

        private void BuildExpoEndorsers(Transform body, BrandExhibit exhibit, bool onsite)
        {
            var people = S.ExpoEndorsers(exhibit);
            if (people.Length == 0 || onsite && S.Clock.Today.TotalDays != exhibit.Day) return;
            var card = BrandCard(body, "开幕日 · 品牌代言人");
            foreach (string person in people)
            {
                var row = Ui.Row(card, 62, 8);
                bool focus = onsite && S.ExpoPersonArrived(person);
                KolAvatar(row, person, 44, 58, onsite && !focus, focus ? (Action)(() => _ctrl.FocusExpoPerson(person)) : null);
                var details = Ui.Rect(row, "ExpoEndorserDetails"); Ui.Size(details, -1, 62, 1); Ui.VList(details, 3, 0);
                Line(details, S.CityPerson(person).Name + (exhibit.KolIds.Contains(person) ? " · 兼任站台KOL" : ""), 17, Ui.Accent);
                Line(details, S.ExpoEndorserStatus(exhibit, person), 15, focus ? Ui.Good : Ui.Dim);
            }
            Line(card, onsite ? "点击已到场的代言人头像可切换镜头。" : "本届站台费按报名报价每日结算。", 15, Ui.Dim);
        }
    }
}
