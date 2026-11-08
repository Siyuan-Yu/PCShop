using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>One lightweight city drawing; only its camera changes during focus, pan and zoom.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AssetPropertyMap : MaskableGraphic, IScrollHandler, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        protected override void OnEnable()
        {
            // Also repair an existing window after an editor script reload.
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }

        public sealed class ViewState
        {
            public bool Initialized;
            public Vector2 Center;
            public float Zoom = 1;
            public string Region = "all";
        }
        public sealed class Building
        {
            public Vector2 Center;
            public float Width, Depth, Heading;
            public string Id, Name, StreetId;
            public Place Place;
        }
        public static readonly Color OwnedColor = new Color32(232,190,98,255);
        public static readonly Color RentedColor = new Color32(103,181,216,255);
        public static readonly Color OtherColor = new Color32(105,124,108,255);
        private GameSession _session;
        private ViewState _state;
        private Rect _bounds;
        private readonly List<Vector2> _roads = new List<Vector2>(), _lake = new List<Vector2>();
        private readonly List<int> _lakeTriangles = new List<int>();
        private readonly List<Building> _buildings = new List<Building>();
        private readonly HashSet<string> _owned = new HashSet<string>();
        private readonly HashSet<string> _rented = new HashSet<string>();
        private string _selected;
        private bool _animating, _dragged;
        private Vector2 _fromCenter, _toCenter, _lastPointer;
        private float _fromZoom, _toZoom, _elapsed;
        public Action Changed;
        public Action<Building> BuildingSelected;
        public float Zoom => _state?.Zoom ?? 1;
        public string Region => _state?.Region ?? "all";

        public void Initialize(GameSession session, ViewState state, Place[] owned, Place[] rented, string selected)
        {
            _session = session; _state = state; raycastTarget = true;
            var city = session.City;
            var positions = new List<Vector2>();
            foreach (var node in city.Map.Nodes)
            {
                var p = city.ViewPosition(node.Id); var a = new Vector2((float)p.X, (float)p.Y); positions.Add(a);
                foreach (var edge in city.Map.NeighborsOf(node.Id))
                    if (string.CompareOrdinal(node.Id, edge.To) < 0)
                    {
                        var end = city.ViewPosition(edge.To);
                        _roads.Add(a); _roads.Add(new Vector2((float)end.X, (float)end.Y));
                    }
            }
            foreach (var p in city.LakeShore) { var v = new Vector2((float)p.x, (float)p.y); _lake.Add(v); positions.Add(v); }
            TriangulateLake();
            foreach (var place in city.PublicPlaces)
            {
                var center = PlacePosition(place); positions.Add(center);
                _buildings.Add(new Building { Id = place.Id, Place = place, Name = session.PlaceTitle(place), StreetId = place.StreetId,
                    Center = center, Width = Math.Max(6, (float)place.Width), Depth = Math.Max(6, (float)place.Depth), Heading = (float)place.Heading });
            }
            foreach (var block in city.Blocks.Where(b => string.IsNullOrEmpty(b.homeId)))
                _buildings.Add(new Building { Id="block:"+_buildings.Count, Name="街区建筑", StreetId=block.street,
                    Center = new Vector2((float)block.x,(float)block.y), Width=(float)block.width, Depth=(float)block.depth, Heading=(float)block.heading });
            _bounds = BoundsOf(positions, 35);
            if (!_state.Initialized) { _state.Initialized = true; _state.Center = _bounds.center; _state.Zoom = 1; _state.Region = "all"; }
            foreach (var place in owned) _owned.Add(place.Id);
            foreach (var place in rented) if (!_owned.Contains(place.Id)) _rented.Add(place.Id);
            Select(selected); RefreshCamera();
        }

        public Vector2 PlacePosition(Place place)
        {
            var point = _session.City.ViewPosition(place.NodeId);
            return new Vector2((float)(point.X + (place.ExplicitEntrance ? place.EntranceX : 0)), (float)(point.Y + (place.ExplicitEntrance ? place.EntranceY : 0)));
        }
        private static Rect BoundsOf(IEnumerable<Vector2> points, float padding)
        {
            var array = points.ToArray(); if (array.Length == 0) return new Rect(-50,-50,100,100);
            return Rect.MinMaxRect(array.Min(p=>p.x)-padding,array.Min(p=>p.y)-padding,array.Max(p=>p.x)+padding,array.Max(p=>p.y)+padding);
        }
        private float BaseScale => Mathf.Max(.001f,Mathf.Min((rectTransform.rect.width-60)/Mathf.Max(1,_bounds.width),(rectTransform.rect.height-60)/Mathf.Max(1,_bounds.height)));
        private Vector2 Project(Vector2 point) => rectTransform.rect.center + (point - _state.Center) * (BaseScale * _state.Zoom);
        public Vector2 ProjectBuilding(Building building) => Project(building.Center);
        public Building BuildingAt(string id) => _buildings.FirstOrDefault(b=>b.Id==id);
        public void Select(string id)
        {
            _selected = id;
            SetVerticesDirty();
        }
        public void FocusAll() => Fly(_bounds.center, 1, "all");
        public void FocusDistrict(LayoutAgencyDistrictData district)
        {
            var streets = new HashSet<string>(district.streets ?? Array.Empty<string>());
            var places = _session.City.Places.Where(p => streets.Contains(p.StreetId)
                && (p.Kind == PlaceKind.Home || p.Kind == PlaceKind.Agency || GameSession.IsCommercialPremises(p) || _owned.Contains(p.Id))).ToArray();
            if (places.Length == 0) return;
            var area = BoundsOf(places.Select(PlacePosition), 25);
            float scale = Mathf.Min((rectTransform.rect.width-90)/Mathf.Max(1,area.width),(rectTransform.rect.height-90)/Mathf.Max(1,area.height));
            Fly(area.center, Mathf.Clamp(scale/BaseScale,1,6), district.id);
        }
        public void FocusPlace(Place place)
        {
            string region = _session.City.AgencyDistrictFor(place.StreetId)?.id ?? "";
            Fly(PlacePosition(place), Mathf.Max(3.5f,_state.Zoom), region);
        }
        private void Fly(Vector2 center, float zoom, string region)
        {
            _fromCenter = _state.Center; _fromZoom = _state.Zoom; _toCenter = center; _toZoom = Mathf.Clamp(zoom,.85f,8);
            _state.Region = region; _elapsed = 0; _animating = true;
        }
        private void Update()
        {
            if (!_animating || _state == null) return;
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed/.7f), eased = 1-Mathf.Pow(1-t,3);
            _state.Center = Vector2.Lerp(_fromCenter,_toCenter,eased);
            _state.Zoom = Mathf.Exp(Mathf.Lerp(Mathf.Log(_fromZoom),Mathf.Log(_toZoom),eased));
            RefreshCamera(); if (t >= 1) _animating = false;
        }
        public void ZoomBy(float factor) => ApplyZoom(factor,rectTransform.rect.center);
        private void ApplyZoom(float factor, Vector2 anchor)
        {
            if (_state == null) return;
            _animating = false;
            var world = _state.Center + (anchor-rectTransform.rect.center)/(BaseScale*_state.Zoom);
            _state.Zoom = Mathf.Clamp(_state.Zoom*factor,.85f,8);
            _state.Center = world - (anchor-rectTransform.rect.center)/(BaseScale*_state.Zoom);
            _state.Region = ""; RefreshCamera();
        }
        public void OnScroll(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out var local);
            ApplyZoom(Mathf.Exp(data.scrollDelta.y*.16f),local); data.Use();
        }
        public void OnPointerDown(PointerEventData data) { _dragged=false; }
        public void OnPointerClick(PointerEventData data)
        {
            if (_state==null||_dragged||data.button!=PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out var local);
            var world=_state.Center+(local-rectTransform.rect.center)/(BaseScale*_state.Zoom);
            Building chosen=null;float score=float.MaxValue;
            foreach(var building in _buildings)
            {
                var delta=(Vector2)(Quaternion.Euler(0,0,building.Heading)*(Vector3)(world-building.Center));
                float padding=3/(BaseScale*_state.Zoom);
                bool exact=Mathf.Abs(delta.x)<=building.Width/2&&Mathf.Abs(delta.y)<=building.Depth/2;
                if(!exact&&(Mathf.Abs(delta.x)>building.Width/2+padding||Mathf.Abs(delta.y)>building.Depth/2+padding))continue;
                float candidate=(exact?0:1000000)+(world-building.Center).sqrMagnitude;
                if(candidate<score){score=candidate;chosen=building;}
            }
            if(chosen!=null)Select(chosen.Id);
            BuildingSelected?.Invoke(chosen);data.Use();
        }
        public void OnBeginDrag(PointerEventData data)
        {
            _animating = false; _dragged=true; data.eligibleForClick = false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out _lastPointer);
        }
        public void OnDrag(PointerEventData data)
        {
            if (_state == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out var next);
            _state.Center -= (next-_lastPointer)/(BaseScale*_state.Zoom); _lastPointer = next; _state.Region = "";
            _state.Center = new Vector2(Mathf.Clamp(_state.Center.x,_bounds.xMin,_bounds.xMax),Mathf.Clamp(_state.Center.y,_bounds.yMin,_bounds.yMax));
            RefreshCamera(); data.Use();
        }
        public void OnEndDrag(PointerEventData data) { }
        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); if (_state != null) RefreshCamera(); }
        private void RefreshCamera()
        {
            if (_state == null) return;
            SetVerticesDirty(); Changed?.Invoke();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (_state == null) return;
            var rect = rectTransform.rect;
            Quad(mesh,new[]{rect.min,new Vector2(rect.xMax,rect.yMin),rect.max,new Vector2(rect.xMin,rect.yMax)},new Color32(51,67,60,255));
            int start=mesh.currentVertCount;
            foreach(var point in _lake)mesh.AddVert(Project(point),new Color32(33,61,76,255),Vector2.zero);
            for(int i=0;i+2<_lakeTriangles.Count;i+=3)mesh.AddTriangle(start+_lakeTriangles[i],start+_lakeTriangles[i+1],start+_lakeTriangles[i+2]);
            for(int i=0;i<_lake.Count;i++)AssetTrendGraphic.Stroke(mesh,Project(_lake[i]),Project(_lake[(i+1)%_lake.Count]),1,new Color32(99,132,120,255));
            for(int i=0;i+1<_roads.Count;i+=2)AssetTrendGraphic.Stroke(mesh,Project(_roads[i]),Project(_roads[i+1]),1.3f,new Color32(110,129,116,255));
            foreach(var b in _buildings)
            {
                var rotation=Quaternion.Euler(0,0,-b.Heading);
                var corners=new[]{new Vector2(-b.Width,-b.Depth),new Vector2(b.Width,-b.Depth),new Vector2(b.Width,b.Depth),new Vector2(-b.Width,b.Depth)};
                for(int i=0;i<4;i++)corners[i]=Project(b.Center+(Vector2)(rotation*(Vector3)(corners[i]*.5f)));
                Quad(mesh,corners,_owned.Contains(b.Id)?OwnedColor:_rented.Contains(b.Id)?RentedColor:OtherColor);
                if(b.Id==_selected)for(int i=0;i<4;i++)AssetTrendGraphic.Stroke(mesh,corners[i],corners[(i+1)%4],2.2f,new Color32(255,239,198,255));
            }
        }
        private static void Quad(VertexHelper mesh,Vector2[] corners,Color tint)
        {
            int start=mesh.currentVertCount;foreach(var c in corners)mesh.AddVert(c,tint,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
        private void TriangulateLake()
        {
            if(_lake.Count<3)return;
            var indices=Enumerable.Range(0,_lake.Count).ToList();
            float area=0;for(int i=0;i<_lake.Count;i++)area+=Cross(_lake[i],_lake[(i+1)%_lake.Count]);
            if(area<0)indices.Reverse();
            int guard=indices.Count*indices.Count;
            while(indices.Count>2&&guard-->0)
            {
                bool clipped=false;
                for(int i=0;i<indices.Count;i++)
                {
                    int a=indices[(i+indices.Count-1)%indices.Count],b=indices[i],c=indices[(i+1)%indices.Count];
                    if(Cross(_lake[b]-_lake[a],_lake[c]-_lake[b])<=0)continue;
                    bool inside=indices.Any(p=>p!=a&&p!=b&&p!=c&&InTriangle(_lake[p],_lake[a],_lake[b],_lake[c]));
                    if(inside)continue;_lakeTriangles.Add(a);_lakeTriangles.Add(b);_lakeTriangles.Add(c);indices.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)break;
            }
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)=>Cross(b-a,p-a)>=0&&Cross(c-b,p-b)>=0&&Cross(a-c,p-c)>=0;
    }
}
