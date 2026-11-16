using System;
using System.Collections;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private bool CanAnimateUi => Application.isPlaying && _ctrl.isActiveAndEnabled;
        private UiReveal NewUiReveal() => CanAnimateUi ? new UiReveal() : null;

        private void PlayUiReveal(UiReveal reveal)
        {
            if (reveal != null) _ctrl.StartCoroutine(reveal.Play());
        }

        private void RevealAppearance(Transform target, float duration, float initialOpacity)
        {
            var reveal = NewUiReveal();
            if (reveal == null) return;
            reveal.Fade(target, 0, duration, initialOpacity);
            PlayUiReveal(reveal);
        }

        private GameSession _cashMotionSession;
        private Money _cashFrom, _cashTarget, _cashShown;
        private float _cashMotionStart;
        private const float CashMotionSeconds = .24f;

        private void SetHudCash(Money balance)
        {
            if (_cashMotionSession != S || !CanAnimateUi)
            {
                _cashMotionSession = S;
                _cashFrom = _cashTarget = _cashShown = balance;
                WriteHudCash(balance);
                return;
            }
            if (_cashTarget == balance) return;
            _cashFrom = _cashShown;
            _cashTarget = balance;
            _cashMotionStart = Time.unscaledTime;
        }

        private void UpdateHudCashMotion()
        {
            if (_cashShown == _cashTarget) return;
            float progress = EaseOut(Mathf.Clamp01((Time.unscaledTime - _cashMotionStart) / CashMotionSeconds));
            _cashShown = InterpolateMoney(_cashFrom, _cashTarget, progress);
            WriteHudCash(_cashShown);
        }

        private void WriteHudCash(Money balance)
        {
            _cashText.text = balance.ToString();
            _cashText.color = balance.IsNegative ? Ui.Warn : Ui.Good;
        }

        private static float EaseOut(float progress) => progress * (2f - progress);

        private static Money InterpolateMoney(Money from, Money to, float progress) => progress >= 1
            ? to : Money.FromCents((long)Math.Round(from.Cents + ((decimal)to.Cents - from.Cents) * (decimal)progress));

        private sealed class UiReveal
        {
            private readonly List<(Transform Target, float Delay, float Duration, Action<float> Apply)> _tracks =
                new List<(Transform, float, float, Action<float>)>();

            public void Fade(Transform target, float delay, float duration = .20f, float initialOpacity = .34f)
            {
                var group = target.gameObject.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
                float opacity = group.alpha;
                Add(target, delay, duration, progress => group.alpha = opacity * Mathf.Lerp(initialOpacity, 1, progress));
            }

            public void CountMoney(Text label, Money total, Func<Money, string> format, float delay = .03f)
            {
                if (total == Money.Zero) return;
                Add(label.transform, delay, .36f, progress => label.text = format(InterpolateMoney(Money.Zero, total, progress)));
            }

            public void Emphasize(Transform target, float delay)
            {
                var scale = target.localScale;
                Add(target, delay, .26f, progress =>
                    target.localScale = scale * (1 + .045f * Mathf.Sin(Mathf.PI * progress)));
            }

            public void Add(Transform target, float delay, float duration, Action<float> apply)
            {
                apply(0f);
                _tracks.Add((target, delay, duration, apply));
            }

            public IEnumerator Play()
            {
                float start = Time.unscaledTime;
                while (_tracks.Count > 0)
                {
                    float elapsed = Time.unscaledTime - start;
                    for (int i = _tracks.Count - 1; i >= 0; i--)
                    {
                        var track = _tracks[i];
                        if (track.Target == null)
                        {
                            _tracks.RemoveAt(i);
                            continue;
                        }
                        if (elapsed < track.Delay) continue;
                        float progress = Mathf.Clamp01((elapsed - track.Delay) / track.Duration);
                        track.Apply(EaseOut(progress));
                        if (progress >= 1f) _tracks.RemoveAt(i);
                    }
                    if (_tracks.Count > 0) yield return null;
                }
            }
        }
    }
}
