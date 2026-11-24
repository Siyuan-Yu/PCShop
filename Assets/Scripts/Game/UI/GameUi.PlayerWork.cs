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
        private bool _workScreenForWeekend;
        private readonly List<(Button button, int speed)> _workSpeedButtons = new List<(Button, int)>();
        private readonly List<string> _workMessages = new List<string>();
        private EventDef _workEvent;
        public bool WorkScreenVisible => _workScreen != null && _workScreen.gameObject.activeSelf;

        private void BuildPlayerWorkEntry(Transform parent, Place p, Window placeWindow)
        {
            if (!S.OffersPlayerWork(p) && S.WorkPlace != p) return;
            if (S.HasWorkContract && S.WorkPlace == p)
            {
                Ui.Size(Ui.Button(parent, "我的打工合同 · " + S.WorkHours,
                    () => OpenPlayerWork(p, placeWindow), 17, Ui.ButtonBg), -1, 40);
            }
            else if (!S.HasWorkContract && S.OffersPlayerWork(p))
            {
                var offer = S.PlayerWorkFor(p);
                var row = Ui.Row(parent, 40, 12);
                Ui.Size(Ui.Button(row, "在这里打工", () => OpenPlayerWork(p, placeWindow), 18, Ui.ButtonHot), 136, 40);
                Ui.Size(Ui.Label(row, $"{offer.Title} · ¥{S.PlayerWorkWage(p, offer):N0}/班", 17, TextAnchor.MiddleLeft, Ui.Accent), flexWidth: 1);
            }
        }

        public void OpenPlayerWork(Place p, Window parent = null)
        {
            OpenWindow("打工 · " + p.Name, 980, 630, w =>
            {
                if (S.HasWorkContract)
                {
                    BuildPlayerWorkSummary(w.Content, w);
                    if (!S.WorkContract.ReadyToPay && S.Clock.Today.TotalDays < S.WorkContract.StartDay)
                        Para(w.Content, GameSession.SupplierClosesOnWeekend(S.WorkPlace) ? "按日程到店，进入签约店铺就会自动上班。"
                            : "明天再来工作。排班开始后，进入签约店铺就会自动上班，不用再点开始。", 20, Ui.Accent);
                    return;
                }
                BuildPlayerWorkOffer(w, p, parent);
            }, scrollContent: false);
        }

        private void BuildPlayerWorkOffer(Window w, Place place, Window placeWindow)
        {
            var offer = S.PlayerWorkFor(place);
            long wage = S.PlayerWorkWage(place, offer);
            var impact = S.PlayerWorkImpact(place, wage, offer.End);
            bool weekdaysOnly = GameSession.SupplierClosesOnWeekend(place);
            var days = Enumerable.Range(1, 7).Select(S.Clock.Today.AddDays).ToArray();
            int firstShift = System.Array.FindIndex(days, day => !weekdaysOnly || !day.IsWeekend);
            int shifts = weekdaysOnly ? 5 : days.Length;
            string firstShiftText = firstShift == 0 ? "明天首班" : Ui.WeekdayName(days[firstShift].Weekday) + "首班";

            var overview = Ui.Size(Ui.FlatPanel(w.Content, "WorkOffer", Ui.FlatWell, Ui.FlatWellEdge), -1, 146).rectTransform;
            var metrics = Ui.Row(overview, 0, 18).Fill(16);
            long raise = wage - offer.Wage;
            string payDetail = $"岗位底薪 ¥{offer.Wage:N0} · {impact.attribute}加薪 ¥{raise:N0}";
            BuildWorkOfferMetric(metrics, "每班底薪", $"¥{wage:N0}", payDetail, 44);
            var divider = Ui.Size(Ui.Panel(metrics, "WorkOfferDivider", Ui.Edge), 1);
            divider.raycastTarget = false;
            BuildWorkOfferMetric(metrics, offer.Title, offer.Hours, firstShiftText + " · 当天收工到账", 33);

            var aptitude = Ui.Size(Ui.FlatPanel(w.Content, "WorkAptitude", Ui.SelectedButton(true)), -1, 50).rectTransform;
            var aptitudeRow = Ui.Row(aptitude, 0, 10).Fill(10);
            Ui.Size(Ui.Label(aptitudeRow, "岗位主要看", 17, color: Ui.Good), 90);
            var current = Ui.Size(Ui.Label(aptitudeRow, $"{impact.attribute} {impact.score}", 23, color: Ui.Good), 0, -1, 1);
            current.fontStyle = FontStyle.Bold;
            Ui.Size(Ui.Label(aptitudeRow, $"预计表现奖金 +¥{impact.bonus:N0} / 班", 17, TextAnchor.MiddleRight, Ui.Good), 230);

            var schedule = Ui.Row(w.Content, 32, 10);
            Ui.Size(Ui.Label(schedule, "一周排班", 20), 0, -1, 1).fontStyle = FontStyle.Bold;
            Ui.Size(Ui.Label(schedule, $"{shifts} 班底薪合计 <b>¥{wage * shifts:N0}</b>", 18, TextAnchor.MiddleRight, Ui.Accent), 320);
            var scheduleDays = Ui.Row(w.Content, 62, 7);
            for (int i = 0; i < days.Length; i++)
            {
                bool rest = weekdaysOnly && days[i].IsWeekend;
                bool first = i == firstShift;
                var tile = Ui.Size(Ui.FlatPanel(scheduleDays, "WorkDay_" + i, first ? Ui.SelectedButton(true) : Ui.FlatBand), 0, -1, 1);
                tile.raycastTarget = false;
                string note = rest ? "\n<size=14>休息</size>" : first ? i == 0 ? "\n<size=14>明天</size>" : "\n<size=14>首班</size>" : "";
                var label = Ui.Label(tile.transform, "<b>" + Ui.WeekdayName(days[i].Weekday) + "</b>" + note, 20, TextAnchor.MiddleCenter,
                    first ? Ui.Good : rest ? Ui.Dim : Ui.TextColor);
                label.rectTransform.Fill(3);
            }

            var effects = Ui.Row(w.Content, 38, 10);
            Ui.Size(Ui.Label(effects, "上满一班", 17, color: Ui.Dim), 103);
            double stress = impact.stressPerHour * (offer.End - offer.Start) / 60;
            foreach (string effect in new[] { $"压力 +{stress:0.#}", $"健康 −{offer.HealthCost}", $"心情 −{offer.MoodCost}" })
            {
                var chip = Ui.Size(Ui.FlatPanel(effects, "WorkEffect", Ui.WarningSurface, Ui.WarningSurface), 128, 34);
                chip.raycastTarget = false;
                Ui.Label(chip.transform, effect, 17, TextAnchor.MiddleCenter, Ui.Warn).rectTransform.Fill(2);
            }
            Ui.Size(Ui.Label(w.Content, "提供工作餐和收工餐 · 到店自动上班", 17, color: Ui.Dim), -1, 30);

            var spacer = Ui.Size(Ui.Rect(w.Content, "WorkFooterSpacer"));
            spacer.GetComponent<LayoutElement>().flexibleHeight = 1;
            Ui.Size(Ui.Panel(w.Content, "WorkFooterDivider", Ui.Edge), -1, 1).raycastTarget = false;
            var footer = Ui.Row(w.Content, 52, 10);
            RectTransform helpCard = null;
            Ui.Size(Ui.Button(footer, "?  出勤说明", () => helpCard.gameObject.SetActive(!helpCard.gameObject.activeSelf), 17), 145, 42)
                .name = "WorkAttendanceHelpButton";
            Ui.Size(Ui.Rect(footer, "WorkActionSpacer"), 0, -1, 1);
            bool canSign = S.CurrentPlace == place && S.OffersPlayerWork(place) && (!weekdaysOnly || S.IsWholesalerOpen);
            Ui.Size(Ui.Button(footer, "签一周合同", () =>
            {
                if (!S.SignPlayerWork(place)) return;
                w.Rebuild();
                placeWindow?.RequestRebuild();
            }, 21, Ui.ButtonHot, canSign), 220, 48).name = "ContractSign";

            long lateFee = PlayerWorkContract.LatePenaltyForWage(wage, offer.End - offer.Start);
            helpCard = Ui.FlatPanel(w.Content, "WorkAttendanceHelp", Ui.FlatWell, Ui.FlatWellEdge).rectTransform;
            helpCard.Anchor(0, 0, 0, 0, 16, 77, -570, -190);
            helpCard.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Ui.VList(helpCard, 6, 13);
            Ui.Size(Ui.Label(helpCard, $"<b>迟到</b>　每 30 分钟扣底薪 ¥{lateFee:N0}，不足 30 分钟按一档计。\n　　　　 表现奖金按比例减少。", 16, TextAnchor.MiddleLeft), -1, 54);
            Ui.Size(Ui.Label(helpCard, "<b>缺勤</b>　当天无工资。", 16), -1, 26);
            helpCard.gameObject.SetActive(false);
        }

        private static void BuildWorkOfferMetric(Transform parent, string title, string value, string detail, int valueSize)
        {
            var metric = Ui.Size(Ui.Rect(parent, "WorkOfferMetric"), 0, -1, 1);
            Ui.VList(metric, 1, 0).childAlignment = TextAnchor.MiddleLeft;
            Ui.Size(Ui.Label(metric, title, 18, color: Ui.Accent), -1, 25);
            Ui.Size(Ui.Label(metric, value, valueSize, color: Ui.Accent), -1, 51).fontStyle = FontStyle.Bold;
            Ui.Size(Ui.Label(metric, detail, 17, color: Ui.Dim), -1, 26);
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
                    _workScreen = Ui.Panel(_modalCanvas.transform, "PlayerWorkScreen", Ui.ChromeWell);
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
                        var button = Ui.Size(Ui.Button(row, TimeController.SpeedLabel(mode), () => S.Time.SetSpeed(mode), 21), -1, -1, 1);
                        _workSpeedButtons.Add((button, mode));
                    }
                    Para(content, "打工中暂不能进行其他操作。可点击倍速，或按数字键 1/2/3/4；下班后自动恢复操作。", 17, Ui.Dim);
                }
                _workScreen.transform.SetAsLastSibling();
                foreach (var (button, speed) in _workSpeedButtons)
                    Ui.SetButtonFill(button, Ui.SelectedButton(S.Time.Speed == speed));
                _workScreenForWeekend = S.IsWeekendWorking;
                if (_workScreenForWeekend)
                {
                    var end = S.WeekendWorkEnds;
                    _workStatus.text = $"打工中 · 周末搬货\n预计 {end.Hour:00}:{end.Minute:00} 收工 · 工资 ¥{GameSession.WeekendWorkPay}\n现在 {S.Clock.Now} · 还剩 {S.WorkMinutesLeft} 分钟\n当前 {TimeController.SpeedLabel(S.Time.Speed)}";
                    return;
                }
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
                _workSpeedButtons.Clear();
                if (!_workScreenForWeekend) Toast(S.WorkContract?.LastNote ?? "今天收工了。");
            }
            if (_workEvent != null) { var e = _workEvent; _workEvent = null; ShowEvent(e); }
            if (_workMessages.Count > 0)
            {
                Popup("打工期间的消息", string.Join("\n", _workMessages.Take(8)) + "\n详细内容可在通知记录查看。"); _workMessages.Clear();
            }
        }
    }
}
