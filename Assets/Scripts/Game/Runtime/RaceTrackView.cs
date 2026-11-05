using System;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed class RaceTrackView:MonoBehaviour
    {
        public RaceTrack Track {get;private set;}
        private Transform _moving,_player,_gate;private readonly Transform[] _rivals=new Transform[2];
        public void Build(RaceTrack track)
        {
            Track=track;transform.position=new Vector3((float)track.X,.65f,(float)track.Y);
            var scenery=new GameObject("CircuitScenery").transform;scenery.SetParent(transform,false);
            Prims.Box(scenery,"IslandBank",Vector3.down*.4f,new Vector3((float)track.Width+10,.7f,(float)track.Depth+10),Mats.Lit(ArtDirection.Hex("B9AD8B")));
            Prims.Box(scenery,"Infield",Vector3.zero,new Vector3((float)track.Width,.15f,(float)track.Depth),Mats.Lit(ArtDirection.Sage));
            var resources=gameObject.AddComponent<CityLandscapeResources>();
            Ribbon(scenery,track,(float)track.HalfRoad+1.3f,.14f,Mats.Lit(ArtDirection.Cream),resources);
            Ribbon(scenery,track,(float)track.HalfRoad,.18f,Mats.Lit(ArtDirection.Hex("39434A")),resources);
            for(int i=0;i<track.Points.Length;i++)
            {
                var a=Point(i);var b=Point(i+1);var delta=b-a;var normal=Vector3.Cross(Vector3.up,delta.normalized);
                foreach(int side in new[]{-1,1})
                {
                    var border=Prims.Box(scenery,"Barrier",(a+b)*.5f+normal*(float)(side*(track.HalfRoad+3.8))+Vector3.up*.6f,new Vector3(.65f,1.1f,delta.magnitude+.4f),Mats.Lit(i%2==0?ArtDirection.Clay:ArtDirection.Cream));border.transform.rotation=Quaternion.LookRotation(delta);
                }
            }
            var sim=new RaceSimulation(track,MotorCatalog.All[0],true);double heading;var start=sim.AtDistance(0,out heading);
            var line=Prims.Box(scenery,"StartFinish",new Vector3((float)start.X,.22f,(float)start.Y),new Vector3((float)track.HalfRoad*2,.04f,1.2f),Mats.Lit(ArtDirection.Cream));line.transform.rotation=Quaternion.Euler(0,(float)heading*Mathf.Rad2Deg,0);
            for(int i=0;i<4;i++)Prims.Box(scenery,"Grandstand",new Vector3(-10,.6f+i*.6f,5+i*2),new Vector3(24,.7f,2),Mats.Lit(i%2==0?ArtDirection.Wood:ArtDirection.Cream));
            Prims.Box(scenery,"PitOffice",new Vector3(15,2.2f,8),new Vector3(12,4.3f,6),Mats.Lit(ArtDirection.Cream));
            Prims.Box(scenery,"PitRoof",new Vector3(15,4.5f,8),new Vector3(14,.5f,8),Mats.Lit(ArtDirection.Blue));
            for(int i=0;i<4;i++){Prims.Box(scenery,"FloodlightPole",new Vector3((i%2==0?-1:1)*(float)track.Width*.43f,4,(i<2?-1:1)*(float)track.Depth*.43f),new Vector3(.3f,8,.3f),Mats.Lit(ArtDirection.Ink));}
            StaticBatchingUtility.Combine(scenery.gameObject);
            _moving=new GameObject("RaceVehicles").transform;_moving.SetParent(transform,false);_moving.gameObject.SetActive(false);
        }
        private Vector3 Point(int i){var p=Track.Points[i%Track.Points.Length];return new Vector3((float)p.X,0,(float)p.Y);}
        private static void Ribbon(Transform parent,RaceTrack t,float half,float height,Material mat,CityLandscapeResources resources)
        {
            int n=t.Points.Length;var v=new Vector3[n*2];var tri=new int[n*6];
            for(int i=0;i<n;i++){var prev=t.Points[(i+n-1)%n];var next=t.Points[(i+1)%n];var tangent=new Vector3((float)(next.X-prev.X),0,(float)(next.Y-prev.Y)).normalized;var normal=Vector3.Cross(Vector3.up,tangent);var at=new Vector3((float)t.Points[i].X,height,(float)t.Points[i].Y);v[i*2]=at-normal*half;v[i*2+1]=at+normal*half;int j=(i+1)%n;int k=i*6;tri[k]=i*2;tri[k+1]=j*2;tri[k+2]=i*2+1;tri[k+3]=i*2+1;tri[k+4]=j*2;tri[k+5]=j*2+1;}
            var mesh=new Mesh{name="RaceRoad"};mesh.vertices=v;mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateBounds();resources.Owned.Add(mesh);
            var go=new GameObject("RoadRibbon",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        public void Begin(RaceSimulation race)
        {
            foreach(Transform c in _moving)Destroy(c.gameObject);
            _moving.gameObject.SetActive(true);_player=MotorVisual.Build(_moving,race.Model,ArtDirection.Hex("FFD35A"));
            for(int i=0;i<2;i++){_rivals[i]=MotorVisual.Build(_moving,race.Model,i==0?ArtDirection.Clay:ArtDirection.Blue);_rivals[i].gameObject.SetActive(!race.Practice);}
            _gate=Prims.Box(_moving,"NextCheckpoint",Vector3.zero,new Vector3((float)Track.HalfRoad*2,.12f,1),Mats.Unlit(new Color(.3f,1,.55f))).transform;
            Sync(race);
        }
        public void Sync(RaceSimulation r)
        {
            PlaceMotor(_player,r.X,r.Y,r.Heading);
            for(int i=0;i<2;i++){double h;var p=r.AtDistance(r.RivalProgress[i],out h);PlaceMotor(_rivals[i],p.X+(i==0?-1:1)*Math.Cos(h)*2,p.Y-(i==0?-1:1)*Math.Sin(h)*2,h);}
            var next=Point(r.Next);_gate.localPosition=next+Vector3.up*.27f;_gate.localRotation=Quaternion.LookRotation(Point(r.Next+1)-Point(r.Next));
        }
        private static void PlaceMotor(Transform t,double x,double y,double h){t.localPosition=new Vector3((float)x,.23f,(float)y);t.localRotation=Quaternion.Euler(0,(float)h*Mathf.Rad2Deg,0);}
        public void End()=>_moving.gameObject.SetActive(false);
    }
    public static class MotorVisual
    {
        public static Transform Build(Transform parent,MotorModel model,Color color)
        {
            if(MotorCatalog.ClassOf(model.Kind)==2)
            {
                var car=Prims.ParkedCar(parent,Vector3.zero,color).transform;car.localRotation=Quaternion.identity;
                if(model.Price>=500000){car.localScale=new Vector3(1.12f,.75f,1.05f);Prims.Box(car,"RearWing",new Vector3(0,1.2f,-1.35f),new Vector3(1.9f,.12f,.45f),Mats.Lit(ArtDirection.Ink));}
                if(model.Kind==VehicleKind.Van)Prims.Box(car,"CargoCabin",new Vector3(0,1.3f,-.6f),new Vector3(1.5f,1.2f,2.2f),Mats.Lit(color));
                return car;
            }
            var root=new GameObject(model.Name).transform;root.SetParent(parent,false);bool three=model.Kind==VehicleKind.Tricycle;
            Prims.Box(root,"Frame",new Vector3(0,.5f,0),new Vector3(.45f,.3f,1.7f),Mats.Lit(color));
            Prims.Box(root,"Seat",new Vector3(0,.8f,-.35f),new Vector3(.55f,.16f,.7f),Mats.Lit(ArtDirection.Ink));
            Prims.Box(root,"Handlebar",new Vector3(0,1.1f,.65f),new Vector3(.9f,.12f,.12f),Mats.Lit(ArtDirection.Ink));
            for(int i=0;i<(three?3:2);i++)
            {
                var wheel=Prims.Shape(PrimitiveType.Cylinder,root,"Wheel",new Vector3(three&&i>0?(i==1?-.65f:.65f):0,.3f,i==0?.7f:-.7f),new Vector3(.58f,.12f,.58f),Mats.Lit(ArtDirection.Ink));wheel.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            if(three)Prims.Box(root,"CargoBed",new Vector3(0,.7f,-.8f),new Vector3(1.4f,.4f,1),Mats.Lit(color));
            Prims.Box(root,"RiderJacket",new Vector3(0,1.15f,-.15f),new Vector3(.5f,.7f,.35f),Mats.Lit(color));
            Prims.Shape(PrimitiveType.Sphere,root,"Helmet",new Vector3(0,1.7f,-.1f),Vector3.one*.48f,Mats.Lit(ArtDirection.Cream));return root;
        }
    }
}
