using System.Collections.Generic;
using PCShopSell.Core.World;
using UnityEngine;
namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private void BuildRegionalGarage(Transform root,Place p,Vector3 node,Vector3 door,PlaceVisual vis)
        {
            // A compact annex beside the existing reception, not a new parking island.
            float w=(float)p.Width,d=(float)p.Depth;
            Prims.Box(root,"GarageFoundation",new Vector3(0,.12f,d*.5f),new Vector3(w+.5f,.45f,d+.5f),Mats.Lit(ArtDirection.Hex("858B77")));
            var body=Prims.Box(root,"GarageAnnex",new Vector3(0,1.8f,d*.5f),new Vector3(w,3.6f,d),Mats.Lit(ArtDirection.Cream),true);
            body.AddComponent<PlaceMarker>().PlaceId=p.Id;
            Prims.Box(root,"GarageRoof",new Vector3(0,3.7f,d*.5f),new Vector3(w+.4f,.25f,d+.4f),Mats.Lit(ArtDirection.Ink));
            Prims.Box(root,"RollerDoor",new Vector3(0,1.5f,-.06f),new Vector3(w-1,2.8f,.15f),Mats.Lit(ArtDirection.Blue));
            for(int i=0;i<7;i++)Prims.Box(root,"DoorSlat",new Vector3(0,.35f+i*.36f,-.16f),new Vector3(w-1,.035f,.025f),Mats.Lit(ArtDirection.Ink));
            Prims.SignText(root,"车库",new Vector3(0,3.2f,-.2f),w-.7f,.45f,ArtDirection.Cream);
            var delta=door-node;
            if(!_city.IsLakeCity && delta.sqrMagnitude>.1f)
            {
                var step=Prims.Box(root,"GarageWalkway",root.InverseTransformPoint((door+node)*.5f)+Vector3.up*.08f,
                    new Vector3(1.8f,.38f,delta.magnitude),Mats.Lit(ArtDirection.Hex("858B77")));
                step.transform.rotation=Quaternion.LookRotation(delta);
            }
            vis.LabelPos=root.TransformPoint(new Vector3(0,4.5f,d*.35f));
        }
        private static void BuildRaceEntrance(Transform root,Vector3 door,PCShopSell.Core.Session.RaceTrack track)
        {
            var edge=new Vector3(
                Mathf.Clamp(door.x,(float)(track.X-track.Width/2+4),(float)(track.X+track.Width/2-4)),0,
                Mathf.Clamp(door.z,(float)(track.Y-track.Depth/2+4),(float)(track.Y+track.Depth/2-4)));
            var delta=edge-door;delta.y=0;
            if(delta.sqrMagnitude<.01f)return;
            var center = (door + edge) * .5f;
            center.y = Mathf.Max(.32f, door.y - .01f) - .3f;
            var landing=Prims.Box(root,"RaceEntranceLanding",root.InverseTransformPoint(center),
                new Vector3(10,.6f,delta.magnitude+10),Mats.Lit(ArtDirection.Hex("858B77")));
            landing.transform.rotation=Quaternion.LookRotation(delta);
            var resources=root.gameObject.AddComponent<CityLandscapeResources>();
            var normal=Vector3.Cross(Vector3.up,delta.normalized)*3.5f;
            var start=door-delta.normalized*5;start.y=Mathf.Max(.26f,door.y+.02f);
            var end=edge+delta.normalized*3+Vector3.up*.82f;
            var vertices=new[]{root.InverseTransformPoint(start-normal),root.InverseTransformPoint(end-normal),root.InverseTransformPoint(start+normal),root.InverseTransformPoint(end+normal)};
            var mesh=new Mesh{name="RaceEntranceRamp"};mesh.vertices=vertices;mesh.triangles=new[]{0,1,2,2,1,3};mesh.RecalculateNormals();mesh.RecalculateBounds();resources.Owned.Add(mesh);
            var ramp=new GameObject("RaceEntranceRamp",typeof(MeshFilter),typeof(MeshRenderer));ramp.transform.SetParent(root,false);
            ramp.GetComponent<MeshFilter>().sharedMesh=mesh;ramp.GetComponent<MeshRenderer>().sharedMaterial=Mats.Lit(ArtDirection.Hex("41494D"));
        }
        private void BuildRaceCauseways()
        {
            var root=new GameObject("LakeRaceCauseways").transform;root.SetParent(_root,false);
            root.gameObject.AddComponent<CityLandscapeResources>();
            float top = LakeAccessHeight(_city) - .01f;
            const float bottom = -.30f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var segment in _lakeAccessSurfaces)
            {
                // Match the rounded paving footprint and let the shared street mesh own the top.
                var outline = RoadSurfaceGeometry.Road(segment.A, segment.B, segment.Width);
                for (int i = 0; i < outline.Length; i++)
                {
                    var a = outline[i];
                    var b = outline[(i + 1) % outline.Length];
                    int start = vertices.Count;
                    vertices.Add(new Vector3(a.x, bottom, a.y));
                    vertices.Add(new Vector3(a.x, top, a.y));
                    vertices.Add(new Vector3(b.x, bottom, b.y));
                    vertices.Add(new Vector3(b.x, top, b.y));
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
                }
            }
            if (vertices.Count > 0) LandscapeMesh(root, "RaceAccessCauseway", vertices, triangles, Mats.Lit(ArtDirection.Hex("858B77")));
            float scale=(float)_city.ExpansionScale;
            float bankTop = top - .005f;
            Prims.Box(root,"StarterMotorHubBank",new Vector3(-550*scale,(bankTop+bottom)*.5f,-135*scale),new Vector3(65*scale,bankTop-bottom,65*scale),Mats.Lit(ArtDirection.Sage));
            StaticBatchingUtility.Combine(root.gameObject);
        }
    }
}
