using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private GameObject _lifeDogView,_lifeLibraryView,_lifeStallView;
        private readonly GameObject[] _lifeBookViews=new GameObject[3];
        private void SyncLifeEventViews()
        {
            if(Session==null||World==null)return;
            var dog=Session.LifeDog;
            if(dog==null||dog.Stage=="rescued")
            {if(_lifeDogView!=null)Kill.It(_lifeDogView);_lifeDogView=null;}
            else
            {
                if(_lifeDogView==null)
                {
                    _lifeDogView=PetSceneModels.BuildAnimal(null,true,ArtDirection.Hex("A66E3B"),false,out var head,out var tail,out var legs);
                    _lifeDogView.name="LifeCompanionDog";
                    _lifeDogView.AddComponent<PetCompanionMotion>().Bind(true,head,tail,legs);
                }
                bool home=dog.Stage=="adopted"&&!Session.LifeDogFollowing;
                Transform parent=null;
                if(home)parent=HomeRoom?.Root;
                else if(Session.LifeDogFollowing)parent=InHome?HomeRoom.Root:InInterior?Interior.Root:World.Root.transform;
                else if(InHome&&dog.Place==Session.Home.Id)parent=HomeRoom.Root;
                else if(InInterior&&dog.Place==Session.CurrentPlace?.Id)parent=Interior.Root;
                else if(World.Places.TryGetValue(dog.Place,out var place))parent=place.Root.transform;
                _lifeDogView.SetActive(parent!=null);
                _lifeDogView.GetComponent<PetCompanionMotion>().Playing=Session.Time.IsRunning;
                if(parent!=null)
                {
                    // 同一父级不重复重置局部朝向；换场景时清掉旧位置的步态差值。
                    if(_lifeDogView.transform.parent!=parent)
                    {_lifeDogView.transform.SetParent(parent,false);_lifeDogView.GetComponent<PetCompanionMotion>().ResetTravel();}
                    if(Session.LifeDogFollowing)_lifeDogView.transform.position=_avatar.Position+new Vector3(.8f,0,.6f);
                    else _lifeDogView.transform.localPosition=new Vector3(1.3f,0,home?1.2f:-2.5f);
                }
            }
            if(World.Places.TryGetValue(GameSession.LifeServicePlace,out var service))
            {
                if(_lifeLibraryView==null)
                {
                    _lifeLibraryView=new GameObject("CommunityLibraryShelf");_lifeLibraryView.transform.SetParent(service.Root.transform,false);_lifeLibraryView.transform.localPosition=new Vector3(-3f,0,-2);
                    Prims.Box(_lifeLibraryView.transform,"BookTable",new Vector3(0,.55f,0),new Vector3(1.8f,.15f,.6f),Mats.Lit(ArtDirection.Sage));
                    for(int i=0;i<3;i++)_lifeBookViews[i]=Prims.Box(_lifeLibraryView.transform,"CommunityBook"+i,new Vector3(-.55f+i*.55f,.8f,0),new Vector3(.4f,.35f,.35f),Mats.Lit(i==0?ArtDirection.Blue:i==1?ArtDirection.Cream:ArtDirection.Sage));
                    Prims.SignText(_lifeLibraryView.transform,"社区图书角",new Vector3(0,1.4f,0),2.1f,.3f,ArtDirection.Cream);
                    Prims.Box(_lifeLibraryView.transform,"NightClassBoard",new Vector3(2.8f,1,0),new Vector3(1.7f,1.2f,.15f),Mats.Lit(ArtDirection.Sage));
                    Prims.SignText(_lifeLibraryView.transform,"夜校 · 19:00",new Vector3(2.8f,1.2f,-.12f),1.6f,.25f,ArtDirection.Cream);
                    Prims.Shape(PrimitiveType.Sphere,_lifeLibraryView.transform,"StreetGuitarBody",new Vector3(4.3f,.65f,0),new Vector3(.4f,.55f,.15f),Mats.Lit(ArtDirection.Cream));
                    Prims.Box(_lifeLibraryView.transform,"StreetGuitarNeck",new Vector3(4.3f,1.15f,0),new Vector3(.1f,.7f,.12f),Mats.Lit(ArtDirection.Sage));
                }
                var stock=Session.LifeBookStock;for(int i=0;i<3;i++)_lifeBookViews[i].SetActive(stock[i]>0);
            }
            var stall=Session.LifeStall;
            if(stall==null||stall.Done){if(_lifeStallView!=null)Kill.It(_lifeStallView);_lifeStallView=null;}
            else if(_lifeStallView==null&&World.Places.TryGetValue(GameSession.LifeFairPlace,out var market))
            {
                _lifeStallView=new GameObject("PlayerCommunityStall");_lifeStallView.transform.SetParent(market.Root.transform,false);_lifeStallView.transform.localPosition=new Vector3(1.5f,0,-2);
                Prims.Box(_lifeStallView.transform,"StallCounter",new Vector3(0,.5f,0),new Vector3(2,.9f,.8f),Mats.Lit(ArtDirection.Sage));
                Prims.Box(_lifeStallView.transform,"ConsignedGoods",new Vector3(0,1.1f,0),new Vector3(.8f,.3f,.5f),Mats.Lit(ArtDirection.Cream));
                Prims.SignText(_lifeStallView.transform,"周末小摊 · "+stall.Name,new Vector3(0,1.9f,0),3,.3f,ArtDirection.Cream);
            }
        }
    }
}
