using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _mcnRecruitBrand;

        private void OpenMcnKolRecruitment(string agencyId, string brandId)
        {
            var agency=BrandAgency.Get(agencyId);
            if(agency==null)return;
            OpenWindow(agency.Name+" · 人事接待",1000,780,w=>
            {
                var brand=S.BrandBusiness.Brands.FirstOrDefault(b=>b.Id==brandId);
                if(brand==null){Line(w.Content,"请先成立品牌。",20);return;}
                Line(w.Content,"为「"+brand.Name+"」洽谈代言 · "+S.BrandKolCount(brandId)+" / "+S.BrandKolCapacity(brandId),22,Ui.Accent);
                Para(w.Content,"人事：这里是我们公司的主播。可以先了解本人，确认合适后由我们办理90天代言合约，包含直播排班和展会邀请资格。",18);
                var list=Ui.Scroll(w.Content,510);
                foreach(var talent in KolMarket.All.Where(k=>BrandAgency.ForPerson(k.Id).Id==agencyId))
                {
                    var person=talent;
                    var card=BrandCard(list,"");
                    var row=Ui.Row(card,112,10);KolAvatar(row,person.Id,84,112);
                    var info=Ui.Rect(row,"McnTalent");Ui.Size(info,-1,112,1);Ui.VList(info,3);
                    Line(info,S.CityPerson(person.Id).Name+" · "+person.Topic,20,Ui.Accent);
                    Line(info,$"魅力 {person.Charm} · 智力 {person.Intelligence} · 粉丝 {S.KolFollowers(person.Id):N0}",17);
                    Line(info,$"签约费 ¥{S.KolSigningFee(person.Id):N0} · 当前日薪 ¥{S.KolDailyWage(person.Id):N0}",17);
                    if(!S.IsMet(person.Id))
                    {
                        Line(card,"先由人事安排实际见面、交换名片，再谈合约。",16,Ui.Dim);
                        Ui.Size(Ui.Button(card,"办理介绍 / 交换名片",()=>
                        {_mcnAgencyId=agencyId;_mcnRecruitBrand=brandId;w.Close();OpenCommunityMcn();},17),-1,36);
                        continue;
                    }
                    string why=S.KolSigningBlocker(brandId,person.Id);
                    Ui.Size(Ui.Button(card,"请人事出示代言合约",()=>OpenMcnKolProposal(agencyId,brandId,person.Id),17,Ui.ButtonHot,why==null),-1,38)
                        .name="McnKolProposal_"+person.Id;
                    if(why!=null)Line(card,why,15,Ui.Dim);
                }
                Ui.Size(Ui.Button(w.Content,"返回公司接待",()=>{w.Close();_mcnAgencyId=agencyId;OpenCommunityMcn();},17),-1,38);
            });
        }

        private void OpenMcnKolProposal(string agencyId,string brandId,string person)
        {
            var agency=BrandAgency.Get(agencyId);
            OpenWindow(agency.Name+" · 代言合约",800,590,w=>
            {
                var brand=S.BrandBusiness.Brands.FirstOrDefault(b=>b.Id==brandId);
                if(brand==null)return;
                var row=Ui.Row(w.Content,110,12);KolAvatar(row,person,80,106);
                Line(row,S.CityPerson(person).Name+" × "+brand.Name,23,Ui.Accent);
                Line(w.Content,"经办公司："+agency.Name+" · 人事接待",18);
                Para(w.Content,"合约90天，占用1个品牌名额。签约后统一安排品牌直播，并可邀请参加展会。",19);
                Para(w.Content,$"签约费 ¥{S.KolSigningFee(person):N0}，当前日薪 ¥{S.KolDailyWage(person):N0}，每日结算，随粉丝和人气增长调整。暂停直播仍计薪；展会另计站台溢价，无额外营业额分成。",18);
                Line(w.Content,"签约后可在品牌面板调整直播时间、查看合约。",16,Ui.Dim);
                string why=S.KolSigningBlocker(brandId,person);
                if(why!=null)Line(w.Content,why,16,Ui.Warn);
                Ui.Size(Ui.Button(w.Content,"确认并由人事办理签约…",()=>ConfirmBusiness("在"+agency.Name+"签约",
                    S.CityPerson(person).Name+" · "+brand.Name+"\n支付签约费¥"+S.KolSigningFee(person)+"，合约90天；当前日薪¥"+S.KolDailyWage(person)+"。",()=>
                    {
                        if(S.SignKolLive(brandId,person))
                        {
                            CloseAllWindows();_brandId=brandId;_brandTab=BrandKolTab;_kolPage="代言人与排班";OpenBrandBusiness();
                        }
                        else {Toast(S.KolSigningBlocker(brandId,person));w.Rebuild();}
                    }),18,Ui.ButtonHot,why==null),-1,44).name="McnKolSign_"+person;
            });
        }
    }
}
