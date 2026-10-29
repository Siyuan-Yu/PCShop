using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>可拖动的配件卡片。</summary>
    public sealed class DragCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PartDef Part;
        public Func<DragCard, Vector2, bool> OnDropped;
        private RectTransform _rt;
        private Transform _homeParent;
        private int _homeIndex;
        private CanvasGroup _group;
        private Transform _dragLayer;

        public void Init(PartDef part, Transform dragLayer, Func<DragCard, Vector2, bool> onDropped)
        {
            Part = part;
            OnDropped = onDropped;
            _dragLayer = dragLayer;
            _rt = (RectTransform)transform;
            _group = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _homeParent = transform.parent;
            _homeIndex = transform.GetSiblingIndex();
            Vector2 size = _rt.rect.size;
            transform.SetParent(_dragLayer, true);
            _rt.sizeDelta = size;
            _group.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)_dragLayer, e.position, e.pressEventCamera, out Vector3 world);
            _rt.position = world;
        }

        public void OnEndDrag(PointerEventData e)
        {
            _group.blocksRaycasts = true;
            if (OnDropped != null && OnDropped(this, e.position)) return;
            transform.SetParent(_homeParent, false);
            transform.SetSiblingIndex(_homeIndex);
        }
    }

    public sealed partial class GameUi
    {
        /// <summary>
        /// 装机小游戏：把正确的配件拖进机箱对应的槽位。CPU、内存要先装主板；拿错档次/接口算失误，失误越多耗时越长。
        /// </summary>
        public void OpenBuildMinigame(BuildOrder order)
        {
            List<PartDef> need = PartCatalog.BuildList(order.Tier);
            var rng = new System.Random(order.Id * 31 + 7);
            var cards = new List<PartDef>(need);
            foreach (PartCategory c in new[] { PartCategory.Cpu, PartCategory.Memory, PartCategory.Gpu }.OrderBy(_ => rng.Next()).Take(3))
            {
                int other = (order.Tier + 1 + rng.Next(2)) % PartCatalog.TierCount;
                cards.Add(PartCatalog.Find(c, other));
            }
            cards = cards.OrderBy(_ => rng.Next()).ToList();

            var placed = new HashSet<PartCategory>();
            int mistakes = 0;
            Text status = null;
            Button finish = null;

            OpenWindow($"装机：{PartCatalog.TierName(order.Tier)}整机（报价 {order.Price}）", 1180, 760, w =>
            {
                Text tip = Ui.Label(w.Content, $"把<b>{PartCatalog.TierName(order.Tier)}</b>档配件拖进机箱对应位置。先装主板，再装 CPU 和内存。拿错型号算失误。", 18);
                Ui.Size(tip, -1, 30);

                RectTransform body = Ui.Rect(w.Content, "Body");
                Ui.Size(body, -1, 560);
                var hl = body.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 16;
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                hl.childForceExpandWidth = false;

                Image bench = Ui.Panel(body, "Bench", Ui.PanelBg2);
                Ui.Size(bench, 400, 560);
                var grid = bench.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(180, 82);
                grid.spacing = new Vector2(10, 10);
                grid.padding = new RectOffset(12, 12, 12, 12);

                Image chassis = Ui.Panel(body, "Chassis", new Color(0.12f, 0.13f, 0.15f));
                Ui.Size(chassis, 700, 560);
                RectTransform crt = chassis.rectTransform;

                var slots = new Dictionary<PartCategory, RectTransform>();
                void Slot(PartCategory c, float x, float y, float sw, float sh)
                {
                    Image s = Ui.Panel(crt, "Slot_" + c, new Color(0.25f, 0.28f, 0.32f, 0.9f));
                    RectTransform rt = s.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1);
                    rt.anchoredPosition = new Vector2(x, -y);
                    rt.sizeDelta = new Vector2(sw, sh);
                    Text t = Ui.Label(rt, PartCatalog.CategoryName(c), 18, TextAnchor.MiddleCenter, Ui.Dim);
                    t.rectTransform.Fill();
                    slots[c] = rt;
                }
                Slot(PartCategory.Motherboard, 160, 30, 380, 300);
                Slot(PartCategory.Cpu, 200, 60, 150, 110);
                Slot(PartCategory.Memory, 390, 60, 120, 110);
                Slot(PartCategory.Gpu, 170, 350, 360, 80);
                Slot(PartCategory.Storage, 560, 350, 120, 80);
                Slot(PartCategory.PowerCase, 20, 450, 300, 90);

                status = Ui.Label(w.Content, "", 19);
                Ui.Size(status, -1, 30);
                void UpdateStatus()
                {
                    status.text = $"已装 {placed.Count}/6 · 失误 {mistakes} 次";
                    if (finish != null) finish.interactable = placed.Count == 6;
                }

                Transform dragLayer = w.Root;
                foreach (PartDef p in cards)
                {
                    Button cardBtn = Ui.Button(bench.transform, $"{p.Name}\n<size=13>{PartCatalog.CategoryName(p.Category)} · {PartCatalog.TierName(p.Tier)}{(p.Socket != null ? " · " + p.Socket : "")}</size>", null, 15,
                        new Color(0.30f, 0.34f, 0.42f));
                    var card = cardBtn.gameObject.AddComponent<DragCard>();
                    card.Init(p, dragLayer, (dc, screen) =>
                    {
                        PartCategory? hit = null;
                        foreach (var kv in slots.OrderBy(k => k.Key == PartCategory.Motherboard ? 1 : 0))
                        {
                            if (RectTransformUtility.RectangleContainsScreenPoint(kv.Value, screen, null))
                            {
                                hit = kv.Key;
                                break;
                            }
                        }
                        if (hit == null) return false;
                        PartDef part = dc.Part;
                        if (placed.Contains(hit.Value))
                        {
                            Toast("这个位置已经装好了。");
                            return false;
                        }
                        if (part.Category != hit.Value)
                        {
                            mistakes++;
                            Toast($"{part.Name} 不是装在这里的。");
                            UpdateStatus();
                            return false;
                        }
                        if ((hit == PartCategory.Cpu || hit == PartCategory.Memory) && !placed.Contains(PartCategory.Motherboard))
                        {
                            mistakes++;
                            Toast("先把主板装进机箱。");
                            UpdateStatus();
                            return false;
                        }
                        if (part.Tier != order.Tier)
                        {
                            mistakes++;
                            Toast(part.Socket != null && part.Socket != need.First(x => x.Category == part.Category).Socket
                                ? $"接口不对：{part.Socket}"
                                : $"档次不对：订单要{PartCatalog.TierName(order.Tier)}档");
                            UpdateStatus();
                            return false;
                        }
                        placed.Add(hit.Value);
                        RectTransform slot = slots[hit.Value];
                        dc.transform.SetParent(slot, false);
                        var rt = (RectTransform)dc.transform;
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = new Vector2(6, 6);
                        rt.offsetMax = new Vector2(-6, -6);
                        dc.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.45f);
                        Destroy(dc);
                        UpdateStatus();
                        return true;
                    });
                }

                RectTransform row = Ui.Row(w.Content, 50);
                finish = Ui.Size(Ui.Button(row, "装好了，开机测试并交付", () =>
                {
                    if (S.CompleteBuild(order.Id, mistakes))
                    {
                        Toast($"交付成功，收款 {order.Price}！技术 +2");
                        w.Close();
                        RebuildDock();
                    }
                }, 20, Ui.ButtonHot, false), 360);
                Ui.Size(Ui.Button(row, "先不装了", w.Close, 18), 160);
                UpdateStatus();
            });
        }

        private static void Destroy(UnityEngine.Object o) => Kill.It(o);
    }
}
