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
            if(!S.City.HasPlace(GameSession.CommunityMcnId)){Toast("这张旧地图没有社区MCN");return;}
            var place=S.City.GetPlace(GameSession.CommunityMcnId);
            if(S.CurrentPlace==place)OpenPlaceWindow(place,BuildCommunityMcn,1000,780,holds:true,leaveOnClose:false);
            else OpenWindow("社区MCN · 联合商务接待",1000,780,w=>BuildCommunityMcn(w,place));
        }
        private void BuildCommunityMcn(Window w,PCShopSell.Core.World.Place place)
        {
            bool here=S.CurrentPlace==place;
            Line(w.Content,"社区 · 商务介绍与创作者合作 · 09:00—18:00",21,Ui.Accent);
            Para(w.Content,"选择公司，与人事洽谈品牌代言；未认识的主播先安排介绍，实际见面交换名片。",17);
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
                Ui.Size(Ui.Button(w.Content,"前往"+selected.Name+"人事 · 社区联合接待处",()=>{CloseAllWindows();S.BeginCityTravel(place.Id);},18,Ui.ButtonHot,blocker==null),-1,42);
            }
            var brands=S.BrandBusiness.Brands;
            if(brands.Length>0)
            {
                if(!brands.Any(b=>b.Id==_mcnRecruitBrand))_mcnRecruitBrand=brands[0].Id;
                var choose=Ui.Row(w.Content,36,6);
                foreach(var entry in brands)
                {
                    var brand=entry;
                    Ui.Size(Ui.Button(choose,brand.Name,()=>{_mcnRecruitBrand=brand.Id;w.Rebuild();},16,
                        _mcnRecruitBrand==brand.Id?Ui.ButtonHot:Ui.ButtonBg),-1,36,1);
                }
                string reception=S.McnReceptionBlocker(selected.Id);
                Ui.Size(Ui.Button(w.Content,"与"+selected.Name+"人事洽谈代言 / KOL签约",()=>
                {
                    if(S.BeginMcnRecruitment(selected.Id)){w.Close();OpenMcnKolRecruitment(selected.Id,_mcnRecruitBrand);}
                    else {Toast(S.McnReceptionBlocker(selected.Id));w.Rebuild();}
                },18,Ui.ButtonHot,reception==null),-1,42).name="McnReception_"+selected.Id;
                if(here&&reception!=null)Line(w.Content,reception,15,Ui.Dim);
            }
            var list=Ui.Scroll(w.Content,430);
            foreach(string person in KolMarket.All.Select(p=>p.Id).Concat(GameSession.BrandPeople).Distinct()
                .Where(p => BrandAgency.ForPerson(p).Id == selected.Id))
            {
                string id=person;string name=S.CityPerson(id)?.Name??id;
                var header = Ui.Row(list, 80, 10);
                KolAvatar(header, id, 60, 80);
                Line(header,name+" · "+(Creators.Get(id)?.Style??S.CityPerson(id)?.Interest),19,Ui.Accent);
                if(S.IsMet(id))
                {
                    var row=Ui.Row(list,38,6);
                    Ui.Size(Ui.Button(row,"已存名片 · 联系本人",()=>OpenBrandContact(id),17),-1,38,1);
                    if(Creators.Get(id)!=null)Ui.Size(Ui.Button(row,"推广 / MCN合作",()=>OpenCreatorRelations(id,w),17),-1,38,1);
                    continue;
                }
                var appointment=S.McnIntroductions.FirstOrDefault(a=>a.PersonId==id&&!a.Finished&&a.Until>=S.Clock.Now.TotalMinutes);
                if(appointment!=null)
                {
                    string why=S.McnExchangeBlocker(id);
                    Para(list,why??"对方已到场，可以交换业务名片。",17,Ui.Good);
                    Para(list,"预计到场还需 "+Math.Max(0,appointment.Arrival-S.Clock.Now.TotalMinutes)+" 分钟；请关闭窗口等待，回来可继续办理。",15,Ui.Dim);
                    Ui.Size(Ui.Button(list,"见面介绍 · 交换业务名片",()=>{if(S.ExchangeMcnContact(id)){w.Rebuild();OpenPersonDialogue(id);}else w.Rebuild();},17,Ui.ButtonHot,why==null),-1,38);
                }
                else
                {
                    string why=S.McnIntroductionBlocker(id);
                    if(here&&why!=null)Para(list,why,16,Ui.Dim);
                    Ui.Size(Ui.Button(list,"请公司安排介绍",()=>{S.RequestMcnIntroduction(id);w.Rebuild();},17,Ui.ButtonBg,why==null),-1,38);
                }
            }
            Line(w.Content,S.HasMcn?"你已有自己的MCN公司":S.DistrictAccessTier<2?"自己的MCN公司：进入公园后开放":"公园阶段 · 可以筹备自己的MCN公司",18,Ui.Accent);
            Ui.Size(Ui.Button(w.Content,"管理自己的MCN",()=>{_businessTab=4;ShowBusinessPanel(false);},18),-1,40);
        }
    }
}
