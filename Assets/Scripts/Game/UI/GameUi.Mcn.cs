using System;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _mcnAgencyId = "chengguang";
        public void OpenCommunityMcn()
        {
            if (!S.City.HasPlace(GameSession.CommunityMcnId)) { Toast("这张旧地图没有社区MCN"); return; }
            var place = S.City.GetPlace(GameSession.CommunityMcnId);
            bool here = S.CurrentPlace == place;
            OpenWindow("社区MCN · 联合接待", 1000, 780, w =>
            {
                if (here && S.CurrentPlace != place) { w.Close(); return; }
                BuildCommunityMcn(w, place);
            });
        }

        private void GoToBrandAgency(string agencyId)
        {
            _mcnAgencyId = agencyId;
            if (!S.City.HasPlace(GameSession.CommunityMcnId)) { Toast("这张旧地图没有社区MCN"); return; }
            var place = S.City.GetPlace(GameSession.CommunityMcnId);
            string blocker = S.DistrictAccessBlocker(place);
            if (blocker != null) { Toast(blocker); return; }
            CloseAllWindows();
            if (S.CurrentPlace == place) OpenCommunityMcn();
            else S.BeginCityTravel(place.Id);
        }

        private void BuildCommunityMcn(Window w,PCShopSell.Core.World.Place place)
        {
            bool here=S.CurrentPlace==place;
            Line(w.Content,"社区 · 创作者介绍 · 09:00—18:00",21,Ui.Accent);
            Para(w.Content,"选好公司和主播。付介绍费后收到业务名片，可以联系本人洽谈。",17);
            var agencies = Ui.Row(w.Content, 36, 6);
            foreach (var company in BrandAgency.All)
            {
                var agency = company;
                Ui.Size(Ui.Button(agencies, agency.Name, () => { _mcnAgencyId = agency.Id; w.Rebuild(); },
                    17, _mcnAgencyId == agency.Id ? Ui.ButtonHot : Ui.ButtonBg), -1, 36, 1);
            }
            var selected = BrandAgency.Get(_mcnAgencyId) ?? BrandAgency.All[0];
            Line(w.Content, selected.Specialty + " · " + selected.Description, 16, Ui.Dim);
            if(!here)
            {
                string blocker=S.DistrictAccessBlocker(place);
                if(blocker!=null)Para(w.Content,blocker,17,Ui.Dim);
                Ui.Size(Ui.Button(w.Content,"前往"+selected.Name+" · 社区联合接待处",()=>{CloseAllWindows();S.BeginCityTravel(place.Id);},18,Ui.ButtonHot,blocker==null),-1,42);
            }
            var list=Ui.Scroll(w.Content,520);
            foreach(string person in KolMarket.All.Select(p=>p.Id).Concat(GameSession.BrandPeople).Distinct()
                .Where(p => BrandAgency.ForPerson(p).Id == selected.Id))
            {
                string id=person;string name=S.CityPerson(id)?.Name??id;
                var card = BrandCard(list, "");
                var header = Ui.Row(card, 80, 10);
                KolAvatar(header, id, 60, 80);
                Line(header, name + " · " + (KolMarket.Get(id)?.Topic ?? Creators.Get(id)?.Style ?? S.CityPerson(id)?.Interest), 19, Ui.Accent);
                if(S.IsMet(id))
                {
                    Ui.Size(Ui.Button(card, S.CitySamePlace(id) ? "已存名片 · 当面交谈" : "已存名片 · 打电话",
                        () => OpenPersonDialogue(id, parent: w, remote: !S.CitySamePlace(id), phoneCall: !S.CitySamePlace(id)),
                        17), -1, 38).name = "McnContact_" + id;
                    continue;
                }
                var appointment=S.McnIntroductions.FirstOrDefault(a=>a.PersonId==id&&!a.Finished&&a.Until>=S.Clock.Now.TotalMinutes);
                if(appointment!=null)
                {
                    string why=S.McnExchangeBlocker(id);
                    Para(card, why ?? "对方已到场，可以完成此前约好的介绍。", 17, Ui.Good);
                    if (why != null) Para(card, "预计到场还需 " + Math.Max(0, appointment.Arrival - S.Clock.Now.TotalMinutes) + " 分钟。", 15, Ui.Dim);
                    Ui.Size(Ui.Button(card, "见面介绍 · 交换业务名片", () => { S.ExchangeMcnContact(id); w.Rebuild(); }, 17, Ui.ButtonHot, why == null), -1, 38);
                }
                else
                {
                    string why=S.McnIntroductionBlocker(id);
                    if (here && why != null) Para(card, why, 16, Ui.Dim);
                    string fee = S.McnIntroductionFee(id).ToString("N0");
                    Ui.Size(Ui.Button(card, "安排介绍 · ¥" + fee, () =>
                        ConfirmBusiness("安排介绍", "支付 ¥" + fee + " 取得" + name + "的业务名片。",
                            () => { S.RequestMcnIntroduction(id); w.Rebuild(); }), 17, Ui.ButtonHot, why == null), -1, 38)
                        .name = "McnIntroduce_" + id;
                }
            }
        }
    }
}
