using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Image _workScreen;
        private Text _workStatus;
        private readonly List<string> _workMessages = new List<string>();
        private EventDef _workEvent;
        public bool WorkScreenVisible => _workScreen != null && _workScreen.gameObject.activeSelf;

        private void BuildPlayerWorkEntry(Transform parent, Place p, Window placeWindow)
        {
            if (!S.OffersPlayerWork(p) && S.WorkPlace != p) return;
            if (S.HasWorkContract && S.WorkPlace == p)
            {
                Ui.Size(Ui.Button(parent, "我的打工合同 · " + S.WorkHours,
                    () => OpenPlayerWork(p, placeWindow), 17, Ui.ButtonHot), -1, 40);
            }
            else if (!S.HasWorkContract && S.OffersPlayerWork(p))
            {
                var offer = S.PlayerWorkFor(p);
                Ui.Size(Ui.Button(parent, "在这里打工 · " + offer.Title + " · ¥" + offer.Wage + "/天", () => OpenPlayerWork(p, placeWindow), 17, Ui.ButtonHot), -1, 40);
            }
        }

        public void OpenPlayerWork(Place p, Window parent = null)
        {
            OpenWindow("打工 · " + p.Name, 780, 720, w =>
            {
                if (S.HasWorkContract)
                {
                    BuildPlayerWorkSummary(w.Content, w);
                    if (!S.WorkContract.ReadyToPay && S.Clock.Today.TotalDays < S.WorkContract.StartDay)
                        Para(w.Content, GameSession.SupplierClosesOnWeekend(S.WorkPlace) ? "按日程到店，进入签约店铺就会自动上班。"
                            : "明天再来工作。排班开始后，进入签约店铺就会自动上班，不用再点开始。", 20, Ui.Accent);
                    return;
                }
                var offer = S.PlayerWorkFor(p);
                long rate = (long)System.Math.Ceiling(offer.Wage * 30.0 / (offer.End - offer.Start));
                bool weekdaysOnly = GameSession.SupplierClosesOnWeekend(p);
                string shifts = weekdaysOnly ? $"周一至周五 {offer.Hours}，一周 5 班，明天起算。" : $"每天 {offer.Hours}，连续 7 班，明天开始。";
                Para(w.Content, $"{offer.Title}\n{shifts}\n底薪 ¥{offer.Wage} / 班 · 收工自动到账 · 提供工作餐和收工餐", 20, Ui.Accent);
                var impact = S.PlayerWorkImpact(p, offer.Wage, offer.End);
                var preview = Ui.Panel(w.Content, "WorkImpact", Ui.PanelBg2).rectTransform;
                Ui.VList(preview, 4, 10);
                Line(preview, $"主要属性 · {impact.attribute} {impact.score}   表现奖金 +¥{impact.bonus} / 班", 20, Ui.Accent);
                double stress = impact.stressPerHour * (offer.End - offer.Start) / 60;
                string energy = impact.energyMultiplier == 1 ? "精力按平常速度消耗" : $"精力消耗 {(impact.energyMultiplier - 1) * 100:+0.#;-0.#}%";
                Para(preview, $"完整一班 · 压力 +{stress:0.#} · {energy} · 收工健康 −{offer.HealthCost}、心情 −{offer.MoodCost}", 17);
                var penalties = new List<string>();
                if (impact.fatiguePenalty > 0) penalties.Add($"疲劳使奖金减少 {impact.fatiguePenalty}%");
                if (impact.stressPenalty > 0) penalties.Add($"高压力使奖金减少 {impact.stressPenalty}%");
                if (penalties.Count > 0) Line(preview, string.Join(" · ", penalties), 17, Ui.Warn);
                Para(preview, $"主属性越高，当班压力越少。超过 {GameSession.WorkBonusThreshold} 可获奖金，最高为底薪的 25%。到岗精力低于 {PlayerStats.TiredEnergyThreshold:0}，奖金减 {GameSession.FatigueWorkPenalty}%。压力达到 {PlayerStats.HighStressThreshold:0}，奖金减 {GameSession.HighStressWorkPenalty}%。", 16, Ui.Dim);
                Para(w.Content, "到店自动上班，开工前 1 小时起可到岗。夜班可凭合同进店，上班时可调整倍速。", 17);
                Para(w.Content, $"迟到每 30 分钟扣底薪 ¥{rate}，奖金按比例减少。缺勤当天无工资。", 17, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, weekdaysOnly ? "签一周合同" : "签一周合同（明天开始）",
                    () => { if (S.SignPlayerWork(p)) { w.Rebuild(); parent?.RequestRebuild(); } }, 20, Ui.ButtonHot,
                    S.CurrentPlace == p && S.OffersPlayerWork(p) && (!weekdaysOnly || S.IsWholesalerOpen)), -1, 50);
            });
        }

        private void BuildPlayerWorkSummary(Transform parent, Window window = null)
        {
            var c = S.WorkContract;
            if (c == null) return;
            Line(parent, "打工合同 · " + (S.WorkPlace?.Name ?? "店铺"), 20, Ui.Accent);
            string workdays = GameSession.SupplierClosesOnWeekend(S.WorkPlace) ? "周一至周五" : "每天";
            Para(parent, $"第 {c.StartDay + 1}–{c.EndDay + 1} 天，{workdays} {S.WorkHours} · 底薪 ¥{c.DailyWageYuan} / 班\n已出勤 {c.Days.Count(d => d.State == WorkDayState.Completed)}/{c.ScheduledShiftCount} 天，缺勤 {c.Days.Count(d => d.State == WorkDayState.Absent)} 天\n已到账 ¥{c.Days.Where(d => d.WagePaid).Sum(d => d.EarnedYuan)}（含奖金 ¥{c.Days.Where(d => d.WagePaid).Sum(d => d.BonusYuan)}） · 已扣 ¥{c.DeductionsYuan}", 18);
            Para(parent, c.LastNote, 16, Ui.Dim);
            if (c.Paid) Para(parent, "合同已结束，工资已全部到账，可以再签一周合同。", 16, Ui.Good);
            else Para(parent, "每天自行按时到店，进入签约店铺自动上班；收工后工资自动到账，剩余排班见“日程”。", 16, Ui.Dim);
        }

        private void UpdateWorkScreen()
        {
            if (S.IsWorking)
            {
                // No Window/Hold: the overlay blocks input while the normal world clock keeps running.
                if (_workScreen == null)
                {
                    CloseAllWindows();
                    S.Time.SetPaused(false);
                    _workScreen = Ui.Panel(_modalCanvas.transform, "PlayerWorkScreen", new Color(0.05f, 0.1f, 0.09f, 0.98f));
                    _workScreen.rectTransform.Fill();
                    var panel = Ui.Panel(_workScreen.transform, "WorkPanel", Ui.PanelBg);
                    panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    panel.rectTransform.sizeDelta = new Vector2(820, 470);
                    panel.gameObject.AddComponent<WindowFit>().Fit();
                    var content = Ui.Rect(panel.transform, "Content"); content.Fill(26);
                    var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                    layout.spacing = 18; layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false;
                    _workStatus = Ui.Label(content, "", 23, TextAnchor.UpperLeft, Ui.Accent);
                    Ui.Size(_workStatus, -1, 240);
                    var row = Ui.Row(content, 52);
                    foreach (int speed in TimeController.Speeds)
                    {
                        int mode = speed;
                        Ui.Size(Ui.Button(row, TimeController.SpeedLabel(mode), () => S.Time.SetSpeed(mode), 21, Ui.ButtonHot), -1, -1, 1);
                    }
                    Para(content, "打工中暂不能进行其他操作。可点击倍速，或按数字键 1/2/3/4；下班后自动恢复操作。", 17, Ui.Dim);
                }
                _workScreen.transform.SetAsLastSibling();
                var c = S.WorkContract;
                long start = (long)(c.StartDay + c.WorkingIndex) * 1440 + c.ShiftStart;
                string state = S.Clock.Now.TotalMinutes < start ? "已到岗，等待开工" : "打工中";
                int shiftNumber = c.Days.Take(c.WorkingIndex + 1).Count(d => d.State != WorkDayState.Rest);
                _workStatus.text = $"{state} · {S.WorkPlace?.Name} · {c.JobTitle}\n第 {shiftNumber}/{c.ScheduledShiftCount} 个工作日 · {S.WorkHours}\n现在 {S.Clock.Now} · 约 {S.WorkMinutesLeft} 分钟后收工\n今日迟到 {c.Days[c.WorkingIndex].LateMinutes} 分钟 · 表现奖金 ¥{c.Days[c.WorkingIndex].BonusYuan}（扣款前）\n累计已到账 ¥{c.Days.Where(d => d.WagePaid).Sum(d => d.EarnedYuan)} · 当前 {TimeController.SpeedLabel(S.Time.Speed)}\n健康 {S.Player.Health:0} · 心情 {S.Player.Mood:0} · 压力 {S.Player.Stress:0}/100";
                return;
            }
            if (_workScreen != null)
            {
                Kill.It(_workScreen.gameObject); _workScreen = null;
                Toast(S.WorkContract?.LastNote ?? "今天收工了。");
            }
            if (_workEvent != null) { var e = _workEvent; _workEvent = null; ShowEvent(e); }
            if (_workMessages.Count > 0)
            {
                Popup("打工期间的消息", string.Join("\n", _workMessages.Take(8)) + "\n详细内容可在通知记录查看。"); _workMessages.Clear();
            }
        }
    }
}
