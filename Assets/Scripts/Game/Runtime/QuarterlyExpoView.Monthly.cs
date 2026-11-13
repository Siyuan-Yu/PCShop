using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class QuarterlyExpoView
    {
        private Transform _monthlyHall;
        private GameObject[] _monthlyModels, _monthlyComputers;
        private Renderer[] _monthlyIndicators;
        private Text[] _monthlySigns;
        private Text _monthlyBanner;
        private Material _monthlySolved, _monthlyFault;
        private string _monthlyKind;
        private Vector3 _monthlyFocus;
        public bool MonthlyOpen => _monthlyHall != null && _monthlyHall.gameObject.activeSelf;
        public Vector3 MonthlyFocus => transform.TransformPoint(_monthlyFocus);
        private void BuildMonthlyHall()
        {
            _monthlyHall = Child(transform, "MonthlyInvitationExhibits");
            _monthlyModels = new GameObject[3]; _monthlyComputers = new GameObject[3]; _monthlyIndicators = new Renderer[3]; _monthlySigns = new Text[3];
            _monthlySolved = Mats.Lit(ArtDirection.Sage); _monthlyFault = Mats.Lit(new Color(.85f, .37f, .16f));
            _monthlyBanner = Label(_monthlyHall, "展位现场", new Vector3(0, 3.2f, 1), Mathf.Min(14, _width - 2), .7f, ArtDirection.Ink);
            for (int i = 0; i < 3; i++)
            {
                float x = new[] { -.18f, .20f, .05f }[i] * _width, z = new[] { .18f, .38f, .57f }[i] * _depth;
                var table = new Vector3(x, .85f, z);
                Prims.Box(_monthlyHall, "ExhibitTable" + i, table, new Vector3(3.1f, .2f, 2.2f), Mats.Lit(ArtDirection.Wood));
                foreach (float dx in new[] { -1.2f, 1.2f }) Prims.Box(_monthlyHall, "TableSupport", table + new Vector3(dx, -.4f, 0), new Vector3(.15f, .8f, 1.6f), Mats.Lit(ArtDirection.Ink));
                var model = Child(_monthlyHall, "CityModel" + i); model.localPosition = table + Vector3.up * .13f; _monthlyModels[i] = model.gameObject;
                Prims.Box(model, "Streets", Vector3.zero, new Vector3(2.7f, .08f, 1.9f), Mats.Lit(ArtDirection.Sage));
                Prims.Box(model, "MiniRoad", new Vector3(0, .05f, 0), new Vector3(2.7f, .03f, .25f), Mats.Lit(ArtDirection.Ink));
                for (int j = 0; j < 6; j++)
                {
                    float bx = -.85f + j % 3 * .85f, bz = j < 3 ? -.58f : .58f, height = .3f + (j + i) % 3 * .14f;
                    Prims.Box(model, "MiniBuilding", new Vector3(bx, height / 2 + .05f, bz), new Vector3(.55f, height, .45f), Mats.Lit(j % 2 == 0 ? ArtDirection.Cream : ArtDirection.Blue));
                    Prims.Box(model, "Window", new Vector3(bx, .24f, bz - .23f), new Vector3(.2f, .13f, .02f), Mats.Lit(ArtDirection.Ink));
                }
                Prims.Box(model, "LittleBench", new Vector3(.95f, .13f, -.2f), new Vector3(.35f, .12f, .16f), Mats.Lit(ArtDirection.Wood));
                var pc = Child(_monthlyHall, "ComputerSample" + i); pc.localPosition = table + Vector3.up * .2f; _monthlyComputers[i] = pc.gameObject;
                Prims.Box(pc, "Tower", new Vector3(-.8f, .45f, 0), new Vector3(.55f, .9f, .6f), Mats.Lit(ArtDirection.Ink));
                Prims.Box(pc, "Screen", new Vector3(.15f, .6f, .2f), new Vector3(1.1f, .7f, .08f), Mats.Lit(ArtDirection.Blue));
                Prims.Box(pc, "Stand", new Vector3(.15f, .18f, .2f), new Vector3(.12f, .4f, .12f), Mats.Lit(ArtDirection.Ink));
                _monthlyIndicators[i] = Prims.Box(_monthlyHall, "TaskIndicator" + i, table + new Vector3(1.2f, .25f, -.6f), Vector3.one * .18f, _monthlyFault).GetComponent<Renderer>();
                _monthlySigns[i] = Label(_monthlyHall, "", table + new Vector3(0, 1.5f, .7f), 4.5f, .5f, ArtDirection.Ink);
            }
            _monthlyHall.gameObject.SetActive(false);
        }
        private bool SyncMonthlyHall(GameSession s, bool visible)
        {
            var a = s?.RunningCityActivity;
            bool monthly = visible && a != null && (a.DefinitionId == "social_73" || a.DefinitionId == "social_74" || a.DefinitionId == "social_75" || a.DefinitionId == "social_77");
            _monthlyHall.gameObject.SetActive(monthly); if (!monthly) return false;
            if (_monthlyKind != a.DefinitionId)
            {
                _monthlyKind = a.DefinitionId; bool comic = a.DefinitionId == "social_75" || a.DefinitionId == "social_77";
                _monthlyBanner.text = comic ? "原创模型展 · 城市微缩景样件" : a.DefinitionId == "social_74" ? "布展现场 · 供电与信号调试" : "电子展 · 新品与供应商交流";
                string[] signs = comic ? new[] { "原型与街景", "连接结构与细节", "现货与体验" } : a.DefinitionId == "social_74" ? new[] { "供电支路", "电源开关", "信号接线" } : new[] { "新品体验", "供货交流", "企业需求" };
                for (int i = 0; i < 3; i++) { _monthlyModels[i].SetActive(comic); _monthlyComputers[i].SetActive(!comic); _monthlySigns[i].text = signs[i]; }
            }
            var state = s.OutdoorActivityState(a.Id);
            int node = Mathf.Clamp(state == null ? 0 : state.Pending == "walk" ? state.Visited : state.Visited - 1, 0, 2);
            _monthlyFocus = new Vector3(new[] { -.18f, .20f, .05f }[node] * _width, 1,
                new[] { .18f, .38f, .57f }[node] * _depth);
            for (int i = 0; i < 3; i++)
            { _monthlyIndicators[i].enabled = a.ServiceDefinitionId == "social_74_work"; _monthlyIndicators[i].sharedMaterial = state != null && state.Correct > i ? _monthlySolved : _monthlyFault; }
            return true;
        }
    }
}
