using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private bool _autoExpoChoosing;
        private float _autoExpoChoiceAt;
        private int _autoExpoDay;
        private GameSession _autoExpoSession;

        private void BuildExpoAutoControls(Transform parent, Window w, int day)
        {
            Ui.Size(Ui.Button(parent, _autoExpoChoosing ? "暂停自动" : "自动选择", () =>
            {
                _autoExpoChoosing = !_autoExpoChoosing;
                _autoExpoSession = S; _autoExpoDay = day;
                _autoExpoChoiceAt = Time.unscaledTime + 1.2f;
                w.Rebuild();
            }, 17, _autoExpoChoosing ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1)
                .Tip("自动选择现场评价更高的应对，每次间隔约1秒。多品牌依次处理，可随时暂停或手动接管。")
                .name = "ExpoAutoChoice";
        }

        private void UpdateAutoExpoChoices()
        {
            if (!_autoExpoChoosing) return;
            if (_autoExpoSession != S || S.IsGameOver || !ExpoHudOpen || S.CurrentPlace?.Id != "life_expo"
                || !S.BrandExpoExclusive || S.ActiveBrandExpo.Day != _autoExpoDay)
            { _autoExpoChoosing = false; return; }
            if (MainMenuOpen || Time.unscaledTime < _autoExpoChoiceAt || S.ExpoTimeAdvancing
                || _windows.Any(w => !w.Closed && w != _quarterlyExpoWindow)) return;
            _autoExpoChoiceAt = Time.unscaledTime + 1.2f;
            var e = S.ExpoVenueBrands.Where(x => x.Attending && S.ExpoActionBlocker(x.Id) == null)
                .OrderBy(x => x.Decision).FirstOrDefault();
            if (e == null) { _autoExpoChoosing = false; _quarterlyExpoWindow.RequestRebuild(); return; }
            var moment = S.CurrentExpoMoment(e.Id);
            if (moment == null) { _autoExpoChoosing = false; _quarterlyExpoWindow.RequestRebuild(); return; }
            int choice = Enumerable.Range(0, 3).OrderByDescending(index => moment.Scores[index])
                .ThenByDescending(index => moment.Heat[index]).First();
            S.SelectExpoBrand(e.Id);
            PerformExpoChoice(e.Id, choice, _quarterlyExpoWindow);
        }

        private void PerformExpoChoice(string id, int choice, Window w)
        {
            if (!S.ChooseExpoAction(id, choice))
            {
                _autoExpoChoosing = false;
                Toast(S.ExpoActionBlocker(id) ?? "自动选择已暂停，请检查当前现场状态。");
                w.Rebuild(); return;
            }
            var state = S.BrandBusiness;
            var result = state.Exhibits.FirstOrDefault(e => e.Id == id);
            if (result?.Finished == true || result?.Missed == true)
            { _autoExpoChoosing = false; w.Close(); OpenExpoSettlement(id); return; }
            if (result != null && S.ExpoJointBrandCount(result.Day) > 1)
                Toast((state.Brands.FirstOrDefault(b => b.Id == result.BrandId)?.Name ?? "品牌") + "：" + result.LastFeedback);
            w.Rebuild();
        }
    }
}
