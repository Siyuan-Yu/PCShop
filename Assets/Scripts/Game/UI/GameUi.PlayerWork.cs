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
                        Para(w.Content, "明天再来工作。排班开始后，进入签约店铺就会自动上班，不用再点开始。", 20, Ui.Accent);
                    return;
                }
                var offer = S.PlayerWorkFor(p);
                long rate = (long)System.Math.Ceiling(offer.Wage * 30.0 / (offer.End - offer.Start));
                Para(w.Content, $"{offer.Title}\n每天 {offer.Hours}，连续 7 班，明天开始。\n日薪 ¥{offer.Wage}，每天收工后自动到账，无需押金。店里提供免费工作餐和收工餐。", 20, Ui.Accent);
                Para(w.Content, "每天自行到这家店。开工前 1 小时起可到岗；进入后自动上班，提前到会等到开工。夜间打烊时也能凭合同进店；下班前不能离店、购物或管理其他事项，只能调整时间倍速。", 18);
                Para(w.Content, $"迟到每 30 分钟扣当天工资 ¥{rate}，不足 30 分钟按 30 分钟计，最多扣完当日工资；缺勤扣完当日工资，不额外欠钱。", 18);
                Para(w.Content, $"每天收工：健康 −{offer.HealthCost}、心情 −{offer.MoodCost}。夜班可以白天补觉，最长 8 小时，上班前 1 小时醒。工资扣除当天迟到罚款后自动到账，无需领取。", 18, Ui.Warn);
                Ui.Size(Ui.Button(w.Content, "签一周合同（明天开始）", () => { if (S.SignPlayerWork(p)) { w.Rebuild(); parent?.RequestRebuild(); } }, 20, Ui.ButtonHot, S.CurrentPlace == p && S.OffersPlayerWork(p)), -1, 50);
            });
        }

        private void BuildPlayerWorkSummary(Transform parent, Window window = null)
        {
            var c = S.WorkContract;
            if (c == null) return;
            Line(parent, "打工合同 · " + (S.WorkPlace?.Name ?? "店铺"), 20, Ui.Accent);
            Para(parent, $"第 {c.StartDay + 1}–{c.EndDay + 1} 天，每天 {S.WorkHours} · 日薪 ¥{c.DailyWageYuan}，每天收工自动结算\n已出勤 {c.Days.Count(d => d.State == WorkDayState.Completed)}/7 天，缺勤 {c.Days.Count(d => d.State == WorkDayState.Absent)} 天\n已到账工资 ¥{c.Days.Where(d => d.WagePaid).Sum(d => d.EarnedYuan)}，已扣 ¥{c.DeductionsYuan}", 18);
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
                        Ui.Size(Ui.Button(row, mode + "×", () => S.Time.SetSpeed(mode), 21, Ui.ButtonHot), -1, -1, 1);
                    }
                    Para(content, "打工中暂不能进行其他操作。可点击倍速，或按数字键 1/2/3/4；下班后自动恢复操作。", 17, Ui.Dim);
                }
                _workScreen.transform.SetAsLastSibling();
                var c = S.WorkContract;
                long start = (long)(c.StartDay + c.WorkingIndex) * 1440 + c.ShiftStart;
                string state = S.Clock.Now.TotalMinutes < start ? "已到岗，等待开工" : "打工中";
                _workStatus.text = $"{state} · {S.WorkPlace?.Name} · {c.JobTitle}\n第 {c.WorkingIndex + 1}/7 个工作日 · {S.WorkHours}\n现在 {S.Clock.Now} · 约 {S.WorkMinutesLeft} 分钟后收工\n今日迟到 {c.Days[c.WorkingIndex].LateMinutes} 分钟\n累计已到账 ¥{c.Days.Where(d => d.WagePaid).Sum(d => d.EarnedYuan)} · 当前 {S.Time.Speed}×\n健康 {S.Player.Health:0} · 心情 {S.Player.Mood:0}（收工 −{c.HealthCost}/−{c.MoodCost}）";
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
