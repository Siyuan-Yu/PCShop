using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void OpenKolNegotiation(string person, Window dialogue, bool remote, bool phoneCall)
        {
            int days = 90;
            int frequency = 1;
            dialogue.Root.gameObject.SetActive(false);
            OpenWindow(S.CityPerson(person).Name + " · 代言与直播合约", 760, 560, w =>
            {
                var row = Ui.Row(w.Content, 100, 12);
                KolAvatar(row, person, 74, 96);
                var details = Ui.Rect(row, "KolNegotiationDetails");
                Ui.Size(details, -1, 98, 1);
                Ui.VList(details, 4, 0);
                Line(details, S.CityPerson(person).Name + " · " + KolMarket.Get(person).Topic, 20, Ui.Accent);
                var current = S.ActiveKolAmbassador(person);
                var liveFee = current == null ? Money.FromYuan(S.KolLiveSessionQuote(person)) : Money.FromCents(S.KolLiveSessionFee(current));
                Line(details, $"粉丝 {S.KolFollowers(person):N0} · 每场直播费 {liveFee}", 17);
                if (current != null)
                {
                    var brand = S.BrandBusiness.Brands.FirstOrDefault(b => b.Id == current.BrandId);
                    Line(w.Content, brand == null ? "已签约 · 待分配品牌" : "已签约 · " + brand.Name, 20, Ui.Good);
                    Line(w.Content, $"已付代言费 {Money.FromCents(current.FeeCents)}", 18);
                    Para(w.Content, $"到期 {KolTimestamp(current.EndAt)} · 直播频率 {KolFrequency(current.FrequencyDays)}", 18);
                    Para(w.Content, "在品牌的代言人页管理品牌分配和排班。", 17, Ui.Dim);
                    Ui.Size(Ui.Button(w.Content, "返回交谈", w.Close, 17), -1, 42);
                    return;
                }

                Line(w.Content, "合约期限", 18, Ui.Accent);
                var terms = Ui.Row(w.Content, 40, 6);
                foreach (int term in new[] { 30, 60, 90 })
                    Ui.Size(Ui.Button(terms, term + "天", () => { days = term; w.Rebuild(); }, 17,
                        days == term ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);
                Line(w.Content, "品牌内容频率", 18, Ui.Accent);
                var cadence = Ui.Row(w.Content, 40, 6);
                foreach (int option in new[] { 1, 2, 7 })
                    Ui.Size(Ui.Button(cadence, KolFrequency(option), () => { frequency = option; w.Rebuild(); }, 17,
                        frequency == option ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);

                long fee = S.KolSigningFee(person, days);
                Line(w.Content, $"一次性代言费 ¥{fee:N0} · 覆盖 {days} 天", 19, Ui.Accent);
                Para(w.Content, "独家代言。品牌内容实际开播时支付直播费，其他日子可播个人内容或休息。", 17);
                string defaultBrand = S.DefaultKolBrandName(person);
                Para(w.Content, defaultBrand == null ? "暂无有空位的品牌，签约后可在「我的代言人」分配。"
                    : "签约后自动分配给" + defaultBrand + "，按所选频率在 18:00 开播。可在「我的代言人」调整。", 16, Ui.Dim);
                string why = S.KolSigningBlocker(person, days, frequency, remote, phoneCall);
                if (why != null) Line(w.Content, why, 16, Ui.Warn);
                var actions = Ui.Row(w.Content, 42, 8);
                Ui.Size(Ui.Button(actions, "返回交谈", w.Close, 17), -1, 42, 1);
                Ui.Size(Ui.Button(actions, "确认签约 · ¥" + fee.ToString("N0"), () =>
                {
                    if (!S.SignKolAmbassador(person, days, frequency, remote, phoneCall)) { w.Rebuild(); return; }
                    w.Rebuild();
                }, 18, Ui.ButtonHot, why == null), -1, 42, 1).name = "KolConfirmSign_" + person;
            }, onClosed: () =>
            {
                if (dialogue.Closed) return;
                dialogue.Root.gameObject.SetActive(true);
                dialogue.Rebuild();
            });
        }
    }
}
