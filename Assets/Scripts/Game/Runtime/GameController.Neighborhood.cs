using System.Collections.Generic;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private readonly Dictionary<string,GameObject> _neighborhoodViews=new Dictionary<string,GameObject>();
        private Material _communityPaperLight,_communityGoldLight;
        private GameObject NeighborhoodView(string id,Transform parent)
        {
            if(!_neighborhoodViews.TryGetValue(id,out var go)||go==null){go=new GameObject(id);_neighborhoodViews[id]=go;}
            go.transform.SetParent(parent,false);go.SetActive(true);return go;
        }
        private void HideNeighborhood(string id){if(_neighborhoodViews.TryGetValue(id,out var go)&&go!=null)go.SetActive(false);}
        private void NeighborhoodFigure(string id,string label,Transform parent,Vector3 position,bool child=false)
        {
            var go=NeighborhoodView(id,parent);go.transform.localPosition=position;
            if(go.transform.childCount>0)return;
            float size=child?.65f:1;
            Prims.Shape(PrimitiveType.Capsule,go.transform,"Body",new Vector3(0,.7f*size,0),new Vector3(.5f,.7f,.5f)*size,Mats.Lit(child?ArtDirection.Clay:ArtDirection.Blue));
            Prims.Shape(PrimitiveType.Sphere,go.transform,"Head",new Vector3(0,1.48f*size,0),Vector3.one*.38f*size,Mats.Lit(ArtDirection.Cream));
            Prims.SignText(go.transform,label,new Vector3(0,1.9f*size,0),2,.25f,ArtDirection.Cream);
        }
        private void NeighborhoodWorkerView(string id,NeighborhoodWorker worker,bool show)
        {
            if(!show||worker==null){HideNeighborhood(id);return;}
            Transform parent=World.Root.transform;Vector3 pos=StreetWorldBuilder.ToWorld(new PCShopSell.Core.World.MapPoint(worker.X,worker.Y));
            if(worker.Route==null&&Session.CurrentPlace?.Id==worker.Place)
            {
                if(InInterior){parent=Interior.Root;pos=new Vector3(1.8f,0,2);}
                else if(World.Places.TryGetValue(worker.Place,out var at)){parent=at.Root.transform;pos=new Vector3(2,0,-2.4f);}
            }
            NeighborhoodFigure(id,worker.Name,parent,pos);
        }
        private void SyncNeighborhoodViews()
        {
            if(Session==null||World==null)return;
            var blood=Session.BloodDonation;
            bool bloodVisible=blood!=null&&blood.Stage!="done"&&(Session.Clock.MinuteOfDay>=540&&Session.Clock.MinuteOfDay<1020||blood.Stage is "donating" or "observing");
            if(bloodVisible&&World.Places.TryGetValue(GameSession.LifeServicePlace,out var service))
            {
                var van=NeighborhoodView("CommunityBloodVan",service.Root.transform);van.transform.localPosition=new Vector3(-4,0,-4.5f);
                if(van.transform.childCount==0)
                {
                    var cream=Mats.Lit(ArtDirection.Cream);var red=Mats.Lit(ArtDirection.Clay);
                    Prims.Box(van.transform,"Van",new Vector3(0,1,0),new Vector3(3.5f,1.8f,1.6f),cream);
                    Prims.Box(van.transform,"CrossVertical",new Vector3(0,1,-.83f),new Vector3(.2f,.8f,.04f),red);
                    Prims.Box(van.transform,"CrossHorizontal",new Vector3(0,1,-.84f),new Vector3(.8f,.2f,.04f),red);
                    foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-.7f,.7f})Prims.Shape(PrimitiveType.Sphere,van.transform,"Wheel",new Vector3(x,.3f,z),new Vector3(.55f,.55f,.2f),Mats.Lit(ArtDirection.Ink));
                    Prims.SignText(van.transform,"献血登记 · 社区生活／街坊",new Vector3(0,2.4f,0),4,.3f,ArtDirection.Cream);
                }
                NeighborhoodFigure("CommunityBloodNurse","献血工作人员",service.Root.transform,new Vector3(-1.8f,0,-4));
            }
            else {HideNeighborhood("CommunityBloodVan");HideNeighborhood("CommunityBloodNurse");}
            var child=Session.LostChild;
            if(child!=null&&child.Stage!="done")
            {
                Transform parent=World.Root.transform;Vector3 pos;
                if(child.Stage=="following")
                {parent=InHome?HomeRoom.Root:InInterior?Interior.Root:World.Root.transform;pos=parent.InverseTransformPoint(_avatar.Position+new Vector3(-.7f,0,.6f));}
                else if(World.Places.TryGetValue(child.Place,out var at)){parent=at.Root.transform;pos=new Vector3(-1.5f,0,-2.5f);}
                else pos=StreetWorldBuilder.ToWorld(Session.PlayerPosition);
                NeighborhoodFigure("CommunityLostChild","等家人的孩子",parent,pos,true);
            }
            else HideNeighborhood("CommunityLostChild");
            NeighborhoodWorkerView("CommunityChildHelper",child?.Helper,child!=null&&child.Stage!="done");
            var leak=Session.RoofLeak;
            NeighborhoodWorkerView("CommunityRoofWorker",leak?.Worker,leak!=null&&!leak.Fixed);
            if(leak!=null&&!leak.Fixed&&InInterior&&Session.CurrentPlace?.Id==leak.Shop)
            {
                var puddle=NeighborhoodView("CommunityRoofLeak",Interior.Root);puddle.transform.localPosition=new Vector3(1.5f,0,1.5f);
                if(puddle.transform.childCount==0)
                {
                    Prims.Shape(PrimitiveType.Cylinder,puddle.transform,"Water",new Vector3(0,.025f,0),new Vector3(1,.025f,1),Mats.Lit(ArtDirection.Blue));
                    Prims.Shape(PrimitiveType.Cylinder,puddle.transform,"Bucket",new Vector3(0,.23f,0),new Vector3(.55f,.24f,.55f),Mats.Lit(ArtDirection.Sage));
                    Prims.Shape(PrimitiveType.Sphere,puddle.transform,"Drip",new Vector3(0,2.5f,0),Vector3.one*.12f,Mats.Lit(ArtDirection.Blue));
                    Prims.SignText(puddle.transform,"漏水 · 社区生活／街坊",new Vector3(0,3,0),3,.3f,ArtDirection.Cream);
                }
                var water=puddle.transform.Find("Water");water.gameObject.SetActive(leak.Water>0);water.localScale=new Vector3(.5f+leak.Water*.3f,.025f,.5f+leak.Water*.3f);
                puddle.transform.Find("Bucket").gameObject.SetActive(leak.Bucket);
                var drip=puddle.transform.Find("Drip");drip.gameObject.SetActive(Session.CityIsRaining);drip.localPosition=new Vector3(0,.5f+2*(1-(UnityEngine.Time.unscaledTime%1)),0);
            }
            else HideNeighborhood("CommunityRoofLeak");
            var festival=Session.LanternFestival;
            if(Session.FestivalOpen&&World.Places.TryGetValue(GameSession.LifeServicePlace,out var venue))
            {
                var view=NeighborhoodView("CommunityLanternFestival",venue.Root.transform);view.transform.localPosition=new Vector3(1.8f,0,-5);
                if(view.transform.childCount==0)
                {
                    _communityPaperLight=Mats.Glow(ArtDirection.Clay,new Color(1,.5f,.1f));
                    _communityGoldLight=Mats.Glow(ArtDirection.Brass,new Color(1,.8f,.3f));
                    Prims.Box(view.transform,"Rail",new Vector3(0,2.5f,0),new Vector3(5,.1f,.1f),Mats.Lit(ArtDirection.Brass));
                    foreach(float x in new[]{-2.5f,2.5f})Prims.Box(view.transform,"Post",new Vector3(x,1.2f,0),new Vector3(.12f,2.6f,.12f),Mats.Lit(ArtDirection.Sage));
                    for(int i=0;i<3;i++)
                    {
                        Prims.Shape(PrimitiveType.Sphere,view.transform,"Lamp"+i,new Vector3(-1.7f+i*1.7f,2,0),Vector3.one*.65f,Mats.Glow(ArtDirection.Clay,new Color(1,.5f,.1f)));
                        Prims.Box(view.transform,"SquareLamp"+i,new Vector3(-1.7f+i*1.7f,2,0),new Vector3(.55f,.85f,.55f),_communityPaperLight);
                        Prims.SignText(view.transform,"挂灯位"+(i+1),new Vector3(-1.7f+i*1.7f,.7f,0),1.5f,.22f,ArtDirection.Cream);
                    }
                    Prims.Box(view.transform,"CraftTable",new Vector3(0,.5f,1.3f),new Vector3(2,.9f,.6f),Mats.Lit(ArtDirection.Sage));
                    for(int i=0;i<4;i++)
                    {
                        Prims.Shape(PrimitiveType.Sphere,view.transform,"FestivalDecoration"+i,new Vector3(-3+i*2,3.4f,1.8f),Vector3.one*.4f,Mats.Glow(ArtDirection.Clay,new Color(1,.55f,.1f)));
                        Prims.SignText(view.transform,"灯谜"+(i%3+1),new Vector3(-3+i*2,2.9f,1.8f),1,.2f,ArtDirection.Cream);
                    }
                    Prims.SignText(view.transform,"花灯会 · 猜谜／做灯／逛摊",new Vector3(0,3.1f,0),5,.35f,ArtDirection.Cream);
                }
                for(int i=0;i<3;i++)
                {
                    int hung=festival.Hung[i];var lamp=view.transform.Find("Lamp"+i);var square=view.transform.Find("SquareLamp"+i);
                    lamp.gameObject.SetActive(hung>=0&&hung%3!=2);square.gameObject.SetActive(hung>=0&&hung%3==2);
                    if(hung>=0)
                    {
                        lamp.localScale=hung%3==0?Vector3.one*.65f:new Vector3(.85f,.4f,.85f);
                        lamp.GetComponent<Renderer>().sharedMaterial=square.GetComponent<Renderer>().sharedMaterial=hung>=3?_communityGoldLight:_communityPaperLight;
                    }
                }
                NeighborhoodFigure("CommunityLanternHost","灯谜与挂灯摊主",venue.Root.transform,new Vector3(4,0,-4.5f));
            }
            else {HideNeighborhood("CommunityLanternFestival");HideNeighborhood("CommunityLanternHost");}
        }
    }
}
