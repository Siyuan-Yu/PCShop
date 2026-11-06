using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildExpoBrandPicker(Transform parent, Window w, BrandExhibit selected)
        {
            var exhibits = S.ExpoVenueBrands;
            if (exhibits.Length < 2) return;
            var brands = S.BrandBusiness.Brands;
            var row = Ui.Row(parent, 38, 7);
            Ui.Size(Ui.Label(row, "当前品牌", 16), 78, 38);
            int index = System.Math.Max(0, System.Array.FindIndex(exhibits, e => e.Id == selected?.Id));
            Ui.Size(Ui.Choice(row, exhibits.Select(e => (brands.FirstOrDefault(b => b.Id == e.BrandId)?.Name ?? "品牌")
                + (e.Finished ? " · 已结束" : !e.Attending ? " · 待报到" : S.ExpoActionBlocker(e.Id) == null ? " · 待处理" : " · 本段已处理")), index, choice =>
            { S.SelectExpoBrand(exhibits[choice].Id); _ctrl.FocusExpo("booth"); w.Rebuild(); }), 0, 38, 1).name = "ExpoBrandPicker";
        }

        private void BuildExpoPendingBrands(Transform parent, Window w, BrandExhibit selected)
        {
            var brands = S.BrandBusiness.Brands;
            foreach (var e in S.ExpoVenueBrands.Where(e => e.Attending && e.Day == selected.Day && S.ExpoActionBlocker(e.Id) == null))
            {
                string id = e.Id;
                Ui.Size(Ui.Button(parent, "处理" + (brands.FirstOrDefault(b => b.Id == e.BrandId)?.Name ?? "其他品牌") + "的本段活动", () =>
                { S.SelectExpoBrand(id); w.Rebuild(); }, 18, Ui.ButtonHot), -1, 44);
            }
        }
    }
}
