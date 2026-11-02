using System;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private PlaceVisual CityCloseView
        {
            get
            {
                if(Session==null||World==null)return null;
                Place place=Session.CurrentPlace;
                // Waiting at a closed activity entrance is a real arrival, too. Cancelling the route
                // or walking away immediately restores the player's street camera.
                if(place==null && Session.Trip==null && Session.CityIntent!=null && !Session.CityIntent.Cancelled
                    && Session.CityIntent.EnterOnArrival && Session.City.HasPlace(Session.CityIntent.TargetPlaceId))
                {
                    var target=Session.City.GetPlace(Session.CityIntent.TargetPlaceId);
                    if(World.Places.TryGetValue(target.Id,out var waiting))
                    {
                        Vector3 delta=waiting.Entrance-StreetWorldBuilder.ToWorld(Session.PlayerPosition);delta.y=0;
                        if(delta.sqrMagnitude<=16 && waiting.Exterior==null)place=target;
                    }
                }
                if(place==null || !((place.Business??"").StartsWith("venue_",StringComparison.Ordinal)||place.Business=="服装"||Session.IsCityActivityVenue(place)))return null;
                return World.Places.TryGetValue(place.Id,out var visual)?visual:null;
            }
        }
        private Vector3 CityCloseFocus(PlaceVisual visual)
        {
            float depth=visual.Place.ExplicitEntrance?Mathf.Max(6,(float)visual.Place.Depth):8;
            // The forecourt holds participants, while tables, rails and counters remain in frame.
            if(!visual.Place.ExplicitEntrance&&Session.IsCityActivityVenue(visual.Place))return StreetWorldBuilder.ToWorld(Session.PlayerPosition)+Vector3.up*.8f;
            if(!visual.Place.ExplicitEntrance)return new Vector3(visual.Entrance.x,.8f,StreetWorldBuilder.FrontZAt(visual.Entrance.x)+depth*.24f);
            return visual.Root.transform.TransformPoint(new Vector3(0,.8f,Mathf.Clamp(depth*.24f,1.5f,3)));
        }
    }
}
