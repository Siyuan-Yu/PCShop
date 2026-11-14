using System;
using System.IO;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private GameObject _onsiteScene;
        private Camera _onsiteCamera;
        private RenderTexture _onsiteTexture;
        private Transform _onsiteBall,_onsiteTarget,_onsiteDefender,_onsiteCup,_onsitePlayer;
        private LineRenderer _onsiteAim,_onsiteTrail;
        private string _onsiteSceneKey;
        public RenderTexture OnsiteTexture=>_onsiteTexture;
        public string OnsitePhotoPath(string file)=>Path.Combine(Application.persistentDataPath,"Photos",Path.GetFileName(file));
        public void HideOnsiteScene(){if(_onsiteScene!=null)_onsiteScene.SetActive(false);}
        private void OnDestroy()
        {if(_onsiteScene!=null)Kill.It(_onsiteScene);if(_onsiteTexture!=null){_onsiteTexture.Release();Kill.It(_onsiteTexture);}}
        private void OnsiteLayer(Transform t){t.gameObject.layer=28;foreach(Transform child in t)OnsiteLayer(child);}
        public void RenderOnsiteScene()
        {
            var s=Session?.CurrentOnsite;if(s==null)return;
            string key=s.ActivityId+":"+(s.Number==76?s.Subject:0);
            if(_onsiteScene==null||key!=_onsiteSceneKey)
            {
                if(_onsiteScene!=null){_onsiteScene.SetActive(false);Kill.It(_onsiteScene);}_onsiteSceneKey=key;_onsiteBall=_onsiteTarget=_onsiteDefender=_onsiteCup=_onsitePlayer=null;_onsiteAim=_onsiteTrail=null;
                _onsiteScene=new GameObject("OnsitePlayableScene");_onsiteScene.transform.position=new Vector3(6000,0,6000);var root=_onsiteScene.transform;
                if(_onsiteTexture==null){_onsiteTexture=new RenderTexture(1600,900,24);_onsiteTexture.Create();}
                _onsiteCamera=new GameObject("OnsiteSceneCamera").AddComponent<Camera>();_onsiteCamera.transform.SetParent(root,false);_onsiteCamera.targetTexture=_onsiteTexture;
                _onsiteCamera.cullingMask=1<<28;_onsiteCamera.nearClipPlane=.05f;_onsiteCamera.farClipPlane=600;_onsiteCamera.clearFlags=CameraClearFlags.SolidColor;_onsiteCamera.backgroundColor=new Color(.18f,.28f,.32f);
                _onsiteCamera.enabled=false;
                var light=new GameObject("OnsiteKeyLight").AddComponent<Light>();light.transform.SetParent(root,false);light.type=LightType.Directional;light.intensity=1.2f;light.cullingMask=1<<28;light.transform.localRotation=Quaternion.Euler(55,-35,0);
                var wood=Mats.Lit(ArtDirection.Wood);var cream=Mats.Lit(ArtDirection.Cream);var sage=Mats.Lit(ArtDirection.Sage);var ink=Mats.Lit(ArtDirection.Ink);
                if(s.Number<=53)
                {
                    Prims.Box(root,"BarFloor",new Vector3(0,-.1f,1),new Vector3(14,.2f,10),wood);
                    Prims.Box(root,"BackWall",new Vector3(0,2.2f,5),new Vector3(14,4.4f,.3f),ink);
                    Prims.Box(root,"Counter",new Vector3(0,.65f,1),new Vector3(8,1.3f,1.6f),sage);
                    for(int i=0;i<9;i++)Prims.Shape(PrimitiveType.Cylinder,root,"IngredientBottle"+i,new Vector3(-3+i*.75f,1.75f,3.9f),new Vector3(.23f,.35f,.23f),i%3==0?cream:i%3==1?sage:wood);
                    _onsiteCup=Prims.Shape(PrimitiveType.Cylinder,root,"ActualDrink",new Vector3(0,1.5f,.8f),new Vector3(.45f,.25f,.45f),Mats.Lit(new Color(.6f,.3f,.35f))).transform;
                    Prims.SignText(root,"灯塔吧台",new Vector3(0,3.5f,4.7f),7,.5f,ArtDirection.Cream);
                    var a=Session.RunningCityActivity;for(int i=0;i<a.Participants.Length;i++)
                    {
                        var p=a.Participants[i];var figure=Prims.Figure(root,Session.CityPerson(p.PersonId)?.Name,CrowdView.PersonClothing(p.PersonId),ArtDirection.Cream,1);
                        figure.transform.localPosition=new Vector3(-2.5f+i*2.5f,0,2.5f);figure.GetComponent<FigureMotion>().LocomotionEnabled=false;
                        Prims.SignText(root,Session.CityPerson(p.PersonId)?.Name,new Vector3(-2.5f+i*2.5f,2.1f,2.5f),2.4f,.3f,ArtDirection.Cream);
                    }
                    _onsiteCamera.orthographic=true;_onsiteCamera.orthographicSize=4.4f;_onsiteCamera.transform.localPosition=new Vector3(6,7,-8);_onsiteCamera.transform.LookAt(root.TransformPoint(new Vector3(0,1.2f,1.5f)));
                }
                else if(s.Number<=56)
                {
                    Prims.Box(root,"PlayableBasketballCourt",new Vector3(0,-.1f,6),new Vector3(18,.2f,22),wood);
                    foreach(float x in new[]{-8f,8f})Prims.Box(root,"Sideline",new Vector3(x,.015f,6),new Vector3(.08f,.025f,20),cream);
                    Prims.Box(root,"Backboard",new Vector3(0,3.5f,8.5f),new Vector3(1.8f,1.1f,.12f),cream);
                    Prims.Box(root,"HoopPost",new Vector3(0,1.6f,9),new Vector3(.2f,3.2f,.2f),sage);
                    for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;Prims.Box(root,"Rim",new Vector3(Mathf.Cos(a)*.3f,3.05f,8+Mathf.Sin(a)*.3f),new Vector3(.14f,.07f,.14f),Mats.Lit(ArtDirection.Clay));}
                    _onsiteBall=Prims.Shape(PrimitiveType.Sphere,root,"LiveBasketball",new Vector3(0,1.7f,0),Vector3.one*.35f,Mats.Lit(ArtDirection.Clay)).transform;
                    _onsiteDefender=Prims.Figure(root,"DefendingPartner",ArtDirection.Sage,ArtDirection.Cream,1).transform;_onsiteDefender.GetComponent<FigureMotion>().LocomotionEnabled=false;
                    _onsitePlayer=Prims.Figure(root,"PlayerShooting",ArtDirection.Clay,ArtDirection.Cream,1).transform;_onsitePlayer.GetComponent<FigureMotion>().LocomotionEnabled=false;
                    _onsiteCamera.orthographic=true;_onsiteCamera.orthographicSize=9;_onsiteCamera.transform.localPosition=new Vector3(14,17,-10);_onsiteCamera.transform.LookAt(root.TransformPoint(new Vector3(0,1,4)));
                }
                else if(s.Number<=58)
                {
                    Prims.Box(root,"GolfDrivingRange",new Vector3(0,-.15f,95),new Vector3(110,.3f,220),sage);
                    for(int z=20;z<=180;z+=20){Prims.Box(root,"DistanceLine",new Vector3(0,.02f,z),new Vector3(80,.03f,.12f),cream);Prims.SignText(root,z+"米",new Vector3(-40,.2f,z),8,1.5f,ArtDirection.Cream);}
                    _onsiteTarget=new GameObject("GolfTarget").transform;_onsiteTarget.SetParent(root,false);
                    Prims.Shape(PrimitiveType.Cylinder,_onsiteTarget,"TargetCircle",new Vector3(0,.03f,0),new Vector3(16,.04f,16),wood);
                    Prims.Box(_onsiteTarget,"FlagPole",new Vector3(0,2,0),new Vector3(.2f,4,.2f),cream);Prims.Box(_onsiteTarget,"Flag",new Vector3(1,3.5f,0),new Vector3(2,1,.1f),Mats.Lit(ArtDirection.Clay));
                    _onsiteBall=Prims.Shape(PrimitiveType.Sphere,root,"LiveGolfBall",new Vector3(0,.3f,0),Vector3.one*1.1f,cream).transform;
                    _onsitePlayer=Prims.Figure(root,"PlayerGolfing",ArtDirection.Clay,ArtDirection.Cream,1).transform;_onsitePlayer.localScale=Vector3.one*2.5f;_onsitePlayer.GetComponent<FigureMotion>().LocomotionEnabled=false;
                    _onsiteCamera.orthographic=true;_onsiteCamera.orthographicSize=83;_onsiteCamera.transform.localPosition=new Vector3(90,135,-45);_onsiteCamera.transform.LookAt(root.TransformPoint(new Vector3(0,0,75)));
                }
                else
                {
                    Prims.Box(root,"ExhibitionPlinth",new Vector3(0,-.13f,0),new Vector3(2,.2f,2),wood);
                    ExpoProductVisual.Build(root,GameSession.OnsitePhotoSubjects[s.Subject]);
                    Prims.Box(root,"ExhibitBackdrop",new Vector3(0,1.5f,2),new Vector3(10,5,.1f),sage);
                    _onsiteCamera.orthographic=false;_onsiteCamera.fieldOfView=42;
                }
                if(s.Number>=54&&s.Number<=58)
                {
                    LineRenderer Line(string name,Color color,float width){var go=new GameObject(name);go.transform.SetParent(root,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.startWidth=line.endWidth=width;line.material=Mats.Unlit(color);return line;}
                    _onsiteAim=Line("AimDirection",ArtDirection.Cream,s.Number<=56?.06f:.3f);_onsiteTrail=Line("BallFlightTrail",ArtDirection.Brass,s.Number<=56?.045f:.35f);
                }
                OnsiteLayer(root);
            }
            _onsiteScene.SetActive(true);var t=_onsiteScene.transform;
            if(_onsiteBall!=null)_onsiteBall.localPosition=new Vector3((float)s.BallX,(float)s.BallY,(float)s.BallZ+(s.Number<=56&&s.Mode==1?2.8f:0));
            if(_onsiteDefender!=null)_onsiteDefender.localPosition=new Vector3((float)s.DefenderX,0,4.5f);
            if(_onsiteTarget!=null)_onsiteTarget.localPosition=new Vector3((float)s.TargetX,0,(float)s.TargetZ);
            if(_onsiteCup!=null)
            {_onsiteCup.localPosition=new Vector3(0,s.Pending is "sip" or "taste"?1.9f:1.5f,.8f);_onsiteCup.localRotation=Quaternion.Euler(s.Pending is "sip" or "taste"?-25:0,0,0);}
            if(_onsitePlayer!=null)
            {
                float origin=s.Number<=56&&s.Mode==1?2.8f:0;_onsitePlayer.localPosition=new Vector3(-.55f,0,origin-.2f);
                float power=s.Phase=="charging"?(float)s.Power:.65f;float range=s.Number<=56?(s.Mode==1?8:13)*power:new[]{100f,180,55}[s.Mode]*power*power;
                float yaw=(float)s.Angle*Mathf.Deg2Rad;_onsiteAim.positionCount=2;_onsiteAim.SetPositions(new[]{new Vector3(0,.08f,origin),new Vector3(Mathf.Sin(yaw)*range+(float)s.Wind,.08f,origin+Mathf.Cos(yaw)*range)});
                _onsiteTrail.positionCount=s.Phase is "flight" or "result"?32:0;
                for(int i=0;i<_onsiteTrail.positionCount;i++)
                {float f=(float)(s.Phase=="flight"?Math.Min(1,s.Flight/(s.Number<=56?1.4:2.6)):1)*i/31;float y=s.Number<=56?1.7f*(1-f)+3.05f*f+2.5f*Mathf.Sin(Mathf.PI*f):.2f+Mathf.Sin(Mathf.PI*f)*Mathf.Max(3,(float)s.Distance*.2f);_onsiteTrail.SetPosition(i,new Vector3((float)s.LandingX*f,y,origin+(float)s.LandingZ*f));}
            }
            if(s.Number==76)
            {
                float yaw=(float)s.PhotoYaw*Mathf.Deg2Rad,pitch=(float)s.PhotoPitch*Mathf.Deg2Rad,d=(float)s.PhotoDistance;
                _onsiteCamera.transform.localPosition=new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch)*d,.6f+Mathf.Sin(pitch)*d,-Mathf.Cos(yaw)*Mathf.Cos(pitch)*d);
                _onsiteCamera.transform.LookAt(t.TransformPoint(new Vector3((float)s.PhotoOffset,.6f,0)));
                _onsiteCamera.backgroundColor=Color.Lerp(new Color(.06f,.07f,.08f),new Color(.5f,.6f,.65f),(float)s.Exposure/2);
                t.Find("OnsiteKeyLight").GetComponent<Light>().intensity=.5f+(float)s.Exposure*.8f;
            }
            _onsiteCamera.Render();
        }
        public bool CaptureOnsitePhoto()
        {
            if(Session?.CurrentOnsite?.Number!=76)return false;
            string file="photo-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".png";
            Texture2D frame=null;var previous=RenderTexture.active;
            try
            {
                RenderOnsiteScene();RenderTexture.active=_onsiteTexture;frame=new Texture2D(_onsiteTexture.width,_onsiteTexture.height,TextureFormat.RGB24,false);
                frame.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);frame.Apply();string path=OnsitePhotoPath(file);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,frame.EncodeToPNG());
                if(Session.RecordOnsitePhoto(file,frame.width,frame.height))return true;
                File.Delete(path);return false;
            }
            catch(Exception ex){Debug.LogWarning("Photo capture failed: "+ex.Message);return false;}
            finally {RenderTexture.active=previous;if(frame!=null)Kill.It(frame);}
        }
    }
}
