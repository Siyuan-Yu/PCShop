using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private sealed class LayoutSnapshot
        {
            public List<PlacedFurniture> Layout;
            public Dictionary<string, int> Furniture;
            public List<StockLine> Parts;
            public List<KeyValuePair<string, int>> Goods;
            public int[] Displays;
        }
        private readonly List<LayoutSnapshot> _layoutUndo = new List<LayoutSnapshot>();
        private readonly List<LayoutSnapshot> _layoutRedo = new List<LayoutSnapshot>();
        private bool _layoutHistoryActive, _layoutHistoryShop;
        public bool CanUndoLayout => _layoutHistoryActive && _layoutUndo.Count > 0;
        public bool CanRedoLayout => _layoutHistoryActive && _layoutRedo.Count > 0;
        public void BeginLayoutHistory(bool shop)
        {
            _layoutHistoryShop = shop; _layoutHistoryActive = true;
            _layoutUndo.Clear(); _layoutRedo.Clear();
        }
        public void EndLayoutHistory() { _layoutHistoryActive = false; _layoutUndo.Clear(); _layoutRedo.Clear(); }
        private LayoutSnapshot CaptureLayout() => new LayoutSnapshot
        {
            Layout = LayoutOf(_layoutHistoryShop).Select(p => p.Clone()).ToList(),
            Furniture = new Dictionary<string, int>(_furnStock),
            Parts = PlayerShop.Stock.Lines(), Goods = PlayerShop.Stock.GoodsLines(),
            Displays = PlayerShop.DisplayPcs.ToArray()
        };
        private void RememberLayout(bool shop)
        {
            if (!_layoutHistoryActive || shop != _layoutHistoryShop) return;
            _layoutUndo.Add(CaptureLayout()); _layoutRedo.Clear();
            if (_layoutUndo.Count > 30) _layoutUndo.RemoveAt(0);
        }
        private bool RestoreLayout(List<LayoutSnapshot> from, List<LayoutSnapshot> to)
        {
            if (!_layoutHistoryActive || !Time.IsHeld || from.Count == 0) return false;
            to.Add(CaptureLayout());
            var state = from[from.Count - 1]; from.RemoveAt(from.Count - 1);
            var layout = LayoutOf(_layoutHistoryShop); layout.Clear(); layout.AddRange(state.Layout.Select(p => p.Clone()));
            _furnStock.Clear(); foreach (var item in state.Furniture) _furnStock[item.Key] = item.Value;
            if (_layoutHistoryShop)
            {
                PlayerShop.Stock.Clear();
                foreach (var line in state.Parts) PlayerShop.Stock.ForceAdd(line.Part, line.Boxed, line.Count);
                foreach (var line in state.Goods) PlayerShop.Stock.ForceAddGoods(line.Key, line.Value);
                PlayerShop.RestoreDisplay(state.Displays);
                ApplyUpgrades();
            }
            return true;
        }
        public bool UndoLayout() => RestoreLayout(_layoutUndo, _layoutRedo);
        public bool RedoLayout() => RestoreLayout(_layoutRedo, _layoutUndo);
    }
}
