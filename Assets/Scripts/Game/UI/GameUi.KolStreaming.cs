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
        private string _kolPage = "我的代言人", _selectedKolContract;
        private static Material _kolOfflinePortrait;
        private GameSession _kolDraftSession;
        private readonly Dictionary<string,KolLiveContract> _kolScheduleDrafts=new Dictionary<string,KolLiveContract>();
        public void OpenKolManagement(){_brandTab=BrandKolTab;_kolPage="代言人市场";OpenBrandBusiness();}
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
        private void BuildKolBrandPage(Transform parent, Window w, BrandRecord[] brands)
        {
            if (_kolDraftSession != S)
            {
                _kolDraftSession = S;
                _kolScheduleDrafts.Clear();
                _selectedKolContract = null;
            }

            var nav = Ui.Row(parent, 40, 8);
            foreach (var page in new[] { "我的代言人", "代言人市场", "专场记录" })
                Ui.Size(Ui.Button(nav, page, () => { _kolPage = page; w.Rebuild(); }, 18,
                    _kolPage == page ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);
            if (_kolPage == "代言人市场") { BuildKolMarket(parent, w); return; }
            if (_kolPage == "专场记录")
            {
                if (brands.Length == 0)
                {
                    Para(parent, "创立品牌并完成直播后可查看专场记录。", 18);
                    return;
                }
                int index = Math.Max(0, Array.FindIndex(brands, b => b.Id == _brandId));
                var filter = Ui.Row(parent, 38, 8);
                Ui.Size(Ui.Label(filter, "查看品牌", 17), 90, 38);
                Ui.Size(Ui.Choice(filter, brands.Select(b => b.Name), index, i =>
                    { _brandId = brands[i].Id; w.RequestRebuild(); }), 280, 38);
                BuildKolReports(parent, brands[index]);
                return;
            }

            long now = S.Clock.Now.TotalMinutes;
            var contracts = S.KolStreaming.Contracts.Where(c => !c.Ended && c.SignedAt <= now && c.EndAt > now).ToArray();
            if (contracts.Length == 0)
            {
                Para(parent, "还没有签约代言人。可以在代言人市场了解各家MCN。", 19);
                Ui.Size(Ui.Button(parent, "前往代言人市场", () => { _kolPage = "代言人市场"; w.Rebuild(); }, 18, Ui.ButtonHot), -1, 40);
                return;
            }

            var selected = contracts.FirstOrDefault(c => c.Id == _selectedKolContract)
                ?? contracts.FirstOrDefault(c => c.BrandId == _brandId) ?? contracts[0];
            _selectedKolContract = selected.Id;
            var split = Ui.Row(parent, 528, 10);
            var roster = BrandCard(split, "签约代言人 · " + contracts.Length + "人"); Ui.Size(roster, 270, 528);
            var list = Ui.Scroll(roster, 468);
            foreach (var c in contracts)
            {
                var row = Ui.Row(list, 74, 8);
                KolAvatar(row, c.PersonId, 49, 66);
                string brandName = brands.FirstOrDefault(b => b.Id == c.BrandId)?.Name;
                string status = brandName == null ? "待分配品牌" : brandName + " · " + (c.Enabled ? KolFrequency(c.FrequencyDays) : "未启用直播");
                var pick = Ui.Size(Ui.Button(row, S.CityPerson(c.PersonId).Name + "\n" + status, () =>
                    { _selectedKolContract = c.Id; w.Rebuild(); }, 16, c.Id == selected.Id ? Ui.ButtonHot : Ui.ButtonBg), -1, 66, 1);
                pick.name = "KolSelect_" + c.Id;
            }

            var editor = BrandCard(split, S.CityPerson(selected.PersonId).Name + " · 代言与直播"); Ui.Size(editor, -1, 528, 1);
            BuildKolSchedule(editor, w, selected, brands);
        }

        private static string KolFrequency(int days) => days == 7 ? "每周一次" : days == 2 ? "隔天" : "每天";

        private void BuildKolSchedule(Transform body, Window w, KolLiveContract contract, BrandRecord[] brands)
        {
            if (!_kolScheduleDrafts.TryGetValue(contract.Id, out var draft))
            {
                draft = contract.Copy();
                _kolScheduleDrafts[contract.Id] = draft;
            }
            var talent = KolMarket.Get(contract.PersonId);
            Line(body, $"{talent.Topic} · 粉丝 {S.KolFollowers(contract.PersonId):N0} · 人气 {S.KolPopularity(contract.PersonId)}", 16);
            Line(body, $"代言费 {Money.FromCents(contract.FeeCents)}（已一次付清） · 到期 {KolTimestamp(contract.EndAt)}", 17);
            Line(body, $"每场直播费 {Money.FromCents(S.KolLiveSessionFee(contract))} · 品牌场开播时扣款", 17, Ui.Accent);

            var assignment = Ui.Row(body, 39, 8);
            Ui.Size(Ui.Label(assignment, "独家代言品牌", 17), 123, 39);
            int assigned = Array.FindIndex(brands, b => b.Id == draft.BrandId);
            Ui.Size(Ui.Choice(assignment, brands.Select(b => b.Name).Prepend("未分配"), assigned + 1, i =>
            {
                if (i == 0) draft.Enabled = false;
                else if (draft.BrandId == null && contract.ScheduleVersion == 0) draft.Enabled = true;
                draft.BrandId = i == 0 ? null : brands[i - 1].Id;
                w.RequestRebuild();
            }), 240, 39).name = "KolBrandChoice_" + contract.Id;
            if (assigned >= 0)
            {
                var brand = brands[assigned];
                Ui.Size(Ui.Label(assignment, $"{S.BrandLevelName(brand.Id)} · {S.BrandKolCount(brand.Id)}/{S.BrandKolCapacity(brand.Id)} 人",
                    15, color: Ui.Dim), 145, 39);
            }
            else if (brands.Length == 0)
                Ui.Size(Ui.Label(assignment, "创建品牌后即可分配", 15, color: Ui.Dim), 180, 39);

            Line(body, "品牌内容频率", 18, Ui.Accent);
            var cadence = Ui.Row(body, 40, 6);
            foreach (int days in new[] { 1, 2, 7 })
                Ui.Size(Ui.Button(cadence, KolFrequency(days), () => { draft.FrequencyDays = days; w.Rebuild(); },
                    17, draft.FrequencyDays == days ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);

            var timing = Ui.Row(body, 37, 6);
            Ui.Size(Ui.Label(timing, "固定直播时段", 16), 123, 37);
            var times = Enumerable.Range(0, 96).Select(n => KolTime(n * 15)).ToArray();
            Ui.Size(Ui.Choice(timing, times, draft.StartMinute / 15, index => { draft.StartMinute = index * 15; w.RequestRebuild(); }), 105, 37);
            Ui.Size(Ui.Label(timing, "至", 16, TextAnchor.MiddleCenter), 26, 37);
            Ui.Size(Ui.Choice(timing, times, draft.EndMinute / 15, index => { draft.EndMinute = index * 15; w.RequestRebuild(); }), 105, 37);
            if (draft.EndMinute < draft.StartMinute) Ui.Size(Ui.Label(timing, "次日", 15, color: Ui.Dim), 42, 37);

            draft.FirstDay = S.KolBrandScheduleAnchor(contract, draft.StartMinute, draft.FrequencyDays);
            long cursor = S.Clock.Now.TotalMinutes;
            var upcoming = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                long next = S.KolNextSpecialStart(draft, cursor);
                if (next < 0) break;
                upcoming.Add(Ui.WeekdayName(new GameDate((int)(next / 1440)).Weekday) + " " + KolTime((int)(next % 1440)));
                cursor = next + 1;
            }
            Line(body, upcoming.Count == 0 ? "暂无后续品牌直播" : "接下来的品牌直播 · " + string.Join("  ·  ", upcoming), 16, Ui.Good);
            Para(body, "每场至少 3 小时。其他日子同一时段可播个人内容或休息，个人直播不收取品牌直播费。", 16, Ui.Dim);

            string blocker = S.KolScheduleBlocker(contract.Id, draft.BrandId, draft.StartMinute, draft.EndMinute, draft.Enabled, draft.FrequencyDays, draft.FirstDay);
            if (blocker != null) Line(body, blocker, 16, Ui.Warn);
            var actions = Ui.Row(body, 40, 6);
            Ui.Size(Ui.Button(actions, draft.Enabled ? "暂停品牌场" : "启用品牌场", () => { draft.Enabled = !draft.Enabled; w.Rebuild(); }, 16, Ui.ButtonBg, assigned >= 0), 104, 40);
            Ui.Size(Ui.Button(actions, "保存品牌与排班", () =>
            {
                if (S.ConfigureKolSchedule(contract.Id, draft.BrandId, draft.StartMinute, draft.EndMinute, draft.Enabled, draft.FrequencyDays, draft.FirstDay))
                {
                    _kolScheduleDrafts.Remove(contract.Id);
                    Toast("已保存代言品牌和直播排班。");
                }
                w.Rebuild();
            }, 17, Ui.ButtonHot, blocker == null), -1, 40, 1).name = "KolSaveSchedule_" + contract.Id;
            var room = S.GetKolRoom(contract.PersonId, false);
            Ui.Size(Ui.Button(actions, "进入直播间", () =>
            {
                w.Close();
                OpenKolLiveRoom(contract.Id);
            }, 16, Ui.ButtonBg, room.IsLive), 124, 40).name = "KolWatch_" + contract.Id;
            Ui.Size(Ui.Button(actions, "解除合约…", () =>
                ConfirmBusiness("解除代言合约", "释放签约名额，已付签约费不退。", () =>
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
                Line(details, $"直播 {latest.LiveMinutes} 分钟 · 峰值在线 {latest.PeakViewers:N0} 人 · 直播费 {Money.FromCents(latest.FeeCents)}", 17);
                if (records.Length > 1) Line(parent, "历史专场", 18, Ui.Accent);
            }
            foreach (var session in records.Skip(latest != null && latest.InfluenceAfter > latest.InfluenceBefore ? 1 : 0).Take(19))
            {
                var card = BrandCard(parent, "");
                var row = Ui.Row(card, 90, 10);
                KolAvatar(row, session.PersonId, 60, 80);
                var details = Ui.Rect(row, "KolReport"); Ui.Size(details, -1, 90, 1); Ui.VList(details, 4, 0);
                Line(details, S.CityPerson(session.PersonId).Name + " · " + KolTimestamp(session.EndsAt), 18, Ui.Accent);
                Line(details, $"直播 {session.LiveMinutes} 分钟 · 峰值在线 {session.PeakViewers:N0} 人 · 直播费 {Money.FromCents(session.FeeCents)}", 16);
                string effects = session.LegacyMinutes == session.LiveMinutes ? "旧版直播记录"
                    : session.InfluenceAfter == session.InfluenceBefore
                    ? "本场未新增品牌收益"
                    : $"品牌知名度 {session.InfluenceBefore} → {session.InfluenceAfter}   ·   品牌口碑 {session.ReputationBefore} → {session.ReputationAfter}";
                Line(details, effects, 17, Ui.Good);
            }
            Para(parent, "直播收益计入品牌，同品类自营门店共享品牌知名度和口碑。", 16, Ui.Dim);
        }

        private void BuildKolBrandResult(Transform parent, KolLiveSession result, bool rival = false)
        {
            if (result.InfluenceAfter == result.InfluenceBefore && result.ReputationAfter == result.ReputationBefore)
            {
                string message = result.LiveMinutes - result.LegacyMinutes < 15
                    ? "有效直播不足 15 分钟，本场品牌知名度和口碑均未变化。"
                    : "本场品牌知名度和口碑均未变化。";
                Para(parent, message + " 基础客流加成无变化。", 17, Ui.Dim);
                return;
            }
            var highlights = Ui.Row(parent, 124, 8);
            KolReportMetric(highlights, "品牌知名度", result.InfluenceBefore, result.InfluenceAfter);
            KolReportMetric(highlights, "品牌口碑", result.ReputationBefore, result.ReputationAfter);
            double before = GameSession.BrandStandingTraffic(result.InfluenceBefore, result.ReputationBefore);
            double after = GameSession.BrandStandingTraffic(result.InfluenceAfter, result.ReputationAfter);
            var traffic = BrandCard(highlights, rival ? "基础客流加成（参考）" : "门店基础客流加成");
            Ui.Size(traffic, 0, 124, 1);
            Line(traffic, $"{before:0.0%} → {after:0.0%}", 22, Ui.Good);
            Line(traffic, $"本场变化 {(after - before) * 100:+0.0;-0.0;0.0} 个百分点", 15, Ui.Dim);
            Para(parent, rival ? "以该品牌的知名度和口碑折算，供参考。"
                : "随品牌知名度和口碑持续生效。实际进店人数还受门店营业、地段等因素影响。", 16, Ui.Dim);
        }

        private void KolReportMetric(Transform row, string name, int before, int after)
        {
            var card = BrandCard(row, name); Ui.Size(card, 0, 124, 1);
            Line(card, $"{before} → {after}   (+{after - before})", 25, Ui.Good);
            Line(card, "已计入品牌", 15, Ui.Dim);
        }

        private void BuildKolMarket(Transform parent, Window w)
        {
            Para(parent,"到MCN付介绍费拿名片，和主播本人谈合约。签约后在“我的代言人”分配品牌。",17,Ui.Dim);
            Line(parent,"三家公司共用社区MCN联合接待处 · 09:00—18:00",16,Ui.Dim);
            foreach(var agency in BrandAgency.All)
            {
                var company=agency;
                var card=BrandCard(parent,company.Name+" · "+company.Specialty);
                Para(card,company.Description,16);
                var people=KolMarket.All.Where(k=>BrandAgency.ForPerson(k.Id).Id==company.Id).ToArray();
                var preview=Ui.Row(card,70,8);
                foreach(var person in people.Take(3))KolAvatar(preview,person.Id,50,68);
                Line(preview,string.Join("、",people.Take(3).Select(p=>S.CityPerson(p.Id).Name))
                    +(people.Length>3?"等":"")+" · "+people.Length+"位主播",17,Ui.Accent);
                var actions=Ui.Row(card,38,8);
                Ui.Size(Ui.Button(actions,"了解旗下主播",()=>
                {
                    _mcnAgencyId = company.Id; w.Close(); OpenCommunityMcn();
                },17),-1,38,1).name="KolAgencyPreview_"+company.Id;
                Ui.Size(Ui.Button(actions,"前往"+company.Name+"接待处",()=>
                {
                    GoToBrandAgency(company.Id);
                },17,Ui.ButtonHot),-1,38,1).name="KolAgencyVisit_"+company.Id;
            }
        }
    }
}
