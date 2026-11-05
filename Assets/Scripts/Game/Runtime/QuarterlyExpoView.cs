using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.Runtime
{
    public sealed class ExpoPointMarker:MonoBehaviour {public string Zone;}
    public sealed class ExpoKolMarker:MonoBehaviour {public string PersonId;}
    public sealed class QuarterlyExpoView:MonoBehaviour
    {
        private Transform _empty,_live,_booth,_stage,_effects;
        private float _width,_depth,_syncAt,_animation;
        private string _layoutKey,_progressKey;
        private GameSession _session;
        private BrandExhibit _exhibit;
        private readonly List<GameObject> _visitors=new List<GameObject>();
        private readonly Dictionary<string,GameObject> _kols=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,Text> _stockLabels=new Dictionary<string,Text>();
        private readonly Dictionary<string,GameObject[]> _stockBoxes=new Dictionary<string,GameObject[]>();
        private readonly Dictionary<string,GameObject> _eventProps=new Dictionary<string,GameObject>();
        private Text _banner,_dayLabel;
        private GameObject _highlight;
        public bool Open {get;private set;}
        public int BuildVersion {get;private set;}
        public string FocusZone {get;private set;}="overview";
        public string EventEffect {get;private set;}
        public int VisibleVisitors=>_visitors.Count(p=>p.activeSelf);
        public int VisibleKols=>_kols.Values.Count(p=>p.activeSelf);
        public GameObject LiveRoot=>_live?.gameObject;
        public void Initialize(float width,float depth)
        {
            _width=width;_depth=depth;
            _empty=Child(transform,"ExpoClosedHall");
            Prims.Box(_empty,"EmptyReception",new Vector3(0,.7f,2),new Vector3(5,1.4f,1.3f),Mats.Lit(ArtDirection.Sage));
            Label(_empty,"季度展会 · 非展期",new Vector3(0,2.7f,2),9,.7f,ArtDirection.Ink);
            _live=Child(transform,"QuarterlyExpoHall");_live.gameObject.SetActive(false);
        }
        private static Transform Child(Transform parent,string name){var g=new GameObject(name).transform;g.SetParent(parent,false);return g;}
        private static Text Label(Transform parent,string text,Vector3 at,float width,float height,Color color)
        {var root=Child(parent,"ExpoLabel");root.localPosition=at;Prims.SignText(root,text,Vector3.zero,width,height,color);var t=root.GetComponentInChildren<Text>();t.raycastTarget=false;return t;}
        public Vector3 LocalPoint(string zone)
        {
            const string personPrefix = "person:";
            if (zone.StartsWith(personPrefix, StringComparison.Ordinal) &&
                _kols.TryGetValue(zone.Substring(personPrefix.Length), out var actor) && actor.activeSelf)
                return _live.localPosition + actor.transform.localPosition;
            return zone=="stage"?new Vector3(0,.5f,_depth*.78f):zone=="booth"?new Vector3(0,0,_depth*.46f):zone=="aisle"?new Vector3(-7,0,_depth*.34f):zone=="reception"?new Vector3(5,0,4):new Vector3(0,0,_depth*.48f);
        }
        public Vector3 WorldPoint(string zone)=>transform.TransformPoint(LocalPoint(zone));
        public Vector3 PlayerPoint(string zone)
        {
            if(zone=="overview")zone="reception";
            Vector3 offset=zone=="stage"?new Vector3(-2,0,.45f):zone=="booth"?new Vector3(-6-(_exhibit?.Booth??0)*2,0,.8f):zone=="reception"?new Vector3(-3,0,1.3f):new Vector3(-1,0,-1);
            return transform.TransformPoint(LocalPoint(zone)+offset);
        }
        public float CameraSize(float aspect)=>FocusZone=="overview"?Mathf.Max(16,_width/(Mathf.Max(1,aspect)*1.30f)):FocusZone=="booth"?7.5f+(_exhibit?.Booth??0)*2:FocusZone=="reception"||FocusZone.StartsWith("person:",StringComparison.Ordinal)?7.5f:10.5f;
        public void Focus(string zone){if(new[]{"overview","stage","booth","aisle","reception"}.Contains(zone))FocusZone=zone;}
        public bool FocusPerson(string id)
        {
            if (!_kols.TryGetValue(id, out var actor) || !actor.activeSelf) return false;
            FocusZone = "person:" + id;
            return true;
        }
        public void Sync(GameSession session,float dt,bool force=false)
        {
            bool changed=_session!=session;_session=session;
            if(changed){_layoutKey=null;_progressKey=null;}
            bool visible=session!=null&&session.CurrentPlace?.Id=="life_expo";
            Open=visible&&session.CityExpoOpen(false);
            _live.gameObject.SetActive(Open);_empty.gameObject.SetActive(!Open);
            if(!Open)return;
            _syncAt-=dt;
            if(force||changed||_syncAt<=0)
            {
                _syncAt=.25f;var e=session.ExpoAtVenue;_exhibit=e;
                var endorsers = e?.Day == session.Clock.Today.TotalDays ? session.ExpoEndorsers(e) : Array.Empty<string>();
                var presenters = (e?.KolIds ?? Array.Empty<string>()).Union(endorsers).ToArray();
                string key=(e?.Id??"public")+":"+(e?.Booth??-1)+":"+string.Join(",",e?.Products.Select(p=>p.ProductId)??Array.Empty<string>())+":"+string.Join(",",presenters)+":"+string.Join(",",endorsers)+":"+(e?.DesignerId??"")+":"+(e?.DesignerId!=null&&e.PosterReadyAt<=session.Clock.Now.TotalMinutes);
                if(changed||key!=_layoutKey){_layoutKey=key;Build(e, presenters, endorsers);_progressKey=null;FocusZone="overview";}
                string progress=(e?.Decision??-1)+":"+(e?.Heat??0)+":"+(e?.Trust??0)+":"+session.Clock.Today.TotalDays+":"+(e?.Attending==true)+":"+(e?.Finished==true)+":"+string.Join(",",endorsers.Where(session.ExpoPersonArrived));
                if(progress!=_progressKey){_progressKey=progress;Refresh(e, endorsers);}
                foreach(var pair in _kols)
                {
                    bool arrived = session.ExpoPersonArrived(pair.Key);
                    pair.Value.SetActive(arrived);
                    if (!arrived && FocusZone == "person:" + pair.Key) FocusZone = "overview";
                }
            }
            Animate(Mathf.Max(0,Mathf.Min(dt,.1f)));
        }
        private void Build(BrandExhibit e, string[] presenters, string[] endorsers)
        {
            foreach(var c in _live.Cast<Transform>().ToArray()){c.gameObject.SetActive(false);Kill.It(c.gameObject);}
            _visitors.Clear();_kols.Clear();_stockLabels.Clear();_stockBoxes.Clear();_eventProps.Clear();BuildVersion++;
            var cream=Mats.Lit(ArtDirection.Cream);var gold=Mats.Lit(ArtDirection.Brass);var ink=Mats.Lit(ArtDirection.Ink);
            Prims.Box(_live,"CentralAisle",new Vector3(0,.11f,_depth*.43f),new Vector3(6,.08f,_depth*.78f),Mats.Lit(ArtDirection.Clay));
            Prims.Box(_live,"CrossAisle",new Vector3(0,.12f,_depth*.32f),new Vector3(_width*.85f,.05f,3),Mats.Lit(ArtDirection.Clay));
            for(int side=-1;side<=1;side+=2)for(int n=0;n<2;n++)
            {
                var peer=Child(_live,"NeighbourBooth");peer.localPosition=new Vector3(side*_width*.34f,0,6+n*10);
                Prims.Box(peer,"Counter",new Vector3(0,.65f,0),new Vector3(8,1.3f,2),Mats.Lit(side<0?ArtDirection.Sage:ArtDirection.Blue));
                Prims.Box(peer,"Backdrop",new Vector3(0,2.2f,1.8f),new Vector3(8,.9f,.2f),ink);
                Label(peer,side<0?"新品体验 / 同行展台":"原创设计 / 创作者",new Vector3(0,2.2f,1.65f),7,.55f,ArtDirection.Cream);
                for(int j=-1;j<=1;j++){var g=ExpoProductVisual.Build(peer,j==0?"gpu0":"model:expo");g.transform.localPosition=new Vector3(j*2,1.3f,0);}
            }
            _stage=Child(_live,"BrandStage");_stage.localPosition=new Vector3(0,0,_depth*.78f);
            float stageWidth=16+(e?.Booth??0)*3;
            PointBox(_stage,"StageDeck",new Vector3(0,.24f,0),new Vector3(stageWidth,.48f,5),ink,"stage");
            Prims.Box(_stage,"Screen",new Vector3(0,3,2.3f),new Vector3(stageWidth,5,.2f),Mats.Lit(ArtDirection.Blue));
            string brand=e==null?"云帆季度品牌展":sessionBrandName(e.BrandId);
            _banner=Label(_stage,brand,new Vector3(0,3.4f,2.1f),stageWidth-1,1,ArtDirection.Cream);
            _dayLabel=Label(_stage,"",new Vector3(0,2.1f,2.05f),stageWidth-1,.7f,ArtDirection.Brass);
            for(int side=-1;side<=1;side+=2)
            {
                Prims.Box(_stage,"Truss",new Vector3(side*(stageWidth/2+.2f),2.6f,1.8f),new Vector3(.2f,5.2f,.2f),gold);
                Prims.Box(_stage,"Speaker",new Vector3(side*(stageWidth/2-.5f),.9f,-1.7f),new Vector3(.65f,1.2f,.55f),ink);
                Prims.Box(_stage,"StageLamp",new Vector3(side*stageWidth*.3f,5.1f,1.8f),new Vector3(1,.2f,.4f),Mats.Glow(ArtDirection.Cream,ArtDirection.Brass*2,false));
            }
            Prims.Box(_stage,"Lectern",new Vector3(-2,.95f,-.5f),new Vector3(.9f,1.1f,.6f),cream);
            Prims.Box(_stage,"Microphone",new Vector3(-2,1.65f,-.5f),new Vector3(.06f,.35f,.06f),ink);
            _booth=Child(_live,"PlayerBooth");_booth.localPosition=new Vector3(0,0,_depth*.46f);
            float boothWidth=10+(e?.Booth??0)*4;
            PointBox(_booth,"BoothCarpet",new Vector3(0,.16f,0),new Vector3(boothWidth,.08f,6),Mats.Lit(ArtDirection.Sage),"booth");
            Label(_booth,e==null?"品牌体验区":brand+" · "+GameSession.ExpoBoothName(e.Booth),new Vector3(0,2.3f,2.4f),boothWidth,.65f,ArtDirection.Ink);
            if(e!=null)for(int n=0;n<e.Products.Length;n++)
            {
                var product=e.Products[n];var pod=Child(_booth,"ExhibitPod_"+product.ProductId);
                int columns=Math.Min(4,e.Products.Length);pod.localPosition=new Vector3((n%columns-(columns-1)*.5f)*(boothWidth-2)/columns,0,n/columns*2.5f-1.1f);
                PointBox(pod,"ProductCounter",new Vector3(0,.58f,0),new Vector3(2,1.16f,1.45f),cream,"booth");
                var model=ExpoProductVisual.Build(pod,product.ProductId);model.transform.localPosition=Vector3.up*1.17f;
                Label(pod,_session.BrandProductName(product.ProductId),new Vector3(0,.85f,-.76f),2,.3f,ArtDirection.Ink);
                _stockLabels[product.ProductId]=Label(pod,"",new Vector3(0,.42f,-.77f),2,.26f,ArtDirection.Clay);
                var boxes=new GameObject[4];for(int nbox=0;nbox<4;nbox++)boxes[nbox]=Prims.Box(pod,"ReservedCarton",new Vector3(.66f,.2f+nbox*.27f,1.1f),new Vector3(.48f,.25f,.42f),Mats.Lit(ArtDirection.Wood));_stockBoxes[product.ProductId]=boxes;
            }
            if(e?.DesignerId!=null&&e.PosterReadyAt<=_session.Clock.Now.TotalMinutes)
            {
                var poster=Child(_booth,"CommissionedPoster");poster.localPosition=new Vector3(-boothWidth/2+.4f,0,1.8f);
                Prims.Box(poster,"PosterBoard",new Vector3(0,1.8f,0),new Vector3(1.5f,3,.14f),cream);
                Label(poster,brand,new Vector3(0,2.8f,-.09f),1.4f,.38f,ArtDirection.Ink);
                if(e.Products.Length>0){var motif=ExpoProductVisual.Build(poster,e.Products[0].ProductId);motif.transform.localPosition=new Vector3(0,1.35f,-.25f);motif.transform.localScale=Vector3.one*.8f;}
                Label(poster,"新品现场体验",new Vector3(0,.7f,-.10f),1.4f,.3f,ArtDirection.Clay);
            }
            var reception=LocalPoint("reception");PointBox(_live,"RegistrationCounter",reception+Vector3.up*.7f,new Vector3(4,1.4f,1.5f),ink,"reception");
            Label(_live,"报到 / 买家洽谈",reception+new Vector3(0,2,-.85f),5,.6f,ArtDirection.Brass);
            PointBox(_live,"AisleMeetingPoint",LocalPoint("aisle")+Vector3.up*.1f,new Vector3(3,.12f,3),Mats.Lit(ArtDirection.Brass),"aisle");
            Label(_live,"观众互动",LocalPoint("aisle")+new Vector3(0,2,0),4,.5f,ArtDirection.Ink);
            foreach(string id in presenters)
            {
                var host=Prims.Figure(_live,"SignedKol_"+id,CrowdView.PersonClothing(id),new Color(.92f,.76f,.61f),1.15f);host.AddComponent<ExpoKolMarker>().PersonId=id;
                CityNpcClothing.Apply(host,_session,id);var motion=host.GetComponent<FigureMotion>();motion.ActivityKind="expo";motion.PresentationAnimation=true;
                Label(host.transform,_session.CityPerson(id).Name,new Vector3(0,2.05f,0),2.3f,.35f,ArtDirection.Ink);
                if (endorsers.Contains(id)) Label(host.transform,"品牌代言人",new Vector3(0,2.48f,0),2.6f,.36f,ArtDirection.Brass);
                _kols[id]=host;
            }
            for(int n=0;n<26;n++)
            {var visitor=Prims.Figure(_live,"ExpoVisitor_"+n,CrowdView.PersonClothing("expo:"+n),new Color(.85f,.69f,.54f),.95f+(n%3)*.08f);visitor.GetComponent<FigureMotion>().PresentationAnimation=true;visitor.transform.localPosition=new Vector3((n%7-3)*2.4f,0,4+n/7*2.2f);_visitors.Add(visitor);}
            _effects=Child(_live,"CurrentExpoEvent");
            foreach(string effect in new[]{"queue","question","interview","rival","fault","buyer","empty","camera","stock","performance"})_eventProps[effect]=BuildEventProp(effect);
            _highlight=Prims.Shape(PrimitiveType.Cylinder,_live,"CurrentEventMarker",Vector3.zero,new Vector3(3,.035f,3),Mats.Unlit(new Color(1f,.78f,.2f,.4f)));
        }
        private string sessionBrandName(string id)=>_session.BrandBusiness.Brands.FirstOrDefault(b=>b.Id==id)?.Name??"品牌展台";
        private static void PointBox(Transform parent,string name,Vector3 at,Vector3 size,Material material,string zone)
        {Prims.Box(parent,name,at,size,material,true).AddComponent<ExpoPointMarker>().Zone=zone;}
        private GameObject BuildEventProp(string effect)
        {
            var root=Child(_effects,"Event_"+effect);var ink=Mats.Lit(ArtDirection.Ink);var brass=Mats.Lit(ArtDirection.Brass);
            if(effect!="performance"&&effect!="empty"&&effect!="stock"&&effect!="queue")
            {
                var actor=Prims.Figure(root,effect=="interview"||effect=="camera"?"PressVisitor":effect=="fault"?"Technician":"TradeVisitor",effect=="fault"?ArtDirection.Clay:ArtDirection.Blue,new Color(.90f,.75f,.6f),1.05f);
                actor.transform.localPosition=new Vector3(-1,0,-.5f);actor.GetComponent<FigureMotion>().PresentationAnimation=true;actor.GetComponent<FigureMotion>().ActivityKind="expo";
                if(effect=="buyer")Prims.Box(actor.transform,"BuyersCase",new Vector3(.45f,.65f,0),new Vector3(.15f,.38f,.5f),ink);
            }
            if(effect=="camera"||effect=="interview")
            {
                for(int n=-1;n<=1;n++)Prims.Box(root,"TripodLeg",new Vector3(n*.25f,.6f,n==0?.25f:0),new Vector3(.06f,1.2f,.06f),ink);
                Prims.Box(root,"PressCamera",Vector3.up*1.4f,new Vector3(.6f,.45f,.4f),ink);
                Prims.Shape(PrimitiveType.Cylinder,root,"CameraLens",new Vector3(0,1.4f,-.3f),new Vector3(.3f,.2f,.3f),brass).transform.localRotation=Quaternion.Euler(90,0,0);
            }
            else if(effect=="fault")
            {Prims.Box(root,"FaultWarning",Vector3.up*1.5f,new Vector3(1.2f,.8f,.15f),Mats.Lit(ArtDirection.Clay));Label(root,"!",new Vector3(0,1.5f,-.09f),1,.6f,ArtDirection.Cream);Prims.Box(root,"RepairToolbox",new Vector3(.8f,.3f,0),new Vector3(.8f,.6f,.45f),brass);}
            else if(effect=="queue")
            {for(int n=0;n<4;n++){Prims.Box(root,"QueuePost",new Vector3(n*.9f,.55f,0),new Vector3(.08f,1.1f,.08f),brass);if(n<3)Prims.Box(root,"QueueRibbon",new Vector3(n*.9f+.45f,.9f,0),new Vector3(.9f,.07f,.05f),Mats.Lit(ArtDirection.Clay));}}
            else if(effect=="performance")
            {Prims.Box(root,"DemonstrationTable",Vector3.up*.55f,new Vector3(2,1.1f,1.1f),Mats.Lit(ArtDirection.Cream));if(_exhibit?.Products.Length>0){var demo=ExpoProductVisual.Build(root,_exhibit.Products[0].ProductId);demo.transform.localPosition=Vector3.up*1.1f;}}
            else if(effect=="stock")for(int n=0;n<3;n++)Prims.Box(root,"RemainingBoxes",new Vector3(n*.7f,.3f,0),new Vector3(.6f,.6f,.5f),Mats.Lit(ArtDirection.Wood));
            else {Prims.Box(root,"InteractionSign",Vector3.up*1.5f,new Vector3(1,.8f,.15f),brass);Label(root,effect=="question"?"?":effect=="buyer"?"采购洽谈":effect=="rival"?"同行促销":"演示预告",new Vector3(0,1.5f,-.1f),1,.5f,ArtDirection.Ink);}
            root.gameObject.SetActive(false);return root.gameObject;
        }
        private void Refresh(BrandExhibit e, string[] endorsers)
        {
            bool active=e?.Attending==true;int decision=e?.Decision??0;
            EventEffect=active?ExpoMoments.EffectAt(e.Day,decision):null;
            string zone=active?ExpoMoments.ZoneAt(e.Day,decision):"reception";
            if(active)FocusZone=zone;
            _dayLabel.text="第"+(_session.Clock.Today.TotalDays%90-74)+"天 · "+_session.BrandExpoTheme(_session.Clock.Today.TotalDays)+(e?.Finished==true?" · 已闭展":active?" · "+_session.CurrentExpoMoment(e.Id)?.Title:" · 展品体验与品牌交流");
            _highlight.SetActive(active);_highlight.transform.localPosition=LocalPoint(zone)+Vector3.up*.06f;
            foreach(var p in _eventProps){p.Value.SetActive(active&&p.Key==EventEffect);p.Value.transform.localPosition=LocalPoint(zone)+new Vector3(2,0,-1);}
            if(e!=null)foreach(var product in e.Products)
            {
                _stockLabels[product.ProductId].text=e.StockReturned?"已退回 "+(product.Reserved-product.Sold):"余 "+(product.Reserved-product.Sold)+" / "+product.Reserved;
                for(int n=0;n<4;n++)_stockBoxes[product.ProductId][n].SetActive(!e.StockReturned&&product.Reserved-product.Sold>n*25);
            }
            int audience=e?.Finished==true?6:Mathf.Clamp(8+(e?.Booth??0)*3+(e?.Heat??0)/6,8,26);if(EventEffect=="empty")audience=6;
            for(int n=0;n<_visitors.Count;n++)_visitors[n].SetActive(n<audience);
            var onStage = _kols.Keys.Where(id => endorsers.Contains(id)
                ? _session.ExpoEndorserOnStage(e, id) || (e.KolIds.Contains(id) && zone == "stage")
                : zone == "stage").ToArray();
            var atBooth = _kols.Keys.Except(onStage).ToArray();
            foreach (var pair in _kols)
            {
                int stageIndex = Array.IndexOf(onStage, pair.Key);
                bool stage = stageIndex >= 0;
                int index = stage ? stageIndex : Array.IndexOf(atBooth, pair.Key);
                int count = stage ? onStage.Length : atBooth.Length;
                int columns = Math.Min(9, count);
                float x = (index % columns - (columns - 1) * .5f) * 1.25f;
                float z = index / columns * 1.05f;
                pair.Value.transform.localPosition = stage
                    ? LocalPoint("stage") + new Vector3(x, -.02f, -.4f + z)
                    : LocalPoint("booth") + new Vector3(x, 0, 2.5f + z);
                pair.Value.transform.localRotation = Quaternion.Euler(0,180,0);
            }
        }
        private void Animate(float dt)
        {
            _animation+=dt;
            for(int n=0;n<_visitors.Count;n++)
            {
                var v=_visitors[n];if(!v.activeSelf)continue;
                Vector3 target;
                if(EventEffect=="queue")target=LocalPoint("aisle")+new Vector3(n%2*.8f,0,1+n/2*.65f);
                else if(EventEffect=="performance")target=LocalPoint("stage")+new Vector3((n%7-3)*1.05f,-.5f,-4-n/7*.95f);
                else target=new Vector3((n%7-3)*2.4f+Mathf.Sin(_animation*.18f+n)*.45f,0,4+n/7*2.2f);
                Vector3 delta=target-v.transform.localPosition;v.transform.localPosition=Vector3.MoveTowards(v.transform.localPosition,target,dt*2.4f);
                var motion=v.GetComponent<FigureMotion>();motion.ActivityKind=EventEffect=="performance"?"expo":null;
                if(delta.sqrMagnitude>.02f)v.transform.localRotation=Quaternion.RotateTowards(v.transform.localRotation,Quaternion.LookRotation(delta.normalized),dt*200);
                else v.transform.localRotation=Quaternion.RotateTowards(v.transform.localRotation,Quaternion.identity,dt*120);
            }
        }
        public string Pick(Ray ray)
        {
            foreach(var hit in Physics.RaycastAll(ray,2000).OrderBy(h=>h.distance))
            {var p=hit.collider.GetComponentInParent<ExpoPointMarker>();if(p!=null&&p.transform.IsChildOf(_live))return p.Zone;}
            return null;
        }
    }
}
