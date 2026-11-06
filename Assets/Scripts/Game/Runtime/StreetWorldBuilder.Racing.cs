using System.Collections.Generic;
using UnityEngine;
namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private static void BuildRaceEntrance(Transform root,Vector3 door,PCShopSell.Core.Session.RaceTrack track)
        {
            var edge=new Vector3(
                Mathf.Clamp(door.x,(float)(track.X-track.Width/2+4),(float)(track.X+track.Width/2-4)),0,
                Mathf.Clamp(door.z,(float)(track.Y-track.Depth/2+4),(float)(track.Y+track.Depth/2-4)));
            var delta=edge-door;delta.y=0;
            if(delta.sqrMagnitude<.01f)return;
            var landing=Prims.Box(root,"RaceEntranceLanding",root.InverseTransformPoint((door+edge)*.5f)+Vector3.up*.02f,
                new Vector3(10,.6f,delta.magnitude+10),Mats.Lit(ArtDirection.Hex("858B77")));
            landing.transform.rotation=Quaternion.LookRotation(delta);
            var resources=root.gameObject.AddComponent<CityLandscapeResources>();
            var normal=Vector3.Cross(Vector3.up,delta.normalized)*3.5f;
            var start=door-delta.normalized*5+Vector3.up*.26f;var end=edge+delta.normalized*3+Vector3.up*.82f;
            var vertices=new[]{root.InverseTransformPoint(start-normal),root.InverseTransformPoint(end-normal),root.InverseTransformPoint(start+normal),root.InverseTransformPoint(end+normal)};
            var mesh=new Mesh{name="RaceEntranceRamp"};mesh.vertices=vertices;mesh.triangles=new[]{0,1,2,2,1,3};mesh.RecalculateNormals();mesh.RecalculateBounds();resources.Owned.Add(mesh);
            var ramp=new GameObject("RaceEntranceRamp",typeof(MeshFilter),typeof(MeshRenderer));ramp.transform.SetParent(root,false);
            ramp.GetComponent<MeshFilter>().sharedMesh=mesh;ramp.GetComponent<MeshRenderer>().sharedMaterial=Mats.Lit(ArtDirection.Hex("41494D"));
        }
        private void BuildRaceCauseways()
        {
            var root=new GameObject("LakeRaceCauseways").transform;root.SetParent(_root,false);
            var done=new HashSet<string>();
            foreach(var n in _city.Map.Nodes)foreach(var e in _city.Map.NeighborsOf(n.Id))
            {
                if(!n.Id.StartsWith("race_")&&!e.To.StartsWith("race_")&&!n.Id.StartsWith("motor_")&&!e.To.StartsWith("motor_"))continue;
                string key=string.CompareOrdinal(n.Id,e.To)<0?n.Id+":"+e.To:e.To+":"+n.Id;if(!done.Add(key))continue;
                var a=NodeWorld(n.Id);var b=NodeWorld(e.To);var delta=b-a;var at=(a+b)*.5f;
                // Support sits below the shared asphalt/paving; do not draw a second road or crossing kerbs.
                var road=Prims.Box(root,"RaceAccessCauseway",at-Vector3.up*.06f,new Vector3(9.8f,.48f,delta.magnitude),Mats.Lit(ArtDirection.Hex("858B77")));road.transform.rotation=Quaternion.LookRotation(delta);
            }
            Prims.Box(root,"StarterMotorHubBank",new Vector3(-550,-.005f,-135),new Vector3(65,.45f,65),Mats.Lit(ArtDirection.Sage));
            StaticBatchingUtility.Combine(root.gameObject);
        }
    }
}
