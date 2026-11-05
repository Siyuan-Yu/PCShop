using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _kolPage = "主播与排班", _selectedKolContract;
        private static Material _kolOfflinePortrait;
        private GameSession _kolDraftSession;
        private readonly Dictionary<string,KolLiveContract> _kolScheduleDrafts=new Dictionary<string,KolLiveContract>();
        public void OpenKolManagement(){_brandTab=BrandKolTab;_kolPage="KOL市场";OpenBrandBusiness();}
        private static string KolTime(int minute)=>$"{minute/60:00}:{minute%60:00}";
        private void KolAvatar(Transform parent,string id,float width,float height, bool dimmed = false, Action onClick = null)
        {
            var portrait=Ui.Rect(parent,"KolPortraitHolder_"+id);Ui.Size(portrait,width,height);
            var image = Ui.Rect(portrait, "KolPortrait_"+id);
            image.anchorMin = image.anchorMax = new Vector2(.5f, .5f);
            var raw=image.gameObject.AddComponent<RawImage>();raw.texture=PortraitOf(id);
            if (dimmed)
            {
                raw.color = new Color(.55f, .55f, .55f);
                if (_kolOfflinePortrait == null)
                {
                    var shader = Resources.Load<Shader>("KolStudio/OfflinePortrait");
                    if (shader != null) _kolOfflinePortrait = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                if (_kolOfflinePortrait != null) raw.material = _kolOfflinePortrait;
            }
            var fit = image.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = raw.texture == null ? .75f : (float)raw.texture.width / raw.texture.height;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=raw;
            button.onClick.AddListener(() => { if (onClick == null) OpenCityPersonProfile(id); else onClick(); });
        }
        private void BuildKolBrandPage(Transform parent, Window w, BrandRecord brand)
        {
            if (_kolDraftSession != S)
            {
                _kolDraftSession = S;
                _kolScheduleDrafts.Clear();
                _selectedKolContract = null;
            }
            var nav = Ui.Row(parent, 40, 8);
            foreach (var page in new[] { "主播与排班", "KOL市场", "专场记录" })
                Ui.Size(Ui.Button(nav, page, () => { _kolPage = page; w.Rebuild(); }, 18,
                    _kolPage == page ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);
            if (_kolPage == "专场记录") { BuildKolReports(parent, brand); return; }

            var header = Ui.Row(parent, 40, 8);
            Ui.Size(Ui.Label(header, S.BrandLevelName(brand.Id) + " · 签约 " + S.BrandKolCount(brand.Id)
                + " / " + S.BrandKolCapacity(brand.Id), 19, color: Ui.Accent), -1, 40, 1);
            if (brand.Level < 2)
            {
                string upgrade = S.KolUpgradeBlocker(brand.Id);
                var button = Ui.Size(Ui.Button(header, brand.Level == 0 ? "升级中级 · ¥20,000" : "升级高级 · ¥100,000", () =>
                    ConfirmBusiness("升级品牌", brand.Level == 0 ? "支付¥20,000，名额由3人增至7人。" : "支付¥100,000，名额由7人增至12人。",
                        () => { S.UpgradeKolBrand(brand.Id); w.Rebuild(); }), 17, Ui.ButtonHot, upgrade == null), 230, 36);
                button.name = "KolBrandUpgrade";
                button.Tip(upgrade ?? "提升品牌签约名额");
            }
            if (_kolPage == "KOL市场") { BuildKolMarket(parent, w, brand); return; }

            long now = S.Clock.Now.TotalMinutes;
            var contracts = S.KolStreaming.Contracts.Where(c => c.BrandId == brand.Id && !c.Ended && c.EndAt > now).ToArray();
            if (contracts.Length == 0)
            {
                Para(parent, "尚未签约直播KOL。可到KOL市场认识主播并签约。", 19);
                Ui.Size(Ui.Button(parent, "前往KOL市场", () => { _kolPage = "KOL市场"; w.Rebuild(); }, 18, Ui.ButtonHot), -1, 40);
                return;
            }
            var selected = contracts.FirstOrDefault(c => c.Id == _selectedKolContract) ?? contracts[0];
            _selectedKolContract = selected.Id;
            var split = Ui.Row(parent, 490, 10);
            var roster = BrandCard(split, "签约主播"); Ui.Size(roster, 285, 490);
            var list = Ui.Scroll(roster, 382);
            foreach (var c in contracts)
            {
                var row = Ui.Row(list, 74, 8);
                KolAvatar(row, c.PersonId, 49, 66);
                var pick = Ui.Size(Ui.Button(row, S.CityPerson(c.PersonId).Name + "\n" +
                    (c.Enabled ? KolFrequency(c.FrequencyDays) : "专场已暂停"), () =>
                    { _selectedKolContract = c.Id; w.Rebuild(); }, 16, c.Id == selected.Id ? Ui.ButtonHot : Ui.ButtonBg), -1, 66, 1);
                pick.name = "KolSelect_" + c.Id;
            }
            Line(roster, "品牌近7天完成 " + S.KolBrandRecentSpecials(brand.Id) + " 场专场", 15, Ui.Dim);

            var editor = BrandCard(split, S.CityPerson(selected.PersonId).Name + " · 品牌专场排班"); Ui.Size(editor, -1, 490, 1);
            BuildKolSchedule(editor, w, selected);
        }

        private static string KolFrequency(int days) => days == 7 ? "每周一次" : days == 2 ? "隔天" : "每天";

        private void BuildKolSchedule(Transform body, Window w, KolLiveContract contract)
        {
            if (!_kolScheduleDrafts.TryGetValue(contract.Id, out var draft))
            {
                draft = contract.Copy();
                _kolScheduleDrafts[contract.Id] = draft;
            }
            var talent = KolMarket.Get(contract.PersonId);
            Line(body, $"{talent.Topic} · 粉丝 {S.KolFollowers(contract.PersonId):N0} · 人气 {S.KolPopularity(contract.PersonId)}", 16);
            Line(body, $"日薪 ¥{S.KolDailyWage(contract.PersonId):N0} · 暂停专场仍计薪", 15, Ui.Dim);

            Line(body, "专场频率", 18, Ui.Accent);
            var cadence = Ui.Row(body, 40, 6);
            foreach (int days in new[] { 1, 2, 7 })
                Ui.Size(Ui.Button(cadence, KolFrequency(days), () => { draft.FrequencyDays = days; w.Rebuild(); },
                    17, draft.FrequencyDays == days ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);

            var timing = Ui.Row(body, 37, 6);
            int today = S.Clock.Today.TotalDays;
            var dates = Enumerable.Range(today, 7).Prepend(draft.FirstDay).Distinct().ToArray();
            Ui.Size(Ui.Label(timing, "首次专场", 16), 77, 37);
            Ui.Size(Ui.Choice(timing, dates.Select(day => (day == today ? "今天" : day == today + 1 ? "明天" : new GameDate(day).ToString())
                + " · " + Ui.WeekdayName(new GameDate(day).Weekday)), Array.IndexOf(dates, draft.FirstDay),
                index => { draft.FirstDay = dates[index]; w.RequestRebuild(); }), 208, 37);
            var times = Enumerable.Range(0, 96).Select(n => KolTime(n * 15)).ToArray();
            Ui.Size(Ui.Choice(timing, times, draft.StartMinute / 15, index => { draft.StartMinute = index * 15; w.RequestRebuild(); }), 105, 37);
            Ui.Size(Ui.Label(timing, "至", 16, TextAnchor.MiddleCenter), 26, 37);
            Ui.Size(Ui.Choice(timing, times, draft.EndMinute / 15, index => { draft.EndMinute = index * 15; w.RequestRebuild(); }), 105, 37);
            if (draft.EndMinute < draft.StartMinute) Ui.Size(Ui.Label(timing, "次日", 15, color: Ui.Dim), 42, 37);

            long cursor = S.Clock.Now.TotalMinutes;
            var upcoming = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                long next = S.KolNextSpecialStart(draft, cursor);
                if (next < 0) break;
                upcoming.Add(Ui.WeekdayName(new GameDate((int)(next / 1440)).Weekday) + " " + KolTime((int)(next % 1440)));
                cursor = next + 1;
            }
            Line(body, upcoming.Count == 0 ? "合约期内没有后续专场" : "后续专场 · " + string.Join("  ·  ", upcoming), 16, Ui.Good);
            var person = S.CityPerson(contract.PersonId);
            int duration = (draft.EndMinute - draft.StartMinute + 1440) % 1440;
            bool workConflict = person.WorkStart != person.WorkEnd && Enumerable.Range(0, duration / 15).Any(i =>
                GameSession.CityWithinHours((draft.StartMinute + i * 15) % 1440, person.WorkStart, person.WorkEnd));
            Line(body, workConflict ? "该时段可能与主播工作重叠，忙碌时会延迟开播。" : "主播在空闲日期可以开个人直播。",
                15, workConflict ? Ui.Warn : Ui.Dim);
            Para(body, "下播结算品牌知名度和口碑。同品牌近7天完成的专场越多，后续单场增量越小。", 16);

            var actions = Ui.Row(body, 40, 6);
            Ui.Size(Ui.Button(actions, draft.Enabled ? "专场启用" : "专场暂停", () => { draft.Enabled = !draft.Enabled; w.Rebuild(); }, 16), 104, 40);
            Ui.Size(Ui.Button(actions, "保存专场排班", () =>
            {
                if (S.SetKolSchedule(contract.Id, draft.StartMinute, draft.EndMinute, draft.Enabled, draft.FrequencyDays, draft.FirstDay))
                    Toast("已保存品牌专场排班。");
                w.Rebuild();
            }, 17, Ui.ButtonHot), -1, 40, 1).name = "KolSaveSchedule_" + contract.Id;
            var room = S.GetKolRoom(contract.PersonId, false);
            Ui.Size(Ui.Button(actions, "进入直播间", () =>
            {
                w.Close();
                OpenKolLiveRoom(contract.Id);
            }, 16, Ui.ButtonBg, room.IsLive), 124, 40).name = "KolWatch_" + contract.Id;
            Ui.Size(Ui.Button(actions, "解除合约…", () =>
                ConfirmBusiness("解除直播合约", "释放签约名额，已付签约费不退。", () =>
                {
                    if (S.EndKolLive(contract.Id)) _kolScheduleDrafts.Remove(contract.Id);
                    w.Rebuild();
                }), 16, Ui.Warn), 124, 40);
        }

        private void BuildKolReports(Transform parent, BrandRecord brand)
        {
            var records = S.KolBrandHistory(brand.Id);
            Line(parent, "近7天完成 " + S.KolBrandRecentSpecials(brand.Id) + " 场品牌专场", 19, Ui.Accent);
            if (records.Length == 0) Para(parent, "品牌专场下播后，这里会显示本场累积的知名度和口碑。", 18, Ui.Dim);
            var latest = records.FirstOrDefault();
            if (latest != null && latest.InfluenceAfter > latest.InfluenceBefore)
            {
                BuildKolBrandResult(parent, latest);
                var row = Ui.Row(parent, 68, 8);
                KolAvatar(row, latest.PersonId, 44, 60);
                var details = Ui.Rect(row, "LatestKolReport"); Ui.Size(details, -1, 68, 1); Ui.VList(details, 4, 0);
                Line(details, "最近一场 · " + S.CityPerson(latest.PersonId).Name + " · " + KolTimestamp(latest.EndsAt), 18, Ui.Accent);
                Line(details, $"直播 {latest.LiveMinutes} 分钟 · 峰值在线 {latest.PeakViewers:N0} 人", 17);
                if (records.Length > 1) Line(parent, "历史专场", 18, Ui.Accent);
            }
            foreach (var session in records.Skip(latest != null && latest.InfluenceAfter > latest.InfluenceBefore ? 1 : 0).Take(19))
            {
                var card = BrandCard(parent, "");
                var row = Ui.Row(card, 90, 10);
                KolAvatar(row, session.PersonId, 60, 80);
                var details = Ui.Rect(row, "KolReport"); Ui.Size(details, -1, 90, 1); Ui.VList(details, 4, 0);
                Line(details, S.CityPerson(session.PersonId).Name + " · " + KolTimestamp(session.EndsAt), 18, Ui.Accent);
                Line(details, $"直播 {session.LiveMinutes} 分钟 · 峰值在线 {session.PeakViewers:N0} 人", 16);
                string effects = session.LegacyMinutes == session.LiveMinutes ? "旧版直播记录"
                    : session.InfluenceAfter == session.InfluenceBefore
                    ? "本场未新增品牌收益"
                    : $"品牌知名度 {session.InfluenceBefore} → {session.InfluenceAfter}   ·   品牌口碑 {session.ReputationBefore} → {session.ReputationAfter}";
                Line(details, effects, 17, Ui.Good);
            }
            Para(parent, "直播收益计入品牌，同品类自营门店共享品牌知名度和口碑。", 16, Ui.Dim);
        }

        private void BuildKolBrandResult(Transform parent, KolLiveSession result)
        {
            if (result.InfluenceAfter == result.InfluenceBefore && result.ReputationAfter == result.ReputationBefore)
            {
                string message = result.LiveMinutes - result.LegacyMinutes < 15
                    ? "有效直播不足 15 分钟，本场品牌知名度和口碑均未变化。"
                    : "本场品牌知名度和口碑均未变化。";
                Para(parent, message, 17, Ui.Dim);
                return;
            }
            var highlights = Ui.Row(parent, 124, 8);
            KolReportMetric(highlights, "品牌知名度", result.InfluenceBefore, result.InfluenceAfter);
            KolReportMetric(highlights, "品牌口碑", result.ReputationBefore, result.ReputationAfter);
        }

        private void KolReportMetric(Transform row, string name, int before, int after)
        {
            var card = BrandCard(row, name); Ui.Size(card, 0, 124, 1);
            Line(card, $"{before} → {after}   (+{after - before})", 25, Ui.Good);
            Line(card, "已计入品牌", 15, Ui.Dim);
        }

        private void BuildKolMarket(Transform parent,Window w,BrandRecord brand)
        {
            Para(parent,"90天独家直播合约：一次签约费＋动态日薪，日结，暂停仍计薪。未知联系人可到社区MCN申请介绍并实际见面。",16,Ui.Dim);
            Ui.Size(Ui.Button(parent,"去社区MCN认识KOL",OpenCommunityMcn,17),-1,36);
            for(int i=0;i<KolMarket.All.Length;i+=2)
            {
                var row=Ui.Row(parent,216,10);
                foreach(var talent in KolMarket.All.Skip(i).Take(2))
                {
                    var d=talent;var card=Ui.Panel(row,"KolMarket_"+d.Id,Ui.PanelBg2).rectTransform;Ui.Size(card,-1,216,1);Ui.VList(card,4,8);
                    var top=Ui.Row(card,128,8);KolAvatar(top,d.Id,84,112);var text=Ui.Rect(top,"TalentInfo");Ui.Size(text,-1,128,1);Ui.VList(text,2);
                    Line(text,S.CityPerson(d.Id).Name,20,Ui.Accent);Line(text,d.Topic,15);
                    Line(text,$"魅力 {d.Charm}　智力 {d.Intelligence}",17);Line(text,$"粉丝 {S.KolFollowers(d.Id):N0} · 人气 {S.KolPopularity(d.Id)}",15,Ui.Dim);
                    string why=S.KolSigningBlocker(brand.Id,d.Id);
                    Ui.Size(Ui.Button(card,"签约 ¥"+S.KolSigningFee(d.Id).ToString("N0")+" · 日薪¥"+S.KolDailyWage(d.Id),()=>ConfirmBusiness("直播签约 · "+S.CityPerson(d.Id).Name,"一次支付¥"+S.KolSigningFee(d.Id)+"，独家直播90天，占用1个名额。当前日薪¥"+S.KolDailyWage(d.Id)+"，随粉丝和人气增加，每日结算；暂停仍计薪，解约结清当日。默认18:00至次日02:00，可独立排班。",()=>{S.SignKolLive(brand.Id,d.Id);w.Rebuild();}),17,Ui.ButtonHot,why==null),-1,36).name="KolSign_"+d.Id;
                    if(why!=null)Line(card,why,13,Ui.Dim);
                }
            }
        }
    }
}
