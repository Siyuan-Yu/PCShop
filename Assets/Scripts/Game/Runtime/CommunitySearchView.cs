using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed class CommunitySearchView : MonoBehaviour
    {
        private GameController _game;
        private GameObject _boxes;
        private readonly GameObject[] _props=new GameObject[3];
        public void Initialize(GameController game){_game=game;}
        public void Refresh()
        {
            var s=_game?.Session;
            if(s==null)return;
            bool visible=s.CommunitySearchActive&&!_game.InHome&&!_game.InInterior;
            if(!visible){if(_boxes!=null)_boxes.SetActive(false);return;}
            if(_boxes==null)
            {
                _boxes=new GameObject("CommunitySearchBoxes");_boxes.transform.SetParent(transform,false);
            }
            for(int i=0;i<3;i++)
            {
                bool available=s.CommunityBoxAvailable(i);
                if(!available){if(_props[i]!=null)_props[i].SetActive(false);continue;}
                if(_props[i]==null)
                {
                    var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name="CommunityBox_"+i;_props[i]=box;
                    box.transform.SetParent(_boxes.transform,false);
                    box.transform.position=StreetWorldBuilder.ToWorld(s.CommunityBoxPosition(i))+Vector3.up*.45f;
                    box.transform.localScale=new Vector3(1.1f,.9f,.8f);
                    box.GetComponent<Renderer>().sharedMaterial=Mats.Lit(new Color(.55f,.36f,.18f));
                    box.GetComponent<Collider>().enabled=false;Kill.It(box.GetComponent<Collider>());
                    var band=GameObject.CreatePrimitive(PrimitiveType.Cube);band.transform.SetParent(box.transform,false);
                    band.transform.localScale=new Vector3(.16f,1.02f,1.02f);
                    band.GetComponent<Renderer>().sharedMaterial=Mats.Lit(new[]{new Color(.25f,.65f,.3f),new Color(.25f,.45f,.85f),new Color(.8f,.25f,.2f)}[i]);
                    band.GetComponent<Collider>().enabled=false;Kill.It(band.GetComponent<Collider>());
                }
                _props[i].SetActive(true);
            }
            _boxes.SetActive(true);
        }
        private void LateUpdate()=>Refresh();
    }
}
