using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 人脉图：“我”在中间，认识的人围成一圈——越熟离你越近、连线越粗越亮；
    /// 按圈子分扇区（朋友熟客 / 电子市场 / 商业街店铺 / 街坊），他们彼此之间认识的也连上细线。点头像看详情。
    /// </summary>
    public sealed partial class GameUi
    {
        private string _contactSel;

        private const float GraphWidth = 940, GraphHeight = 660;

        private static readonly Color[] GroupColors =
        {
            new Color(0.93f, 0.62f, 0.45f),
            new Color(0.45f, 0.68f, 0.95f),
            new Color(0.5f, 0.82f, 0.62f),
            new Color(0.82f, 0.66f, 0.92f),
        };

        private static readonly string[] GroupNames = { "朋友熟客", "电子市场", "商业街店铺", "街坊" };

        private static Color ClosenessColor(int level) =>
            level >= 60 ? new Color(0.95f, 0.78f, 0.3f) : level >= 30 ? new Color(0.45f, 0.82f, 0.72f) : new Color(0.55f, 0.6f, 0.66f);

        public void OpenRelationsPanel()
        {
            OpenWindow("人脉", 1300, 790, w =>
            {
                List<ContactInfo> people = S.Contacts();
                if (_contactSel != null && people.All(c => c.Id != _contactSel)) _contactSel = null;

                RectTransform body = Ui.Row(w.Content, GraphHeight, 12);
                Image graph = Ui.Panel(body, "Graph", new Color(0.08f, 0.1f, 0.13f, 1f));
                Ui.Size(graph, GraphWidth, GraphHeight);
                graph.GetComponent<LayoutElement>().minWidth = GraphWidth;
                BuildContactGraph(graph.rectTransform, people, w);

                RectTransform side = Ui.Rect(body, "Detail");
                Ui.VList(side, 6, 0);
                Ui.Size(side, -1, -1, 1);
                ContactInfo sel = people.FirstOrDefault(c => c.Id == _contactSel);
                if (sel != null) BuildContactDetail(side, sel, people, w);
                else BuildContactSummary(side, people);

                RectTransform legend = Ui.Row(w.Content, 26, 14);
                for (int i = 0; i < GroupNames.Length; i++)
                    Ui.Size(Ui.Label(legend, $"<color=#{ColorUtility.ToHtmlStringRGB(GroupColors[i])}>■</color> {GroupNames[i]}", 15, TextAnchor.MiddleLeft, Ui.Dim), 130);
                Ui.Size(Ui.Label(legend, "越熟离“我”越近，连线越亮：<color=#8C99A8>脸熟</color> · <color=#73D1B8>熟人</color> · <color=#F2C64D>朋友</color>　细线：他们彼此认识", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, -1, 1);
            }, onClosed: () => _contactSel = null);
        }

        private void BuildContactGraph(RectTransform graph, List<ContactInfo> people, Window w)
        {
            if (people.Count == 0)
            {
                Text empty = Ui.Label(graph, "还没认识什么人。\n去店里、档口、街上和人聊聊，认识的人会出现在这里。", 20, TextAnchor.MiddleCenter, Ui.Dim);
                empty.rectTransform.Fill(40);
                return;
            }

            // 每个圈子一个扇区，扇区大小按人数分；圈子里越熟的越靠中间。
            var pos = new Dictionary<string, Vector2>();
            int total = people.Count;
            float angle = Mathf.PI / 2f;
            for (int g = 0; g < GroupNames.Length; g++)
            {
                List<ContactInfo> members = people.Where(c => (int)c.Group == g).OrderByDescending(c => c.Level).ToList();
                if (members.Count == 0) continue;
                float span = Mathf.PI * 2f * members.Count / total;
                for (int i = 0; i < members.Count; i++)
                {
                    float a = angle + span * (i + 0.5f) / members.Count;
                    float r = Mathf.Lerp(300f, 110f, Mathf.Clamp01(members[i].Level / 100f));
                    if (members.Count > 3) r += (i % 2 == 0 ? -1f : 1f) * 14f;
                    pos[members[i].Id] = new Vector2(Mathf.Cos(a) * r * 1.42f, Mathf.Sin(a) * r * 0.95f);
                }
                angle += span;
            }

            var known = new HashSet<string>(people.Select(c => c.Id));
            foreach (string[] t in SocialTies.All)
                if (known.Contains(t[0]) && known.Contains(t[1]))
                    GraphEdge(graph, pos[t[0]], pos[t[1]], 1.5f, new Color(0.62f, 0.66f, 0.76f, 0.35f));
            foreach (ContactInfo c in people)
            {
                bool hi = c.Id == _contactSel;
                GraphEdge(graph, Vector2.zero, pos[c.Id], hi ? 5f : 1.5f + c.Level / 30f, ClosenessColor(c.Level) * new Color(1, 1, 1, hi ? 1f : 0.75f));
            }

            Image me = Ui.Panel(graph, "Me", new Color(0.88f, 0.70f, 0.28f));
            PlaceAt(me.rectTransform, Vector2.zero, new Vector2(62, 62));
            Text meText = Ui.Label(me.transform, "<b>我</b>", 24, TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.12f));
            meText.rectTransform.Fill();

            foreach (ContactInfo c in people) GraphNode(graph, c, pos[c.Id], w);
        }

        private static void PlaceAt(RectTransform rt, Vector2 at, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = at;
        }

        private static void GraphEdge(RectTransform parent, Vector2 a, Vector2 b, float width, Color color)
        {
            Image img = Ui.Panel(parent, "Edge", color);
            img.raycastTarget = false;
            Vector2 d = b - a;
            PlaceAt(img.rectTransform, (a + b) / 2f, new Vector2(d.magnitude, width));
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        private void GraphNode(RectTransform graph, ContactInfo c, Vector2 at, Window w)
        {
            bool sel = c.Id == _contactSel;
            float size = sel ? 60 : 50;
            Image frame = Ui.Panel(graph, "Node_" + c.Id, sel ? Color.white : GroupColors[(int)c.Group]);
            PlaceAt(frame.rectTransform, at, new Vector2(size, size));
            RectTransform imgRt = Ui.Rect(frame.transform, "Face").Fill(3);
            var raw = imgRt.gameObject.AddComponent<RawImage>();
            raw.texture = PortraitOf(c.Id);
            raw.uvRect = new Rect(0.12f, 0.48f, 0.76f, 0.5f);
            if (raw.texture == null) raw.color = new Color(0.25f, 0.28f, 0.33f);
            var b = imgRt.gameObject.AddComponent<Button>();
            b.targetGraphic = raw;
            b.onClick.AddListener(() =>
            {
                _contactSel = sel ? null : c.Id;
                w.Rebuild();
            });
            raw.Tip($"<b>{c.Name}</b>　{c.Role}\n{c.LevelName} {c.Level}\n{c.Where}");

            Text name = Ui.Label(graph, $"<b>{c.Name}</b>", 14, TextAnchor.MiddleCenter, Ui.TextColor);
            name.raycastTarget = false;
            PlaceAt(name.rectTransform, at + new Vector2(0, -size / 2 - 10), new Vector2(90, 20));
        }

        private void BuildContactSummary(RectTransform side, List<ContactInfo> people)
        {
            Ui.Size(Ui.Label(side, "认识的人", 22), -1, 34);
            Ui.Size(Ui.Label(side, $"一共 {people.Count} 个", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 26);
            int close = people.Count(c => c.Level >= 60), mid = people.Count(c => c.Level >= 30 && c.Level < 60);
            Ui.Size(Ui.Label(side, $"<color=#F2C64D>朋友以上 {close}</color>　<color=#73D1B8>熟人 {mid}</color>　脸熟 {people.Count - close - mid}", 16, TextAnchor.MiddleLeft), -1, 26);
            foreach (ContactInfo c in people.OrderByDescending(c => c.Level).Take(12))
                Ui.Size(Ui.Label(side, $"{c.Name}　<size=13><color=#ABB5A8>{c.Role}</color></size>　<color=#{ColorUtility.ToHtmlStringRGB(ClosenessColor(c.Level))}>{c.LevelName} {c.Level}</color>", 15, TextAnchor.MiddleLeft), -1, 24);
            Para(side, "点图上的头像看他的详情、和谁有关系。", 15, Ui.Dim);
        }

        private void BuildContactDetail(RectTransform side, ContactInfo c, List<ContactInfo> people, Window w)
        {
            RectTransform top = Ui.Row(side, 180, 10);
            PortraitCard(top, c.Id, c.Name, "", 135, null);
            RectTransform info = Ui.Rect(top, "Info");
            Ui.VList(info, 4, 0);
            Ui.Size(info, -1, -1, 1);
            Ui.Size(Ui.Label(info, $"<b>{c.Name}</b>", 22), -1, 30);
            Para(info, c.Role, 15, Ui.Dim);
            Ui.Size(Ui.Label(info, $"{c.LevelName} {c.Level}", 16, TextAnchor.MiddleLeft, Ui.Good), -1, 24);
            Ui.Bar(info, c.Level / 100f, ClosenessColor(c.Level), 140, 12);

            Para(side, c.Where, 15);
            if (c.IsNpc) Para(side, "交情好处：" + NpcPerks.Describe(c.Id), 14, Ui.Dim);

            var names = people.ToDictionary(p => p.Id, p => p.Name);
            var ties = SocialTies.Of(c.Id).ToList();
            List<string> knownTies = ties.Where(t => names.ContainsKey(t.Key)).Select(t => $"{names[t.Key]}：{t.Value}").ToList();
            int hidden = ties.Count - knownTies.Count;
            if (knownTies.Count > 0 || hidden > 0)
            {
                Ui.Size(Ui.Label(side, "他认识的人", 17), -1, 26);
                foreach (string t in knownTies) Ui.Size(Ui.Label(side, "· " + t, 15, TextAnchor.MiddleLeft), -1, 22);
                if (hidden > 0) Ui.Size(Ui.Label(side, $"· 还有 {hidden} 个你不认识的人", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 22);
            }

            NpcDef npc = c.IsNpc ? Npcs.Get(c.Id) : null;
            if (npc != null && S.IsMet(npc.Id))
                Ui.Size(Ui.Button(side, "联系他", () => OpenNpcWindow(npc, null, w), 17, Ui.ButtonHot), -1, 40);
            else
                Para(side, "当面才能聊：去他那里找他。", 14, Ui.Dim);
            Ui.Size(Ui.Button(side, "返回总览", () =>
            {
                _contactSel = null;
                w.Rebuild();
            }, 16), -1, 36);
        }
    }
}
