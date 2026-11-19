using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;
namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private const int HomeWardrobeColumns = 3;
        private const int HomeWardrobeCardHeight = 52;
        private const int HomeWardrobeCardGap = 6;
        private static readonly (string Tag, string Label)[] HomeWardrobeSeasons =
        {
            (null, "全部"), ("spring", "春"), ("summer", "夏"), ("autumn", "秋"), ("winter", "冬")
        };

        private sealed class HomeWardrobeSelection
        {
            public string PreviewId;
            public string Season;
        }

        private AvatarPreview _avatarPreview;
        private string _cityClothingPreview;
        private bool _cityClothingDetails;
        private int _cityClothingPage;

        public void OpenWardrobeWindow()
        {
            var selection = new HomeWardrobeSelection();
            int rows = Mathf.CeilToInt(S.OwnedOutfits.Count / (float)HomeWardrobeColumns);
            float height = Mathf.Clamp(170 + rows * (HomeWardrobeCardHeight + HomeWardrobeCardGap), 510, 704);
            OpenRoomWindow("衣柜", 1120, height, w => BuildHomeWardrobe(w, selection));
        }

        private void BuildHomeWardrobe(Window w, HomeWardrobeSelection selection)
        {
            double temperature = S.CityOutdoorTemperature(S.Clock.Now.TotalMinutes);
            var equipped = S.Outfit;
            var owned = Outfits.All.Where(outfit => S.OwnedOutfits.Contains(outfit.Id)).ToArray();
            var preview = owned.FirstOrDefault(outfit => outfit.Id == selection.PreviewId) ?? equipped;
            var visible = owned.Where(outfit => selection.Season == null
                || outfit.SeasonTags.Split(',').Contains(selection.Season)).ToArray();

            var columns = Ui.Row(w.Content, -1, 16);
            var columnLayout = columns.GetComponent<HorizontalLayoutGroup>();
            columnLayout.childAlignment = TextAnchor.UpperLeft;
            columnLayout.childForceExpandHeight = false;
            var mirror = Ui.Size(Ui.Rect(columns, "WardrobeMirror"), 304, -1, 0);
            Ui.VList(mirror, 7, 0);
            BuildHomeWardrobeMirror(w, mirror, preview, equipped, temperature, selection);

            var catalog = Ui.Size(Ui.Rect(columns, "WardrobeCatalog"), 0, -1, 1);
            Ui.VList(catalog, 7, 0);
            var heading = Ui.Row(catalog, 30);
            var title = Ui.Size(Ui.Label(heading, $"我的衣服 · {visible.Length} 套", 19), 0, -1, 1);
            title.fontStyle = FontStyle.Bold;
            Ui.Size(Ui.Label(heading, $"室外 {temperature:0.0}℃", 16, TextAnchor.MiddleRight, Ui.Dim), 150);

            if (owned.Length > 1)
            {
                var seasons = Ui.Row(catalog, 40);
                foreach (var (tag, label) in HomeWardrobeSeasons)
                {
                    Ui.Size(Ui.Button(seasons, label, () =>
                    {
                        if (selection.Season == tag) return;
                        selection.Season = tag;
                        w.ResetScrollOnRebuild = true;
                        w.Rebuild();
                    }, 16, selection.Season == tag ? Ui.ButtonHot : Ui.ButtonBg), 66, 40);
                }
            }

            if (visible.Length == 0)
            {
                Line(catalog, selection.Season == null ? "衣柜里还没有衣服。" : "这个季节还没有衣服。", 16, Ui.Dim);
                return;
            }

            var grid = Ui.Rect(catalog, "WardrobeGrid");
            Ui.VList(grid, HomeWardrobeCardGap, 0);
            for (int start = 0; start < visible.Length; start += HomeWardrobeColumns)
            {
                var row = Ui.Row(grid, HomeWardrobeCardHeight, HomeWardrobeCardGap);
                for (int column = 0; column < HomeWardrobeColumns; column++)
                {
                    int index = start + column;
                    if (index >= visible.Length)
                    {
                        Ui.Size(Ui.Rect(row, "EmptySpace"), 0, -1, 1);
                        continue;
                    }

                    var outfit = visible[index];
                    string status = outfit.Id == equipped.Id ? "已穿着"
                        : OutfitFitsTemperature(outfit, temperature) ? "适合当前天气" : "";
                    string caption = status.Length == 0 ? outfit.Name : $"{outfit.Name}\n<size=14>{status}</size>";
                    var card = Ui.Size(Ui.Button(row, caption, () =>
                    {
                        if (preview.Id == outfit.Id) return;
                        selection.PreviewId = outfit.Id;
                        w.Rebuild();
                    }, 16, outfit.Id == preview.Id ? Ui.FlatRow : Ui.ButtonBg), 0, HomeWardrobeCardHeight, 1);
                    var label = card.GetComponentInChildren<Text>();
                    label.alignment = TextAnchor.MiddleLeft;
                    label.rectTransform.Anchor(0, 0, 1, 1, 53, 4, 8, 4);
                    FillItemPortrait(card.transform, outfit.ProductId)
                        .Anchor(0, .5f, 0, .5f, 7, -19, -45, -19);
                }
            }
        }

        private void BuildHomeWardrobeMirror(Window w, RectTransform mirror, OutfitDef preview,
            OutfitDef equipped, double temperature, HomeWardrobeSelection selection)
        {
            bool trying = preview.Id != equipped.Id;
            bool fits = OutfitFitsTemperature(preview, temperature);
            var portrait = Ui.Size(Ui.Panel(mirror, "ClothingPreview", new Color(.17f, .19f, .24f)), -1, 290);
            portrait.raycastTarget = false;
            DrawClothingPreview(portrait.rectTransform, preview.Id);

            var state = Ui.Panel(portrait.transform, "WardrobePreviewState", trying ? Ui.ButtonHot : Ui.PanelBg2);
            state.raycastTarget = false;
            state.rectTransform.Anchor(0, 1, 0, 1, 10, -38, -100, 10);
            Ui.Label(state.transform, trying ? "试穿中" : "当前穿着", 15, TextAnchor.MiddleCenter).rectTransform.Fill();

            Line(mirror, preview.Name, 20);
            string weather = fits ? "适合当前天气" : temperature < preview.ComfortMinC ? "今天穿会偏冷" : "今天穿会偏热";
            string charm = preview.CharmBonus > 0 ? $" · 魅力 +{preview.CharmBonus}" : "";
            Line(mirror, $"适温 {preview.ComfortMinC:0}–{preview.ComfortMaxC:0}℃ · {weather}{charm}", 15,
                fits ? Ui.Good : Ui.Dim);

            if (!trying) return;
            var actions = Ui.Row(mirror, 42, 8);
            Ui.Size(Ui.Button(actions, "取消试穿", () =>
            {
                selection.PreviewId = null;
                w.Rebuild();
            }, 17), 0, 42, 1);
            bool canChange = S.CanChangeCityClothing;
            Ui.Size(Ui.Button(actions, "换上", () =>
            {
                if (S.Equip(preview.Id)) selection.PreviewId = null;
                w.Rebuild();
            }, 17, Ui.ButtonHot, canChange), 0, 42, 1.4f);
            if (!canChange) Line(mirror, "现在只能试穿。", 15, Ui.Dim);
        }

        private static bool OutfitFitsTemperature(OutfitDef outfit, double temperature) =>
            temperature >= outfit.ComfortMinC && temperature <= outfit.ComfortMaxC;

        private void DrawClothingPreview(RectTransform portrait, string outfitId)
        {
            _avatarPreview ??= new AvatarPreview();
            _avatarPreview.Render(outfitId);
            var image = Ui.Rect(portrait, "Image").Fill().gameObject.AddComponent<RawImage>();
            image.texture = _avatarPreview.Texture;
            image.raycastTarget = false;
            FitImageToParent(image);
        }

        public void OpenCityWardrobe()
        {
            _cityClothingPreview=S.EquippedOutfit;_cityClothingPage=0;
            OpenWindow("个人衣柜",1000,760,BuildCityWardrobe);
        }
        private void CityClothingMirror(Window w)
        {
            var o=Outfits.Get(_cityClothingPreview)??S.Outfit;
            var row=Ui.Row(w.Content,240,12);
            var portrait = Ui.Size(Ui.Rect(row, "ClothingPreview"), 184, 234);
            DrawClothingPreview(portrait, o.Id);
            Ui.Size(Ui.Label(row,$"<b>{o.Name}</b> · {o.FictionalBrandId} / {o.TierName}\n适温 {o.ComfortMinC:0}—{o.ComfortMaxC:0}℃\n{o.Description}\n\n当前装备：{S.Outfit.Name}\n预览仅试穿；不会购买、换装或增加所有权。\n衣着魅力 +{o.CharmBonus}（总魅力只计算已装备一套）",17,TextAnchor.MiddleLeft),-1,234,1);
            var actions=Ui.Row(w.Content,40,8);
            Ui.Size(Ui.Button(actions,"取消试穿 / 还原",()=>{_cityClothingPreview=S.EquippedOutfit;w.Rebuild();},17),-1,40,1);
            Ui.Size(Ui.Button(actions,"换上已拥有这套",()=>{S.Equip(o.Id);w.Rebuild();},17,Ui.ButtonHot,S.CanChangeCityClothing&&S.OwnedOutfits.Contains(o.Id)&&o.Id!=S.EquippedOutfit),-1,40,1);
            if(!S.CanChangeCityClothing)Line(w.Content,"查看和试穿不受位置限制；实际更衣请到家中衣柜或营业店试衣镜旁。",16,Ui.Warn);
        }
        private void BuildCityWardrobe(Window w)
        {
            double temp=S.CityOutdoorTemperature(S.Clock.Now.TotalMinutes);
            Line(w.Content,$"室外 {temp:0.0}℃ · 当前穿着：{S.Outfit.Name}",18,Ui.Accent);
            var weatherOutfit=S.RecommendCityClothing(temp);
            Ui.Size(Ui.Button(w.Content,"换上适合当前天气的衣服 · "+weatherOutfit.Name,()=>
            {
                if(S.EquipForCurrentWeather())_cityClothingPreview=S.EquippedOutfit;
                w.Rebuild();
            },17,Ui.ButtonHot,S.CanChangeCityClothing),-1,42);
            CityClothingMirror(w);
            Ui.Size(Ui.Button(w.Content,_cityClothingDetails?"收起详情":"展开服装详情",()=>{_cityClothingDetails=!_cityClothingDetails;w.Rebuild();},16),-1,34);
            var outfits=Outfits.All.Where(o=>S.OwnedOutfits.Contains(o.Id)).ToArray();
            CityClothingRows(w,outfits,false,null);
            foreach(var shop in S.MyShops.Where(s=>s.Place==S.CurrentPlace))
            {
                var own=shop;
                foreach(var line in own.Sim.Stock.GoodsLines().Where(l=>ClothingProducts.IsClothing(l.Key)&&l.Value>0))
                {
                    var o=ClothingProducts.Outfit(line.Key);if(S.OwnedOutfits.Contains(o.Id))continue;
                    Ui.Size(Ui.Button(w.Content,$"库存转自用：{o.Name}（未预留 {line.Value} 件）",()=>ConfirmBusiness("从库存领用个人衣物",o.Name+"：消耗1件现货，不产生销售收入、不发生自买自卖。已拥有同款不会重复消耗。",()=>{S.UseCityShopClothing(own.Place.Id,o.Id);w.Rebuild();}),16),-1,38);
                }
            }
        }
        private void CityClothingRows(Window w,OutfitDef[] outfits,bool buy,Place store)
        {
            const int size=6;int pages=Math.Max(1,(outfits.Length+size-1)/size);_cityClothingPage=Math.Max(0,Math.Min(_cityClothingPage,pages-1));
            foreach(var item in outfits.Skip(_cityClothingPage*size).Take(size))
            {
                var o=item;var row=Ui.Row(w.Content,42,6);bool owned=S.OwnedOutfits.Contains(o.Id);
                ItemPortrait(row, o.ProductId, 38);
                Ui.Size(Ui.Label(row,o.Name+" · "+o.TierName+" · "+o.ComfortMinC+"—"+o.ComfortMaxC+"℃"+(buy?" · "+o.Price:owned?" · 已拥有":""),16),-1,42,1);
                Ui.Size(Ui.Button(row,"试穿",()=>{_cityClothingPreview=o.Id;w.Rebuild();},16),78,40);
                if(buy)Ui.Size(Ui.Button(row,owned?"已拥有":"柜台购买",()=>{S.BuyCityClothing(store,o.Id);w.Rebuild();},16,Ui.ButtonHot,!owned&&S.CurrentPlace==store&&S.IsPlaceOpen(store)&&S.Ledger.Balance>=o.Price),110,40);
                if(_cityClothingDetails)Para(w.Content,o.FictionalBrandId+" · "+o.WeightKg+"千克 · "+o.Description,15,Ui.Dim);
            }
            if(outfits.Length==0)Line(w.Content,"当前筛选下没有已拥有服装。",16,Ui.Dim);
            var nav=Ui.Row(w.Content,36,8);Ui.Size(Ui.Button(nav,"上一页",()=>{_cityClothingPage--;w.Rebuild();},16,interactable:_cityClothingPage>0),120,36);
            Ui.Size(Ui.Label(nav,(_cityClothingPage+1)+" / "+pages,16),-1,36,1);
            Ui.Size(Ui.Button(nav,"下一页",()=>{_cityClothingPage++;w.Rebuild();},16,interactable:_cityClothingPage+1<pages),120,36);
        }
        public void OpenCityClothingShop(Place place)
        {
            OpenPlaceWindow(place,BuildCarryStore,StoreWindowWidth,960);
        }
    }
}
