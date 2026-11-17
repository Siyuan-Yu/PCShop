using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private long _loanAmountYuan = 1000;
        private int _loanMonths = 1;
        private const string LoanInterestRules = "月息按原本金算单利。每 30 天为一个计息月，不足一个月按一个月。提前结清只付已用月份的利息。";
        private const float LoanReminderHeight = 96f;
        private RectTransform _loanReminderHud;
        private Text _loanReminderTitle, _loanReminderDue, _loanReminderCash;
        private (GameSession Session, int Day, int LedgerRevision, bool AtBank) _loanReminderStamp;
        private bool _loanReminderHasPending;

        private void BuildLoanReminderHud(Transform parent)
        {
            var button = Ui.Button(parent, "", OpenLoanReminder, 18, Ui.PanelBg);
            button.name = "LoanReminderHud";
            _loanReminderHud = (RectTransform)button.transform;
            _loanReminderHud.anchorMin = _loanReminderHud.anchorMax = _loanReminderHud.pivot = Vector2.one;
            _loanReminderHud.sizeDelta = new Vector2(UpcomingScheduleWidth, LoanReminderHeight);
            _loanReminderTitle = Ui.Label(_loanReminderHud, "", 18, color: Ui.Accent);
            _loanReminderTitle.rectTransform.Anchor(0, 1, 1, 1, 14, -32, 32, 6);
            var arrow = Ui.Label(_loanReminderHud, "›", 20, TextAnchor.MiddleRight, Ui.Dim);
            arrow.rectTransform.Anchor(1, 1, 1, 1, -30, -32, 14, 6);
            _loanReminderDue = Ui.Label(_loanReminderHud, "", 17);
            _loanReminderDue.rectTransform.Anchor(0, 1, 1, 1, 14, -61, 14, 33);
            _loanReminderCash = Ui.Label(_loanReminderHud, "", 17);
            _loanReminderCash.rectTransform.Anchor(0, 1, 1, 1, 14, -88, 14, 62);
            _loanReminderHud.gameObject.SetActive(false);
        }

        private void UpdateLoanReminderHud()
        {
            int today = S.Clock.Today.TotalDays;
            var stamp = (S, today, S.Ledger.Revision, S.AtBank);
            if (!_loanReminderStamp.Equals(stamp))
            {
                _loanReminderStamp = stamp;
                RefreshLoanReminder(today);
            }

            bool visible = _loanReminderHasPending && !MainMenuOpen && !S.IsGameOver && !ExpoHudOpen;
            if (_loanReminderHud.gameObject.activeSelf != visible)
                _loanReminderHud.gameObject.SetActive(visible);
        }

        private void RefreshLoanReminder(int today)
        {
            int count = 0;
            int dueDay = int.MaxValue;
            Money due = Money.Zero;
            Money payoff = Money.Zero;
            foreach (var loan in S.AppLoans)
            {
                if (loan.Repaid) continue;
                count++;
                if (loan.DueDay < dueDay)
                {
                    dueDay = loan.DueDay;
                    due = loan.TotalDue;
                    payoff = S.LoanPayoff(loan);
                }
                else if (loan.DueDay == dueDay)
                {
                    due += loan.TotalDue;
                    payoff += S.LoanPayoff(loan);
                }
            }
            _loanReminderHasPending = count > 0;
            if (!_loanReminderHasPending) return;

            Money shortfall = payoff - S.Ledger.Balance;
            string action = S.AtBank || S.BankPlace == null ? "查看贷款" : "前往银行";
            _loanReminderTitle.text = count == 1 ? $"还款提醒 · {action}" : $"{count} 笔待还 · {action}";
            _loanReminderDue.text = $"最近到期 {due} · {LoanCountdown(dueDay - today)}";
            _loanReminderCash.text = $"今日结清 {payoff}" + (shortfall.IsPositive ? $" · 差 {shortfall}" : "");
            _loanReminderCash.color = shortfall.IsPositive ? Ui.Warn : Ui.Good;
        }

        private static string LoanCountdown(int days) => days <= 0 ? "今天还款" : $"还剩 {days} 天";

        private void OpenLoanReminder()
        {
            if (S.AtBank)
            {
                if (!AnyModalOpen || _windows.LastOrDefault() == _bankWindow)
                    OpenBank(S.CurrentPlace, BankPage.Loans);
                return;
            }
            if (AnyModalOpen) return;
            if (S.BankPlace != null) _ctrl.GoToPlace(S.BankPlace);
            else OpenLoansApp();
        }

        private void OpenLoansApp()
        {
            string blocker = S.AppBlocker(PhoneApps.Loans);
            if (blocker != null)
            {
                Toast(S.HasSmartphone ? blocker : "没有智能手机，借款和还款请到「城郊银行」柜台办理。");
                return;
            }
            string amountDraft = _loanAmountYuan.ToString();
            OpenPhoneAppWindow("周转借款", w => BuildLoanDesk(w, amountDraft, text => amountDraft = text));
        }

        private AppLoan LoanQuote(long yuan, int months) => new AppLoan
        {
            PrincipalCents = Money.FromYuan(yuan).Cents,
            StartDay = S.Clock.Today.TotalDays,
            Months = months
        };

        private IEnumerable<AppLoan> LoanRecords() => S.AppLoans.OrderBy(l => l.Repaid)
            .ThenBy(l => l.Repaid ? 0 : l.DueDay).ThenByDescending(l => l.Id);

        private void ConfirmLoanApplication(AppLoan quote, bool atBank, Action completed)
        {
            if (OpeningMealRestricted) return;
            var session = S;
            var application = session.BeginLoanApplication(quote.Principal.Yuan, quote.Months, atBank);
            if (application == null) return;
            AvatarPreview preview = null;
            const float motionSeconds = .56f;
            LoanGesture? moving = null;
            float motionStart = 0;
            string feedback = "";
            bool correct = false;

            OpenWindow(atBank ? "柜台借款 · 动作验证" : "手机借款 · 动作验证", 760, 570, w =>
            {
                Line(w.Content, $"预计到账 {quote.Principal} · 期限 {quote.Months} 个月 · 每月利息 {quote.MonthlyInterest}", 20, Ui.Accent);
                Para(w.Content, $"首月内结清 {session.LoanPayoff(quote)}。{new GameDate(quote.DueDay)} 到期自动扣除本息 {quote.TotalDue}，总余额不足可扣为负数。", 16, Ui.Dim);

                var row = Ui.Row(w.Content, 244, 18);
                var frame = Ui.Size(Ui.FlatPanel(row, "LoanVerificationPreview", Ui.FlatWell, Ui.FlatHotEdge), 192, 244);
                frame.raycastTarget = true;
                if (CanAnimateUi && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    if (preview == null)
                    {
                        preview = new AvatarPreview(verification: true);
                        preview.PrepareVerification(session.Outfit.Id);
                    }
                    var view = Ui.Rect(frame.transform, "LoanVerificationAvatar").Fill(2).gameObject.AddComponent<RawImage>();
                    view.texture = preview.Texture;
                    view.raycastTarget = false;
                }
                var instructions = Ui.Size(Ui.Rect(row, "LoanVerificationInstructions"), 0, 244, 1);
                Ui.VList(instructions, 8, 8);
                var progress = Line(instructions, "", 16, Ui.Accent);
                var prompt = Line(instructions, "", 28);
                var hint = Para(instructions, "", 17, Ui.Dim);
                var response = Para(instructions, "", 17, Ui.Good);
                var gestures = Ui.Row(w.Content, 43, 8);
                var buttons = new List<Button>();
                var actions = Ui.Row(w.Content, 46, 8);
                Ui.Size(Ui.Button(actions, "取消申请", w.Close, 17), 150, 46);
                var submit = Ui.Size(Ui.Button(actions, $"确认借款 · 到账 {quote.Principal}", null, 19, Ui.ButtonHot), 0, 46, 1);
                submit.onClick.AddListener(() =>
                {
                    if (w.Closed || moving.HasValue || S != session) return;
                    if (!session.BorrowAppLoan(application))
                    {
                        feedback = session.AppLoanOfferBlocker(application.AmountYuan, application.Months, atBank) ?? "请重新发起借款申请。";
                        correct = false;
                        Refresh();
                        return;
                    }
                    w.Close();
                    completed();
                });
                submit.name = "LoanVerificationSubmit";
                foreach (var gesture in new[] { LoanGesture.Nod, LoanGesture.ShakeHead, LoanGesture.Wave })
                {
                    var button = Ui.Size(Ui.Button(gestures, LoanGestureName(gesture), () => Perform(gesture), 18), 0, 43, 1);
                    button.name = "LoanGesture_" + gesture;
                    buttons.Add(button);
                }
                frame.gameObject.AddComponent<LoanGestureInput>().Perform = Perform;

                void Perform(LoanGesture gesture)
                {
                    if (w.Closed || moving.HasValue || !application.NextGesture.HasValue || S != session) return;
                    correct = session.PerformLoanGesture(application, gesture);
                    feedback = correct ? "动作已识别" : "动作和提示不一致，再试一次。";
                    if (preview != null)
                    {
                        moving = gesture;
                        motionStart = Time.unscaledTime;
                    }
                    Refresh();
                }

                void Refresh()
                {
                    var next = application.NextGesture;
                    string blocker = next.HasValue ? null : session.AppLoanOfferBlocker(application.AmountYuan, application.Months, atBank);
                    progress.text = $"动作验证 {application.CompletedGestures} / {LoanApplication.GestureCount}";
                    prompt.text = moving.HasValue ? "正在识别…" : next.HasValue ? "请" + LoanGestureName(next.Value) : "验证通过";
                    hint.text = moving.HasValue ? "" : next.HasValue
                        ? LoanGestureHint(next.Value) + "，也可点下方的动作按钮。"
                        : "确认后发放这笔借款。";
                    response.text = moving.HasValue ? "" : blocker ?? feedback;
                    response.color = blocker == null && correct ? Ui.Good : Ui.Warn;
                    foreach (var button in buttons) button.interactable = !moving.HasValue && next.HasValue;
                    submit.interactable = !moving.HasValue && !next.HasValue && blocker == null;
                }

                w.RefreshContent = () =>
                {
                    if (!moving.HasValue) return;
                    float fraction = Mathf.Clamp01((Time.unscaledTime - motionStart) / motionSeconds);
                    preview.RenderGesture(moving.Value, fraction);
                    if (fraction < 1) return;
                    moving = null;
                    Refresh();
                };
                Refresh();
            }, onClosed: () =>
            {
                session.CancelLoanApplication(application);
                preview?.Destroy();
            }, scrollContent: false);
        }

        private static string LoanGestureName(LoanGesture gesture) => gesture switch
        {
            LoanGesture.Nod => "点头", LoanGesture.ShakeHead => "摇头", _ => "挥手"
        };

        private static string LoanGestureHint(LoanGesture gesture) => gesture switch
        {
            LoanGesture.Nod => "在角色画面上下拖动", LoanGesture.ShakeHead => "在角色画面左右拖动",
            _ => "在角色画面斜向上拖动"
        };

        private void ConfirmLoanRepayment(Window w, AppLoan loan, bool atBank = false)
        {
            Money payoff = S.LoanPayoff(loan);
            string interest = loan.NoInterest ? "无息" : $"按 {loan.InterestMonthsAt(S.Clock.Today.TotalDays)} 个月计息";
            ConfirmBusiness("提前结清贷款 #" + loan.Id,
                $"本次支付 {payoff}，其中本金 {loan.Principal}、利息 {payoff - loan.Principal}（{interest}）。\n"
                + $"到期应还 {loan.TotalDue}。现在结清可少付利息 {loan.TotalDue - payoff}。", () =>
                {
                    if (atBank && S.BankBlocker() is string blocker) { Toast(blocker); return; }
                    if (S.RepayAppLoan(loan.Id)) w.Rebuild();
                });
        }

        private void BuildLoanCredit(Transform parent)
        {
            Line(parent, $"可借 {Money.FromYuan(S.AppLoanAvailableYuan)} · 总额度 {Money.FromYuan(S.AppLoanCreditLimitYuan)}", 20, Ui.Accent);
            Para(parent, $"名望 {S.PlayerShop.Reputation:0.0} · 未还本金占用 {S.AppLoanUsedCredit}\n"
                + $"基础额度 {Money.FromYuan(GameSession.AppLoanBaseCreditYuan)}，名望每满 1 点加 {Money.FromYuan(GameSession.AppLoanCreditPerReputationYuan)}，封顶 {Money.FromYuan(GameSession.AppLoanMaximumYuan)}。柜台和手机共用额度。", 15, Ui.Dim);
        }

        private void BuildLoanRecord(Transform parent, Window w, AppLoan loan, bool atBank)
        {
            var card = Ui.FlatPanel(parent, (atBank ? "BankLoan_" : "AppLoan_") + loan.Id, Ui.FlatBand).rectTransform;
            Ui.VList(card, 4, 10);
            string lender = loan.NoInterest ? $" · {S.CityPerson(loan.Lender)?.Name ?? "朋友"}的无息借款" : "";
            Line(card, $"借款 #{loan.Id} · 本金 {loan.Principal} · {new GameDate(loan.StartDay)} 借入{lender}", 16, Ui.Dim);
            if (loan.Repaid)
            {
                Line(card, $"{new GameDate(loan.RepaidDay)} 已结清 · 实付利息 {Money.FromCents(loan.PaidInterestCents)}", 17, Ui.Dim);
                return;
            }

            Money payoff = S.LoanPayoff(loan);
            Money shortfall = payoff - S.Ledger.Balance;
            var row = Ui.Row(card, 38);
            Ui.Size(Ui.Label(row, "今日结清 " + payoff, 21, color: Ui.Accent), 0, -1, 1);
            var repay = Ui.Size(Ui.Button(row, "提前结清", () => ConfirmLoanRepayment(w, loan, atBank), 16,
                Ui.ButtonHot, !shortfall.IsPositive && (!atBank || S.BankBlocker() == null)), 120, 36);
            repay.name = (atBank ? "BankRepay_" : "AppLoanRepay_") + loan.Id;
            string accrued = loan.NoInterest ? "无息" : $"按 {loan.InterestMonthsAt(S.Clock.Today.TotalDays)} 个月计息 {payoff - loan.Principal}";
            Para(card, $"{accrued} · {new GameDate(loan.DueDay)} 到期应还 {loan.TotalDue}（含利息 {loan.TermInterest}）", 16, Ui.Dim);
            if (shortfall.IsPositive) Line(card, $"今天结清还差 {shortfall}", 15, Ui.Warn);
            else if (loan.TotalDue > payoff) Line(card, $"现在结清可少付利息 {loan.TotalDue - payoff}", 15, Ui.Good);
        }

        private void BuildLoanDesk(Window w, string amountDraft, Action<string> saveDraft)
        {
            Line(w.Content, $"当前现金 {S.Ledger.Balance}    贷款待还 {S.AppLoanDebt}", 21, Ui.Accent);
            BuildLoanCredit(w.Content);
            Para(w.Content, $"月息 {GameSession.AppLoanMonthlyRatePercent}%。" + LoanInterestRules
                + "到期自动扣款，现金不足时先划转活期存款。", 15, Ui.Dim);
            var amountRow = Ui.Row(w.Content, 40);
            Ui.Size(Ui.Label(amountRow, "借款金额", 18), 120);
            var amountControls = Ui.Row(amountRow, 36, 4);
            Ui.Size(amountControls, 222, 36);
            var input = Ui.Size(BankAmountInput(amountControls, "QuantityValue_appLoanAmount", amountDraft, 8), 138, 36, 0);
            input.textComponent.alignment = TextAnchor.MiddleCenter;
            var less = AmountButton("−", -1, "QuantityMinus_appLoanAmount");
            less.transform.SetAsFirstSibling();
            var more = AmountButton("+", 1, "QuantityPlus_appLoanAmount");
            Line(amountRow, $"元 · 每 ¥{GameSession.AppLoanAmountStep:N0} 一档", 15, Ui.Dim);
            var presets = Ui.Row(w.Content, 34);
            foreach (long amount in new long[] { 1000, 5000, 10000, 50000 }.Where(yuan => yuan <= S.AppLoanAvailableYuan))
            {
                long selectedAmount = amount;
                Ui.Size(Ui.Button(presets, "¥" + amount.ToString("N0"), () => input.text = selectedAmount.ToString(), 17), -1, -1, 1);
            }
            var monthsRow = Ui.Row(w.Content, 40);
            Ui.Size(Ui.Label(monthsRow, "借款期限", 18), 120);
            QuantityStepper(w, monthsRow, "appLoanMonths", _loanMonths, 1, GameSession.AppLoanMaximumMonths, 1,
                v => _loanMonths = (int)v, unit: "个月");
            var principal = Line(w.Content, "", 19);
            var interest = Line(w.Content, "", 19);
            var due = Line(w.Content, "", 21, Ui.Warn);
            AppLoan quote = null;
            var apply = Ui.Size(Ui.Button(w.Content, "申请借款 · 开始验证", null, 19, Ui.ButtonHot), -1, 46);
            apply.onClick.AddListener(() =>
            {
                RefreshQuote(input.text);
                if (quote != null) ConfirmLoanApplication(quote, false, w.Rebuild);
            });
            input.onValueChanged.AddListener(RefreshQuote);
            RefreshQuote(input.text);
            Line(w.Content, "我的贷款", 20, Ui.Accent);
            var list = Ui.Scroll(w.Content, 260);
            if (S.AppLoans.Count == 0) Line(list, "暂无借款记录。", 17, Ui.Dim);
            foreach (var loan in LoanRecords())
            {
                BuildLoanRecord(list, w, loan, atBank: false);
                if (!loan.Repaid && S.IsClassmateLoan(loan) && loan.ExtensionDays == 0)
                {
                    string reason = S.FriendLoanExtensionBlocker(loan);
                    Ui.Size(Ui.Button(list, "和陈志衡商量延期 30 天", () => { if (S.ExtendFriendLoan(loan.Id)) w.Rebuild(); },
                        17, Ui.ButtonBg, reason == null), -1, 38);
                    if (reason != null) Line(list, reason, 15, Ui.Dim);
                }
            }

            Button AmountButton(string label, int direction, string key)
            {
                var button = Ui.Size(Ui.Button(amountControls, label, null, 21), 38, 36);
                button.name = key;
                var repeat = button.gameObject.AddComponent<HoldRepeat>();
                repeat.Owner = w;
                repeat.Key = key;
                repeat.Commit = n =>
                {
                    long amount = _loanAmountYuan + direction * n * (long)GameSession.AppLoanAmountStep;
                    input.text = Math.Min(S.AppLoanAvailableYuan, Math.Max(GameSession.AppLoanAmountStep, amount)).ToString();
                };
                button.onClick.AddListener(repeat.ClickOnce);
                return button;
            }

            void RefreshQuote(string amount)
            {
                saveDraft(amount);
                long.TryParse(amount, NumberStyles.None, CultureInfo.InvariantCulture, out long yuan);
                string blocker = S.AppLoanOfferBlocker(yuan, _loanMonths, atBank: false);
                bool valid = blocker == null;
                if (valid) _loanAmountYuan = yuan;
                quote = valid ? LoanQuote(yuan, _loanMonths) : null;
                long available = S.AppLoanAvailableYuan;
                less.interactable = available > 0 && (!valid || yuan > GameSession.AppLoanAmountStep);
                more.interactable = available > 0 && (!valid || yuan < available);
                principal.text = valid ? $"到账本金 {quote.Principal} · 每月利息 {quote.MonthlyInterest}" : blocker;
                interest.text = valid ? $"首月内结清 {S.LoanPayoff(quote)} · 到期利息 {quote.TermInterest}" : "";
                due.text = valid ? $"到期日 {new GameDate(quote.DueDay)} · 自动扣款 {quote.TotalDue}" : "";
                apply.interactable = valid;
            }
        }
    }

    internal sealed class LoanGestureInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action<LoanGesture> Perform;
        private Vector2 _start;
        private bool _tracking;
        private int _pointerId;

        public void OnPointerDown(PointerEventData e)
        {
            if (_tracking || e.button != PointerEventData.InputButton.Left) return;
            _tracking = RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out _start);
            _pointerId = e.pointerId;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_tracking || e.pointerId != _pointerId || e.button != PointerEventData.InputButton.Left) return;
            _tracking = false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var end)) return;
            Vector2 delta = end - _start;
            if (delta.sqrMagnitude < 32 * 32) return;
            float horizontal = Mathf.Abs(delta.x), vertical = Mathf.Abs(delta.y);
            if (vertical > horizontal * 1.35f) Perform?.Invoke(LoanGesture.Nod);
            else if (horizontal > vertical * 1.35f) Perform?.Invoke(LoanGesture.ShakeHead);
            else if (delta.y > 0) Perform?.Invoke(LoanGesture.Wave);
        }

        private void OnDisable() => _tracking = false;
    }
}
