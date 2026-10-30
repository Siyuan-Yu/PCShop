using System.Collections.Generic;
using PCShopSell.Core.Home;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>编辑模式的屏幕标注：房间边上的米数刻度，和跟着鼠标的坐标读数。</summary>
    public sealed partial class GameUi
    {
        private readonly List<Text> _rulerLabels = new List<Text>();
        private RectTransform _cursorBox;
        private Text _cursorText;
        private RoomSpec _rulerRoom;
        private int _rulerStep;

        private Text NewRulerLabel(string text, Color color)
        {
            Text t = Ui.Label(_labelCanvas.transform, text, 15, TextAnchor.MiddleCenter, color);
            t.raycastTarget = false;
            t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(80, 22);
            return t;
        }

        private void HideEditHud()
        {
            foreach (Text t in _rulerLabels) t.gameObject.SetActive(false);
            if (_cursorBox != null) _cursorBox.gameObject.SetActive(false);
        }

        /// <summary>每帧：摆放中显示刻度和坐标，其它时候全藏起来。</summary>
        private void UpdateEditHud()
        {
            RoomSpec room = _ctrl.EditRoom;
            // 编辑模式整屏留给房间：左上角的任务条和飘字先收起来
            if (_toastList != null) _toastList.gameObject.SetActive(room == null);
            if (_questPanel != null && room != null) _questPanel.gameObject.SetActive(false);
            if (room == null || _ctrl.Cam == null)
            {
                HideEditHud();
                _rulerRoom = null;
                return;
            }

            Vector3 origin = _ctrl.EditOrigin;
            var canvasRt = (RectTransform)_labelCanvas.transform;
            Vector2 half = canvasRt.rect.size * 0.5f;

            // 刻度：沿前墙（下边）标 X，沿左墙标 Z；大房间每 2 米标一个
            int step = room.Width > 12 ? 2 : 1;
            if (_rulerRoom != room || _rulerStep != step)
            {
                foreach (Text t in _rulerLabels) Kill.It(t.gameObject);
                _rulerLabels.Clear();
                _rulerRoom = room;
                _rulerStep = step;
                Color c = new Color(0.8f, 0.95f, 1f);
                for (int x = 0; x <= room.Width + 1e-6; x += step) _rulerLabels.Add(NewRulerLabel(x.ToString(), c));
                for (int z = 0; z <= room.Depth + 1e-6; z += step) _rulerLabels.Add(NewRulerLabel(z.ToString(), c));
                _rulerLabels.Add(NewRulerLabel("X（米）", new Color(1f, 0.85f, 0.35f)));
                _rulerLabels.Add(NewRulerLabel("Z（米）", new Color(1f, 0.85f, 0.35f)));
            }

            int idx = 0;
            for (int x = 0; x <= room.Width + 1e-6; x += step) PlaceRuler(_rulerLabels[idx++], origin + new Vector3(x, 0, -0.55f), canvasRt, half);
            for (int z = 0; z <= room.Depth + 1e-6; z += step) PlaceRuler(_rulerLabels[idx++], origin + new Vector3(-0.5f, 0, z), canvasRt, half);
            PlaceRuler(_rulerLabels[idx++], origin + new Vector3((float)room.Width + 1.1f, 0, -0.55f), canvasRt, half);
            PlaceRuler(_rulerLabels[idx++], origin + new Vector3(-0.5f, 0, (float)room.Depth + 0.65f), canvasRt, half);

            UpdateCursorReadout(canvasRt, half);
        }

        /// <summary>画面像素 -> 标注画布上的位置（左下角为原点）。直接按比例换算，不依赖画布这一帧有没有跟上镜头。</summary>
        private Vector2 CanvasPos(Vector3 sp, RectTransform canvasRt) =>
            new Vector2(sp.x / Mathf.Max(1, _ctrl.Cam.pixelWidth) * canvasRt.rect.width, sp.y / Mathf.Max(1, _ctrl.Cam.pixelHeight) * canvasRt.rect.height);

        /// <summary>界面画布的宽度（界面单位）。</summary>
        public float CanvasWidth => ((RectTransform)_labelCanvas.transform).rect.width;

        private void PlaceRuler(Text t, Vector3 world, RectTransform canvasRt, Vector2 half)
        {
            Vector3 sp = _ctrl.Cam.WorldToScreenPoint(world);
            bool show = sp.z > 0;
            t.gameObject.SetActive(show);
            if (!show) return;
            t.rectTransform.anchoredPosition = CanvasPos(sp, canvasRt);
        }

        /// <summary>跟着鼠标（或手里这件）的坐标读数。</summary>
        private void UpdateCursorReadout(RectTransform canvasRt, Vector2 half)
        {
            if (_cursorBox == null)
            {
                Image bg = Ui.Panel(_labelCanvas.transform, "EditCursor", new Color(0.05f, 0.07f, 0.1f, 0.88f));
                bg.raycastTarget = false;
                _cursorBox = bg.rectTransform;
                _cursorBox.anchorMin = _cursorBox.anchorMax = Vector2.zero;
                _cursorText = Ui.Label(bg.transform, "", 16, TextAnchor.MiddleLeft, Color.white);
                _cursorText.raycastTarget = false;
                _cursorText.rectTransform.Fill(6);
            }

            if (!_ctrl.EditHover)
            {
                _cursorBox.gameObject.SetActive(false);
                return;
            }

            RoomSpec room = _ctrl.EditRoom;
            Vector2 c = _ctrl.EditCursor;
            PlacedFurniture at = _ctrl.EditGhostAt;
            string text;
            int lines = 1;
            if (_ctrl.PlaceId != null && at != null)
            {
                FurnitureDef def = FurnitureCatalog.Get(_ctrl.PlaceId);
                double w = RoomPlanner.IsRotated(at.Rot) ? def.Depth : def.Width;
                double d = RoomPlanner.IsRotated(at.Rot) ? def.Width : def.Depth;
                string pos = def.Slot == FurnitureSlot.Wall
                    ? (at.Rot == 1 ? $"挂右墙　Z {at.Z:0.00} 米" : $"挂后墙　X {at.X:0.00} 米")
                    : $"中心 X {at.X:0.00}　Z {at.Z:0.00} 米";
                text = $"<b>{def.Name}</b>\n{pos}\n占地 {w:0.##} × {d:0.##} 米";
                lines = 3;
            }
            else
            {
                text = $"X {c.x:0.00}　Z {c.y:0.00} 米";
            }

            _cursorText.text = text;
            _cursorBox.sizeDelta = new Vector2(250, 14 + lines * 22);
            Vector3 world = _ctrl.EditOrigin + new Vector3(c.x, 0.1f, c.y);
            Vector3 sp = _ctrl.Cam.WorldToScreenPoint(world);
            _cursorBox.gameObject.SetActive(sp.z > 0);
            if (sp.z <= 0) return;
            // 靠近屏幕右边（摆放面板）就翻到左边
            bool flip = sp.x / Mathf.Max(1, _ctrl.Cam.pixelWidth) > 0.62f;
            _cursorBox.pivot = new Vector2(flip ? 1f : 0f, 0f);
            _cursorBox.anchoredPosition = CanvasPos(sp, canvasRt) + new Vector2(flip ? -22 : 22, 14);
        }
    }
}
