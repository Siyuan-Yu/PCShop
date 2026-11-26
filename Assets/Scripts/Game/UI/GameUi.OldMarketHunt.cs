using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private const float HuntSearchSeconds = 2.5f;
        private static readonly string[] HuntStallArt =
        {
            "event_oldmarket", "market_hunt_clocks", "market_hunt_records"
        };
        // Click regions use the actual prop locations in the three backgrounds, with the origin at the bottom left.
        private static readonly Rect[] HuntRegions =
        {
            Rect.MinMaxRect(.10f, .30f, .231f, .596f),
            Rect.MinMaxRect(.315f, .354f, .419f, .521f),
            Rect.MinMaxRect(.419f, .362f, .532f, .519f),
            Rect.MinMaxRect(.102f, .258f, .272f, .524f),
            Rect.MinMaxRect(.301f, .263f, .504f, .546f),
            Rect.MinMaxRect(.523f, .265f, .709f, .536f),
            Rect.MinMaxRect(.057f, .266f, .262f, .544f),
            Rect.MinMaxRect(.286f, .266f, .523f, .653f),
            Rect.MinMaxRect(.543f, .266f, .689f, .522f)
        };
        private static Sprite _huntLensDisc;
        private Window _marketHuntWindow;

        private void OpenMarketHunt()
        {
            if ((_marketHuntWindow != null && !_marketHuntWindow.Closed) || S.CurrentPlace?.Id != GameSession.OldMarketVenueId
                || S.MarketHuntRemaining == 0 && S.PendingMarketFind == null)
                return;

            int stall = OldMarketHuntCatalog.Spot(S.PendingMarketFind?.MarketSpotId)?.Stall ?? S.ActiveVenueMarketTheme;
            int selected = -1;
            float animationLeft = 0;
            _marketHuntWindow = OpenWindow("青湾旧物市场 · 淘旧物", 1920, 1080, w =>
            {
                var texture = Resources.Load<Texture2D>("EventCg/" + HuntStallArt[stall]);
                float aspect = texture == null ? 16f / 9f : (float)texture.width / texture.height;
                var scene = Ui.Rect(w.Content, "MarketStallScene").Fill();
                var fitter = scene.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = aspect;
                if (texture != null)
                {
                    var image = Ui.Rect(scene, "StallArt").Fill().gameObject.AddComponent<RawImage>();
                    image.texture = texture;
                    image.raycastTarget = false;
                }
                void ReturnToReception()
                {
                    var place = S.CurrentPlace;
                    w.Close();
                    if (place?.Id == GameSession.OldMarketVenueId) OpenOldMarket(place, null, showReception: true);
                }
                BuildLeisureHeader(w, "青湾旧物市场 · 淘旧物", "返回柜台", ReturnToReception);

                var result = S.PendingMarketFind;
                bool browsing = result == null;
                void MoveTo(int next)
                {
                    stall = (next + HuntStallArt.Length) % HuntStallArt.Length;
                    selected = -1;
                    w.RequestRebuild();
                }
                for (int i = 0; i < OldMarketHuntCatalog.Stalls.Length; i++)
                {
                    int index = i;
                    Ui.Button(scene, OldMarketHuntCatalog.Stalls[i], () => MoveTo(index), 18,
                        Ui.TabFill(i == stall), browsing)
                        .GetComponent<RectTransform>().Anchor(.025f + i * .135f, .848f, .152f + i * .135f, .904f);
                }
                var controls = Ui.Panel(scene, "StallMoveHint", Ui.Chrome);
                controls.raycastTarget = false;
                controls.rectTransform.Anchor(.025f, .025f, .375f, .072f);
                Ui.Label(controls.transform, "A / D 或方向键逛摊 · 点击摊上的实物", 17, TextAnchor.MiddleCenter)
                    .rectTransform.Fill(4);

                if (S.MarketHuntPassSize > 0)
                {
                    var pass = Ui.Panel(scene, "MarketHuntPass", Ui.Chrome);
                    pass.rectTransform.Anchor(.755f, .845f, .975f, .906f);
                    int paidTier = S.MarketHuntTier;
                    string passTitle = $"{OldMarketHuntCatalog.Tiers[paidTier]} · 剩 {S.MarketHuntRemaining} / {S.MarketHuntPassSize} 次";
                    Ui.Label(pass.transform, passTitle, 20, TextAnchor.MiddleCenter, Ui.Accent).rectTransform.Fill(5);
                }

                if (browsing)
                {
                    for (int i = 0; i < OldMarketHuntCatalog.Spots.Length; i++)
                    {
                        if (OldMarketHuntCatalog.Spots[i].Stall != stall) continue;
                        int index = i;
                        var hitArea = Ui.Rect(scene, "SearchProp_" + OldMarketHuntCatalog.Spots[i].Id);
                        var bounds = HuntRegions[i];
                        hitArea.Anchor(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax);
                        var target = hitArea.gameObject.AddComponent<Image>();
                        target.color = Color.clear;
                        var hit = hitArea.gameObject.AddComponent<Button>();
                        hit.targetGraphic = target;
                        void Pick()
                        {
                            selected = index;
                            w.RequestRebuild();
                        }
                        hit.onClick.AddListener(Pick);
                        var tag = Ui.Button(scene, OldMarketHuntCatalog.Spots[i].Label, Pick, 16,
                            Ui.SelectedButton(selected == i));
                        tag.GetComponent<RectTransform>().Anchor(bounds.center.x - .055f, bounds.yMax + .012f,
                            bounds.center.x + .055f, bounds.yMax + .054f);
                    }

                    if (selected >= 0)
                    {
                        var spot = OldMarketHuntCatalog.Spots[selected];
                        var panel = Ui.FlatPanel(scene, "ConfirmHunt", Ui.PanelBg).rectTransform
                            .Anchor(.70f, .10f, .975f, .405f);
                        var box = Ui.Rect(panel, "Confirmation").Fill(16);
                        Ui.VList(box, 7, 0);
                        Line(box, $"淘{spot.Label}？", 25, Ui.Accent);
                        Line(box, $"还剩 {S.MarketHuntRemaining} 次", 17);
                        string blocker = S.MarketHuntSearchBlocker(spot);
                        if (blocker != null) Para(box, blocker, 17, Ui.Warn);
                        var confirmationButtons = Ui.Row(box, 47, 8);
                        Ui.Size(Ui.Button(confirmationButtons, "先逛逛", () => { selected = -1; w.RequestRebuild(); }, 17, Ui.ButtonSecondary), 0, 44, 1);
                        Ui.Size(Ui.Button(confirmationButtons, "开始淘", () =>
                        {
                            if (S.BeginMarketHunt(spot.Id)) animationLeft = HuntSearchSeconds;
                            w.RequestRebuild();
                        }, 20, Ui.ButtonHot, blocker == null), 0, 44, 1);
                    }

                    w.RefreshContent = () =>
                    {
                        if (S.CurrentPlace?.Id != GameSession.OldMarketVenueId || S.IsGameOver) { w.Close(); return; }
                        if (_windows.LastOrDefault() != w || w.RebuildPending) return;
                        if (DirectionalInput.LeftPressed) MoveTo(stall - 1);
                        else if (DirectionalInput.RightPressed) MoveTo(stall + 1);
                    };
                    return;
                }

                var shade = Ui.Panel(scene, "HuntFocusShade", new Color(.10f, .14f, .11f, .37f));
                shade.rectTransform.Fill();
                shade.raycastTarget = false;
                if (animationLeft > 0)
                {
                    int targetIndex = Array.FindIndex(OldMarketHuntCatalog.Spots, spot => spot.Id == result.MarketSpotId);
                    Vector2 center = targetIndex < 0 ? new Vector2(.39f, .44f) : HuntRegions[targetIndex].center;
                    var lens = BuildHuntLens(scene, texture);
                    var status = Ui.Panel(scene, "SearchProgress", Ui.Chrome);
                    status.rectTransform.Anchor(.305f, .105f, .62f, .20f);
                    Ui.Label(status.transform, "正在搜寻…", 21, color: Ui.Accent).rectTransform
                        .Anchor(.05f, .37f, .95f, .95f);
                    var bar = Ui.Panel(status.transform, "SearchMeter", Ui.ChromeButton);
                    bar.rectTransform.Anchor(.05f, .16f, .95f, .27f);
                    var fill = Ui.Panel(bar.transform, "SearchMeterFill", Ui.ChromeGold);
                    w.RefreshContent = () =>
                    {
                        if (_windows.LastOrDefault() != w) return;
                        animationLeft = Mathf.Max(0, animationLeft - Time.unscaledDeltaTime);
                        float progress = 1 - animationLeft / HuntSearchSeconds;
                        var point = center + new Vector2(Mathf.Sin(progress * 8.4f - 1.1f) * .031f,
                            Mathf.Sin(progress * 12f) * .02f);
                        float y = .139f, x = y / aspect;
                        lens.frame.Anchor(point.x - x, point.y - y, point.x + x, point.y + y);
                        const float zoom = 1.62f;
                        lens.glass.uvRect = new Rect(point.x - x / zoom, point.y - y / zoom, 2 * x / zoom, 2 * y / zoom);
                        fill.rectTransform.Anchor(0, 0, progress, 1);
                        if (animationLeft <= 0) w.RequestRebuild();
                    };
                    w.RefreshContent();
                    return;
                }

                var recipients = S.MarketHuntGiftRecipients.ToArray();
                const int recipientsPerRow = 3;
                int recipientRows = (recipients.Length + recipientsPerRow - 1) / recipientsPerRow;
                float cardTop = recipients.Length > 1 ? Mathf.Min(.84f, .56f + .054f * recipientRows)
                    : recipients.Length == 1 ? .56f : .51f;
                var reward = Ui.FlatPanel(scene, "HuntReward", Ui.PanelBg).rectTransform
                    .Anchor(.655f, .10f, .975f, cardTop);
                var content = Ui.Rect(reward, "FoundItem").Fill(19);
                Ui.VList(content, 8, 0);
                Line(content, "淘到了", 18, Ui.Accent);
                ItemLabel(content, GameSession.UsedItemPortrait(result), GameSession.UsedItemName(result), 23, 77, Ui.TextColor);
                Line(content, $"{GameSession.UsedConditionName(result)} · {GameSession.UsedWeight(result):0.##} 千克", 18);
                Para(content, OldMarketHuntCatalog.GradeDetails[result.MarketGrade], 17, Ui.Dim);
                Line(content, $"二手店参考价 {Money.FromYuan(GameSession.UsedValue(result))}", 25, Ui.Good);
                bool canCarry = GameSession.UsedWeight(result) <= S.FreeKg + 1e-9 && !S.IsGameOver;
                if (!canCarry) Line(content, "背包空间不足，腾出负重后再领取。", 17, Ui.Warn);

                void ResolveFind(Func<bool> resolve)
                {
                    var outing = S.RunningCityActivity;
                    if (!resolve()) return;
                    selected = -1;
                    if (outing != null && !outing.Active) w.Close();
                    else if (S.MarketHuntRemaining > 0) w.RequestRebuild();
                    else ReturnToReception();
                }

                void GiftButton(RectTransform row, string personId)
                {
                    string name = S.CityPerson(personId)?.Name ?? "同行者";
                    Ui.Size(Ui.Button(row, "送给" + name, () => ResolveFind(() => S.GiftMarketFindToCompanion(personId)),
                        18, Ui.ButtonBg), 0, 46, 1).name = "MarketHuntGift_" + personId;
                }

                if (recipients.Length > 1)
                    for (int i = 0; i < recipients.Length; i += recipientsPerRow)
                    {
                        var giftRow = Ui.Row(content, 48, 8);
                        foreach (string id in recipients.Skip(i).Take(recipientsPerRow)) GiftButton(giftRow, id);
                    }
                var actions = Ui.Row(content, 48, 8);
                if (recipients.Length == 1) GiftButton(actions, recipients[0]);
                Ui.Size(Ui.Button(actions, "收进背包", () => ResolveFind(S.ClaimMarketFind),
                    18, Ui.ButtonHot, canCarry), 0, 46, 1);
            }, onClosed: () => _marketHuntWindow = null, scrollContent: false, showTitleBar: false, fullScreen: true);
        }

        private static (RectTransform frame, RawImage glass) BuildHuntLens(Transform parent, Texture2D texture)
        {
            var frame = Ui.Rect(parent, "Magnifier");
            var handle = Ui.Panel(frame, "MagnifierHandle", ArtDirection.Hex("735139"));
            handle.raycastTarget = false;
            handle.rectTransform.Anchor(.81f, .01f, 1.36f, .105f);
            handle.rectTransform.pivot = new Vector2(.1f, .5f);
            handle.rectTransform.localRotation = Quaternion.Euler(0, 0, -43);
            var rim = Ui.Rect(frame, "MagnifierRim").Fill().gameObject.AddComponent<Image>();
            rim.sprite = HuntLensDisc();
            rim.color = ArtDirection.Hex("D4AE71");
            rim.raycastTarget = false;
            var mask = Ui.Rect(frame, "LensMask").Fill(10);
            var maskImage = mask.gameObject.AddComponent<Image>();
            maskImage.sprite = rim.sprite;
            maskImage.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var glass = Ui.Rect(mask, "MagnifiedStall").Fill().gameObject.AddComponent<RawImage>();
            glass.texture = texture;
            glass.raycastTarget = false;
            return (frame, glass);
        }

        private static Sprite HuntLensDisc()
        {
            if (_huntLensDisc != null) return _huntLensDisc;
            const int size = 96;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2(x + .5f - size / 2f, y + .5f - size / 2f).magnitude;
                    byte alpha = (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(size / 2f - radius));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _huntLensDisc = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
            _huntLensDisc.hideFlags = HideFlags.HideAndDontSave;
            return _huntLensDisc;
        }
    }
}
