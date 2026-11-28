using System.Collections.Generic;
using UnityEngine;
using PCShopSell.Game.Runtime;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private PlaceVisual _office;
        private Vector3 _officeLocal, _officeFacing;
        private List<Rect> _officeFurniture;
        private bool OfficeDoor => Mathf.Abs(_officeLocal.x)<1.25f && _officeLocal.z<1.65f;
        private bool NearOfficeReception => _office != null
            && DistrictOffice.AtReception(_officeLocal,(float)_office.Place.Width,(float)_office.Place.Depth);

        private void InteractOffice()
        {
            if (OfficeDoor) Session.Leave();
            else if (NearOfficeReception) Ui.OpenOfficeReception(_office.Place);
        }

        private Vector3? SyncOffice()
        {
            var place=Session.CurrentPlace;
            if(place?.Model!="office_tower" || !World.Places.TryGetValue(place.Id,out var target))
            {
                ReturnOfficeToStreet();
                return null;
            }
            if(_office!=target)
            {
                ReturnOfficeToStreet();
                _office=target;
                // The saved floor and the street share a threshold, so keep the real door transform.
                // Isolate the whole cutaway from street terrain, trees and static batches just as at home.
                _office.VenueInterior.transform.SetParent(_actors,true);
                _officeLocal=new Vector3(0,.035f,.85f);
                _officeFacing=target.Root.transform.forward;
                _officeFurniture=DistrictOffice.WalkingObstacles((float)place.Width,(float)place.Depth);
            }
            return target.Root.transform.TransformPoint(_officeLocal);
        }

        private void ReturnOfficeToStreet()
        {
            if(_office?.VenueInterior!=null)
            {
                _office.VenueInterior.SetActive(false);
                _office.VenueInterior.transform.SetParent(_office.Root.transform,true);
            }
            _office=null;
        }

        private void WalkOffice(Vector3 world,float seconds)
        {
            var t=_office.Root.transform;
            Vector3 step=t.InverseTransformDirection(world.normalized)*Mathf.Min(seconds,.12f)*3.1f;
            float half=(float)_office.Place.Width*.5f-.26f;
            Vector3 next=_officeLocal;
            next.x=Mathf.Clamp(next.x+step.x,-half,half);
            bool Clear(Vector3 pos)
            {
                foreach(var rect in _officeFurniture)
                    if(rect.Contains(new Vector2(pos.x,pos.z))) return false;
                return true;
            }
            if(Clear(next)) _officeLocal.x=next.x;
            next=_officeLocal;
            next.z=Mathf.Clamp(next.z+step.z,.18f,(float)_office.Place.Depth-.25f);
            if(Clear(next)) _officeLocal.z=next.z;
            _officeFacing=world.normalized;
        }
    }
}
