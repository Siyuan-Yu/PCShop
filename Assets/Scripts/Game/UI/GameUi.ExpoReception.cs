using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void OpenBookedExpo(string id)
        {
            var e = S.BrandBusiness.Exhibits.FirstOrDefault(x => x.Id == id);
            if (e == null) return;
            _brandId = e.BrandId; _brandTab = BrandExpoTab; _expoPage = "overview";
            OpenBrandBusiness();
        }

        private void OpenExpoBookingReceipt(string brandId, int day)
        {
            OpenWindow("展会报名成功", 740, 500, w =>
            {
                Line(w.Content, S.BrandBusiness.Brands.First(b => b.Id == brandId).Name + " · 云帆会展中心", 23, Ui.Accent);
                Line(w.Content, new GameDate(day) + "—" + new GameDate(day + 3), 21);
                Line(w.Content, "首日报到：" + new GameDate(day) + "09:00—10:00", 20, Ui.Good);
                Line(w.Content, "每日展出：09:00—17:00 · 连续4天", 19);
                Para(w.Content, "请提前出发。参展安排已记入日程，前一天和临近出发时会提醒。展品已交展务保管，未售库存闭展退回。", 18);
                var actions = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(actions, "查看日程", () => { w.Close(); OpenCalendar(day); }, 18), -1, 44, 1);
                Ui.Size(Ui.Button(actions, "知道了", w.Close, 18, Ui.ButtonHot), -1, 44, 1);
            });
        }

        private void OpenExpoReception()
        {
            if (S.CurrentPlace?.Id == "life_expo" && S.CityExpoOpen(false)
                && S.Clock.MinuteOfDay >= 540 && S.Clock.MinuteOfDay <= 1020)
            { CloseAllWindows(); OpenQuarterlyExpo(); return; }
            OpenWindow("云帆会展中心 · 展会安排", 860, 620, BuildExpoReception);
        }

        private void BuildExpoReception(Window w)
        {
            int today = S.Clock.Today.TotalDays;
            bool brandOpen = S.CityExpoOpen(false), comicOpen = S.CityExpoOpen(true);
            Line(w.Content, brandOpen || comicOpen ? "当前展期" : "现在没有展会", 24, Ui.Accent);
            int brandDay = brandOpen ? today / 90 * 90 + 75 : S.NextBrandExpoDay;
            int comicDay = today / 30 * 30 + 21;
            if (today > comicDay + 2) comicDay += 30;
            var brand = BrandCard(w.Content, "季度品牌展会");
            Line(brand, new GameDate(brandDay) + "—" + new GameDate(brandDay + 3), 20);
            Line(brand, "每日09:00—17:00 · 首日09:00—10:00参展报到", 17);
            Line(brand, "报名截止：" + new GameDate(brandDay - 7), 17, Ui.Dim);
            Ui.Size(Ui.Button(brand, "展会报名 / 我的参展安排", () =>
            { w.Close(); _brandTab = BrandExpoTab; _expoPage = "overview"; OpenBrandBusiness(); }, 18, Ui.ButtonHot), -1, 42);
            var comic = BrandCard(w.Content, "原创模型漫展");
            Line(comic, new GameDate(comicDay) + "—" + new GameDate(comicDay + 2), 19);
            Line(comic, "每月22—24日 · 09:00—17:00", 17, Ui.Dim);
            if (comicOpen && S.CurrentPlace?.Id == "life_expo" && S.Clock.MinuteOfDay >= 540 && S.Clock.MinuteOfDay < 1020)
                Ui.Size(Ui.Button(comic, "进入原创模型展位", () => { w.Close(); OpenCityModelExpo(); }, 18), -1, 40);
            if (S.CurrentPlace?.Id == "life_expo")
                Ui.Size(Ui.Button(w.Content, "返回街上", () => { w.Close(); _ctrl.LeaveCurrentPlace(); }, 18), -1, 42);
            else ExpoTravelButton(w.Content);
        }
    }
}
