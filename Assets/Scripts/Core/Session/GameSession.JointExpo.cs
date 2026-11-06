using System;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public BrandExhibit[] ExpoVenueBrands => _brands.Exhibits.Where(e => e.Format == 2 && !e.Missed && BrandDay >= e.Day && BrandDay < e.Day + 4)
            .Select(e => e.Copy()).ToArray();

        public bool SelectExpoBrand(string id)
        {
            if (CurrentPlace?.Id != "life_expo" || !ExpoVenueBrands.Any(e => e.Id == id)) return false;
            _brands.SelectedExpoId = id;
            return true;
        }

        public string ExpoStartBlocker(string id)
        {
            var e = _brands.Exhibits.FirstOrDefault(x => x.Id == id);
            if (e == null || e.Format != 2 || e.Attending || e.Finished || e.Missed) return ExpoIndividualStartBlocker(id);
            foreach (var entry in _brands.Exhibits.Where(x => x.Format == 2 && x.Day == e.Day && !x.Attending && !x.Finished && !x.Missed))
            {
                string blocker = ExpoIndividualStartBlocker(entry.Id);
                if (blocker != null) return (Brand(entry.BrandId)?.Name ?? "品牌") + "：" + blocker;
            }
            return null;
        }

        public int ExpoJointBrandCount(int day) => _brands.Exhibits.Count(e => e.Format == 2 && e.Day == day && !e.Finished && !e.Missed);

        public string ExpoActionBlocker(string id)
        {
            var e = _brands.Exhibits.FirstOrDefault(x => x.Id == id);
            if (e == null || !e.Attending || e.Finished || e.Missed || e.Decision >= 24) return "本品牌当前没有待处理的现场环节";
            var pending = _brands.Exhibits.Where(x => x.Format == 2 && x.Day == e.Day && x.Attending && !x.Finished && !x.Missed && x.Decision < e.Decision).ToArray();
            return pending.Length == 0 ? null : "本品牌本段已处理，请切换到" + string.Join("、", pending.Select(x => Brand(x.BrandId)?.Name ?? "其他品牌"));
        }

        private void CompleteJointExpoRound(BrandExhibit acted)
        {
            var group = _brands.Exhibits.Where(x => x.Format == 2 && x.Day == acted.Day && x.Attending && !x.Finished && !x.Missed).ToArray();
            var next = group.OrderBy(x => x.Decision).First();
            _brands.SelectedExpoId = next.Id;
            if (group.Any(x => x.Decision < acted.Decision)) return;
            int progress = group.Min(x => x.Decision);
            long roundEnd = (acted.Day + (progress - 1) / 6) * 1440L + 540 + ((progress - 1) % 6 + 1) * 80;
            AdvanceExpoTime((int)Math.Max(0, roundEnd - Clock.Now.TotalMinutes), false);
            if (IsGameOver || group.Any(x => x.Missed || !x.Attending)) return;
            if (progress >= 24)
            {
                foreach (var e in group) FinishQuarterlyExpo(e);
                _brands.SelectedExpoId = acted.Id;
            }
            else if (progress % 6 == 0)
            {
                AdvanceExpoTime((int)Math.Max(0, (acted.Day + progress / 6) * 1440L + 540 - Clock.Now.TotalMinutes), true);
                foreach (var e in group.Where(x => x.Attending && !x.Missed)) PayExpoAttendance(e);
            }
        }
    }
}
