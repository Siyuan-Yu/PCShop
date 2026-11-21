using System;
using System.IO;
using System.Linq;
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
        private FootballPenaltyView _footballPenaltyView;
        private Transform _onsiteBall,_onsiteTarget,_onsiteDefender,_onsiteCup,_onsitePlayer;
        private FigureMotion _onsiteShooterMotion;
        private LineRenderer _onsiteAim,_onsiteTrail;
        private string _onsiteSceneKey;
        private const float BasketballCameraShoulder = .72f, BasketballCameraHeight = 2f, BasketballCameraDistance = 6.5f;
        private float _basketballViewYaw, _basketballViewPitch;
        private Vector2 _basketballStartingView;
        public RenderTexture OnsiteTexture=>_onsiteTexture;
        public string OnsitePhotoPath(string file)=>Path.Combine(Application.persistentDataPath,"Photos",Path.GetFileName(file));
        public void HideOnsiteScene()
        {
            if (_onsiteScene != null) _onsiteScene.SetActive(false);
            _footballPenaltyView?.Close();
            _footballPenaltyView = null;
        }

        public void TurnOnsiteBasketballView(float yaw, float pitch)
        {
            _basketballViewYaw = Mathf.Repeat(_basketballViewYaw + yaw, 360f);
            _basketballViewPitch = Mathf.Clamp(_basketballViewPitch + pitch, -55f, 55f);
        }

        private void InitializeOnsiteBasketballView(OnsitePlayState shot)
        {
            var rim = new Vector2((float)shot.TargetX, (float)shot.TargetZ);
            float rimDistance = rim.magnitude;
            float shoulder = Mathf.Asin(Mathf.Clamp01(BasketballCameraShoulder / rimDistance));
            float yaw = (Mathf.Atan2(rim.x, rim.y) - shoulder) * Mathf.Rad2Deg;
            float distance = rimDistance * Mathf.Cos(shoulder) + BasketballCameraDistance;
            float pitch = -Mathf.Atan2((float)OnsitePlayState.BasketballRimHeight - BasketballCameraHeight, distance) * Mathf.Rad2Deg;
            _basketballStartingView = new Vector2(yaw, pitch);
            ResetOnsiteBasketballView();
        }

        public void ResetOnsiteBasketballView()
        {
            _basketballViewYaw = _basketballStartingView.x;
            _basketballViewPitch = _basketballStartingView.y;
        }

        public bool MoveOnsiteBasketball(Vector2 movement)
        {
            var step = Quaternion.Euler(0, _basketballViewYaw, 0) * new Vector3(movement.x, 0, movement.y);
            return Session.MoveOnsiteBasketball(step.x, step.z);
        }

        private (Vector3 Position, Quaternion Rotation) BasketballCameraPose(OnsitePlayState shot)
        {
            var player = new Vector3((float)shot.BasketballPlayerX, BasketballCameraHeight, (float)shot.BasketballOriginZ);
            var shoulder = new Vector3(BasketballCameraShoulder, 0, -BasketballCameraDistance);
            var position = player + Quaternion.Euler(0, _basketballViewYaw, 0) * shoulder;
            return (position, Quaternion.Euler(_basketballViewPitch, _basketballViewYaw, 0));
        }

        public void AimOnsiteBasketballFromView(OnsitePlayState shot)
        {
            var (position, rotation) = BasketballCameraPose(shot);
            var forward = rotation * Vector3.forward;
            float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float heightOffset = 0;
            if (forward.z > .05f)
            {
                // Translate the center ray from the shoulder camera to the ball's release point.
                float depth = (float)OnsitePlayState.BasketballRimDepth - position.z;
                var target = position + forward * (depth / forward.z);
                angle = Mathf.Atan2(target.x - (float)shot.BasketballPlayerX, (float)shot.TargetZ) * Mathf.Rad2Deg;
                heightOffset = target.y - (float)OnsitePlayState.BasketballRimHeight;
            }
            Session.AimOnsiteBasketball(angle, heightOffset);
        }

        private void OnDestroy()
        {
            HideOnsiteScene();
            if (_onsiteScene != null) Kill.It(_onsiteScene);
            if (_onsiteTexture != null) { _onsiteTexture.Release(); Kill.It(_onsiteTexture); }
        }

        private void EnsureOnsiteTexture()
        {
            if (_onsiteTexture != null) return;
            _onsiteTexture = new RenderTexture(1600, 900, 24);
            _onsiteTexture.Create();
        }

        public void AimFootballPenalty(Vector2 viewport)
        {
            if (_footballPenaltyView != null && _footballPenaltyView.TryAim(viewport, out float x))
                Session.AimFootballPenalty(x);
        }

        private void RenderFootballPenalty(OnsitePlayState shot)
        {
            if (_footballPenaltyView == null)
            {
                if (World == null || Cam == null
                    || !World.Places.TryGetValue(CityLifeCatalog.CommunityFootballVenueId, out var venue)) return;
                EnsureOnsiteTexture();
                _footballPenaltyView = new FootballPenaltyView(venue, Session, Cam, _onsiteTexture);
            }
            _footballPenaltyView.Render(shot, Session.ManualAttemptsLeft > 0, Cam.backgroundColor);
        }
        private void OnsiteLayer(Transform t){t.gameObject.layer=28;foreach(Transform child in t)OnsiteLayer(child);}
        public void RenderOnsiteScene()
        {
            var s=Session?.CurrentOnsite;if(s==null)return;
            if (s.IsFootballPenalty) { RenderFootballPenalty(s); return; }
            string key=s.ActivityId+":"+(s.Number==76?s.Subject:0);
            if(_onsiteScene==null||key!=_onsiteSceneKey)
            {
                if(_onsiteScene!=null){_onsiteScene.SetActive(false);Kill.It(_onsiteScene);}_onsiteSceneKey=key;_onsiteBall=_onsiteTarget=_onsiteDefender=_onsiteCup=_onsitePlayer=null;_onsiteAim=_onsiteTrail=null;
                _onsiteShooterMotion = null;
                _onsiteScene=new GameObject("OnsitePlayableScene");_onsiteScene.transform.position=new Vector3(6000,0,6000);var root=_onsiteScene.transform;
                EnsureOnsiteTexture();
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
                        var p = a.Participants[i];
                        var figure = Prims.FigureForPerson(root, Session.CityPerson(p.PersonId)?.Name,
                            CrowdView.PersonClothing(p.PersonId), ArtDirection.Cream, 1, p.PersonId);
                        CityNpcClothing.Apply(figure, Session, p.PersonId);
                        figure.transform.localPosition=new Vector3(-2.5f+i*2.5f,0,2.5f);figure.GetComponent<FigureMotion>().LocomotionEnabled=false;
                        Prims.SignText(root,Session.CityPerson(p.PersonId)?.Name,new Vector3(-2.5f+i*2.5f,2.1f,2.5f),2.4f,.3f,ArtDirection.Cream);
                    }
                    _onsiteCamera.orthographic=true;_onsiteCamera.orthographicSize=4.4f;_onsiteCamera.transform.localPosition=new Vector3(6,7,-8);_onsiteCamera.transform.LookAt(root.TransformPoint(new Vector3(0,1.2f,1.5f)));
                }
                else if(s.Number<=56)
                {
                    BuildOnsiteHalfCourt(root, cream);
                    Prims.Box(root,"Backboard",new Vector3(0,3.5f,8.5f),new Vector3(1.8f,1.1f,.12f),cream);
                    Prims.Box(root,"HoopPost",new Vector3(0,1.6f,9),new Vector3(.2f,3.2f,.2f),sage);
                    var hoop = new Vector3(0, (float)OnsitePlayState.BasketballRimHeight, (float)OnsitePlayState.BasketballRimDepth);
                    var rim = Mats.Lit(ArtDirection.Clay);
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 12;
                        var edge = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                        Prims.Box(root, "Rim", hoop + edge * .3f, new Vector3(.14f,.07f,.14f), rim);
                        if (i % 2 != 0) continue;
                        var top = hoop + edge * .3f;
                        var bottom = hoop + edge * .24f - Vector3.up * .42f;
                        var strand = Prims.Box(root, "NetStrand", (top + bottom) * .5f,
                            new Vector3(.018f, Vector3.Distance(top, bottom), .018f), cream);
                        strand.transform.localRotation = Quaternion.FromToRotation(Vector3.up, top - bottom);
                    }
                    _onsiteBall = Prims.Shape(PrimitiveType.Sphere, root, "LiveBasketball", Vector3.zero,
                        Vector3.one * .42f, Mats.Lit(ArtDirection.Hex("F66A1B"))).transform;
                    string opponentId = Session.RunningCityActivity.Participants.FirstOrDefault()?.PersonId;
                    string opponentName = Session.CityPerson(opponentId)?.Name ?? "同行者";
                    var opponent = Prims.FigureForPerson(root, opponentName, opponentId == null ? ArtDirection.Sage : CrowdView.PersonClothing(opponentId),
                        ArtDirection.Cream, 1, opponentId);
                    if (opponentId != null) CityNpcClothing.Apply(opponent, Session, opponentId);
                    _onsiteDefender = opponent.transform;
                    opponent.GetComponent<FigureMotion>().LocomotionEnabled = false;
                    Prims.SignText(_onsiteDefender, opponentName, new Vector3(0, 2.2f, 0), 2.6f, .26f, ArtDirection.Cream);
                    _onsitePlayer=Prims.Figure(root,"PlayerShooting",ArtDirection.Clay,ArtDirection.Cream,1,FigureAppearance.Player).transform;
                    _onsiteShooterMotion = _onsitePlayer.GetComponent<FigureMotion>();
                    _onsiteShooterMotion.LocomotionEnabled = true;
                    _onsiteShooterMotion.PresentationAnimation = true;
                    _onsiteCamera.orthographic = false;
                    _onsiteCamera.fieldOfView = 58;
                    _onsiteCamera.backgroundColor = ArtDirection.Hex("ADC9D1");
                    light.transform.localRotation = Quaternion.Euler(43, -32, 0);
                    InitializeOnsiteBasketballView(s);
                }
                else if(s.Number<=58)
                {
                    Prims.Box(root,"GolfDrivingRange",new Vector3(0,-.15f,95),new Vector3(110,.3f,220),sage);
                    for(int z=20;z<=180;z+=20){Prims.Box(root,"DistanceLine",new Vector3(0,.02f,z),new Vector3(80,.03f,.12f),cream);Prims.SignText(root,z+"米",new Vector3(-40,.2f,z),8,1.5f,ArtDirection.Cream);}
                    _onsiteTarget=new GameObject("GolfTarget").transform;_onsiteTarget.SetParent(root,false);
                    Prims.Shape(PrimitiveType.Cylinder,_onsiteTarget,"TargetCircle",new Vector3(0,.03f,0),new Vector3(16,.04f,16),wood);
                    Prims.Box(_onsiteTarget,"FlagPole",new Vector3(0,2,0),new Vector3(.2f,4,.2f),cream);Prims.Box(_onsiteTarget,"Flag",new Vector3(1,3.5f,0),new Vector3(2,1,.1f),Mats.Lit(ArtDirection.Clay));
                    _onsiteBall=Prims.Shape(PrimitiveType.Sphere,root,"LiveGolfBall",new Vector3(0,.3f,0),Vector3.one*1.1f,cream).transform;
                    _onsitePlayer=Prims.Figure(root,"PlayerGolfing",ArtDirection.Clay,ArtDirection.Cream,1,FigureAppearance.Player).transform;_onsitePlayer.localScale=Vector3.one*2.5f;_onsitePlayer.GetComponent<FigureMotion>().LocomotionEnabled=false;
                    _onsiteCamera.orthographic=true;_onsiteCamera.orthographicSize=83;_onsiteCamera.transform.localPosition=new Vector3(90,135,-45);_onsiteCamera.transform.LookAt(root.TransformPoint(new Vector3(0,0,75)));
                }
                else
                {
                    Prims.Box(root,"ExhibitionPlinth",new Vector3(0,-.13f,0),new Vector3(2,.2f,2),wood);
                    ExpoProductVisual.Build(root,GameSession.OnsitePhotoSubjects[s.Subject]);
                    Prims.Box(root,"ExhibitBackdrop",new Vector3(0,1.5f,2),new Vector3(10,5,.1f),sage);
                    _onsiteCamera.orthographic=false;_onsiteCamera.fieldOfView=42;
                }
                if(s.Number is >= 57 and <= 58)
                {
                    LineRenderer Line(string name,Color color,float width){var go=new GameObject(name);go.transform.SetParent(root,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.startWidth=line.endWidth=width;line.material=Mats.Unlit(color);return line;}
                    _onsiteAim=Line("AimDirection",ArtDirection.Cream,.3f);
                    _onsiteTrail=Line("BallFlightTrail",ArtDirection.Brass,.35f);
                }
                OnsiteLayer(root);
            }
            _onsiteScene.SetActive(true);var t=_onsiteScene.transform;
            bool basketball = s.IsBasketball;
            float origin = basketball ? (float)s.BasketballOriginZ : 0;
            float playerX = basketball ? (float)s.BasketballPlayerX : 0;
            if (basketball)
            {
                var (position, rotation) = BasketballCameraPose(s);
                _onsiteCamera.transform.localPosition = position;
                _onsiteCamera.transform.localRotation = rotation;
            }
            if(_onsiteBall!=null)_onsiteBall.localPosition=new Vector3(playerX+(float)s.BallX,(float)s.BallY,(float)s.BallZ+origin);
            if (_onsiteDefender != null)
            {
                bool showDefense = s.Number == 56 || s.Number == 54 && s.BasketballDrill;
                _onsiteDefender.gameObject.SetActive(showDefense);
                _onsiteDefender.localPosition = new Vector3((float)s.DefenderX, 0, 4.5f);
            }
            if(_onsiteTarget!=null)_onsiteTarget.localPosition=new Vector3((float)s.TargetX,0,(float)s.TargetZ);
            if(_onsiteCup!=null)
            {_onsiteCup.localPosition=new Vector3(0,s.Pending is "sip" or "taste"?1.9f:1.5f,.8f);_onsiteCup.localRotation=Quaternion.Euler(s.Pending is "sip" or "taste"?-25:0,0,0);}
            if(_onsitePlayer!=null)
            {
                _onsitePlayer.localPosition=new Vector3(-.55f,0,origin-.2f);
                if (_onsiteShooterMotion != null)
                {
                    float release = s.Phase == "flight" && !s.BasketballDrill ? Mathf.Clamp01(1 - (float)s.Flight / .32f) : 0;
                    float step = s.BasketballDrill ? (float)s.BallZ : 0;
                    var rotation = Quaternion.Euler(0, s.BasketballDrill ? 0 : (float)s.Angle, 0);
                    _onsitePlayer.localPosition = new Vector3(playerX, .22f * release, origin + step)
                        + rotation * new Vector3(-.38f, 0, -.2f);
                    _onsitePlayer.localRotation = rotation;
                    var reach = s.BasketballDrill ? _onsiteBall.localPosition : new Vector3(playerX, 1.5f + .45f * release, origin + .16f);
                    float reachWeight = s.Phase == "flight" && !s.BasketballDrill ? release : s.Phase == "result" ? 0 : 1;
                    _onsiteShooterMotion.SetReachPose(t.TransformPoint(reach), reachWeight, s.BasketballDrill ? .22f : 0, false);
                }
            }
            if (_onsiteAim != null && _onsiteTrail != null)
            {
                float power = s.Phase == "charging" ? (float)s.Power : .65f;
                float range = new[] { 100f, 180, 55 }[s.Mode] * power * power;
                bool showAim = s.Phase != "flight";
                _onsiteAim.positionCount = showAim ? 2 : 0;
                if (showAim)
                {
                    float yaw = (float)s.Angle * Mathf.Deg2Rad;
                    _onsiteAim.SetPosition(0, new Vector3(playerX, .08f, origin));
                    _onsiteAim.SetPosition(1, new Vector3(playerX + Mathf.Sin(yaw) * range + (float)s.Wind, .08f, origin + Mathf.Cos(yaw) * range));
                }
                _onsiteTrail.positionCount = s.Phase is ("flight" or "result") ? 32 : 0;
                double progress = s.Phase == "flight" ? Math.Min(1, s.Flight / 2.6) : 1;
                for (int i = 0; i < _onsiteTrail.positionCount; i++)
                {
                    double f = progress * i / 31;
                    float y = .2f + Mathf.Sin(Mathf.PI * (float)f) * Mathf.Max(3, (float)s.Distance * .2f);
                    _onsiteTrail.SetPosition(i, new Vector3((float)(s.LandingX * f), y, (float)(s.LandingZ * f)));
                }
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
        private static void BuildOnsiteHalfCourt(Transform root, Material stripe)
        {
            var grass = Mats.Lit(ArtDirection.Hex("84956A"));
            var clay = Mats.Lit(ArtDirection.Hex("B97A5D"));
            var key = Mats.Lit(ArtDirection.Hex("A3654C"));
            var fence = Mats.Lit(ArtDirection.Hex("CED5BA"));
            var bark = Mats.Lit(ArtDirection.Hex("8D6848"));
            var leaves = Mats.Lit(ArtDirection.Hex("688753"));
            Prims.Box(root, "CourtSurround", new Vector3(0, -.30f, 13), new Vector3(74, .40f, 62), grass);
            Prims.Box(root, "PlayableBasketballCourt", new Vector3(0, -.10f, 2.5f), new Vector3(18, .20f, 16), clay);
            Prims.Box(root, "PaintedKey", new Vector3(0, .004f, 7.15f), new Vector3(4.8f, .007f, 5.5f), key);

            void Mark(Vector2 from, Vector2 to)
            {
                Vector2 delta = to - from, center = (from + to) * .5f;
                var segment = Prims.Box(root, "CourtMarking", new Vector3(center.x, .018f, center.y),
                    new Vector3((float)OnsitePlayState.BasketballCourtLineWidth, .014f, delta.magnitude + .022f), stripe);
                segment.transform.localRotation = Quaternion.Euler(0, Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, 0);
            }

            foreach (float x in new[] { -8.6f, 8.6f })
                Mark(new Vector2(x, -5.1f), new Vector2(x, 10.0f));
            Mark(new Vector2(-8.6f, 10), new Vector2(8.6f, 10));
            Mark(new Vector2(-8.6f, -5.1f), new Vector2(8.6f, -5.1f));
            foreach (float x in new[] { -2.4f, 2.4f })
                Mark(new Vector2(x, 4.4f), new Vector2(x, 10));
            Mark(new Vector2(-2.4f, 4.4f), new Vector2(2.4f, 4.4f));

            void Arc(float radius, float z)
            {
                Vector2 last = new Vector2(radius, z);
                for (int i = 1; i <= 24; i++)
                {
                    float theta = i * Mathf.PI / 24;
                    var next = new Vector2(radius * Mathf.Cos(theta), z - radius * Mathf.Sin(theta));
                    Mark(last, next);
                    last = next;
                }
            }
            Arc(2.4f, 4.4f);
            Arc((float)OnsitePlayState.BasketballThreePointRadius, (float)OnsitePlayState.BasketballRimDepth);
            Prims.Box(root, "CourtFenceRail", new Vector3(0, .87f, 13), new Vector3(31, .16f, .15f), fence);
            for (int i = -4; i <= 4; i++)
                Prims.Box(root, "CourtFencePost", new Vector3(i * 3.8f, .53f, 13), new Vector3(.15f, 1.22f, .15f), fence);
            for (int i = 0; i < 7; i++)
            {
                float x = -19 + 6.6f * i, z = 16.5f + i % 2 * 3f;
                Prims.Shape(PrimitiveType.Cylinder, root, "TreeTrunk", new Vector3(x, 1.2f, z), new Vector3(.41f, 1.25f, .41f), bark);
                Prims.Shape(PrimitiveType.Sphere, root, "TreeCrown", new Vector3(x, 3.45f, z), new Vector3(3.3f, 3.9f, 3.4f), leaves);
                Prims.Shape(PrimitiveType.Sphere, root, "TreeCrown", new Vector3(x + 1.05f, 3.25f, z + .45f), new Vector3(2.15f, 2.65f, 2.25f), leaves);
            }
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
