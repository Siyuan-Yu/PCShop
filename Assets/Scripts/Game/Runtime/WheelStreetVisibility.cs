using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    // On a close orthographic view a wheel one block behind the observer can still
    // project over a shop. Cull that landmark for this camera's draw only.
    internal sealed class WheelStreetVisibility : MonoBehaviour
    {
        private Camera _streetCamera;
        private readonly Transform[] _wheels = new Transform[2];
        private readonly int[] _layers = new int[2];
        private int? _savedMask;

        internal void Bind(Camera streetCamera, StreetWorldBuilder world)
        {
            _streetCamera=streetCamera;
            Register(0, CityLifeCatalog.CommunityFerrisVenue, "Community wheel", world);
            Register(1, CityLifeCatalog.GardenAmusementVenue, "Garden wheel", world);
        }

        private void Register(int index, string id, string layerName, StreetWorldBuilder world)
        {
            int layer=LayerMask.NameToLayer(layerName);
            var wheel=world.Places.TryGetValue(id,out var place) ? place.Root.transform.Find("FerrisWheel") : null;
            _wheels[index]=wheel;
            _layers[index]=layer;
            if(wheel==null || layer<0) return;
            foreach(var renderer in wheel.GetComponentsInChildren<MeshRenderer>(true))
                renderer.gameObject.layer=layer;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering+=BeforeDraw;
            RenderPipelineManager.endCameraRendering+=AfterDraw;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeDraw;
            RenderPipelineManager.endCameraRendering-=AfterDraw;
            RestoreMask();
        }

        private void BeforeDraw(ScriptableRenderContext context, Camera cam)
        {
            if(cam!=_streetCamera || !cam.orthographic || cam.orthographicSize>34f) return;
            var sight=cam.ViewportPointToRay(new Vector3(.5f,.5f,0));
            var ground=new Plane(Vector3.up,Vector3.zero);
            if(!ground.Raycast(sight,out float distance)) return;
            var target=sight.GetPoint(distance);
            var forward=Vector3.ProjectOnPlane(sight.direction,Vector3.up).normalized;
            int visible=cam.cullingMask;
            for(int i=0;i<_wheels.Length;i++)
            {
                var wheel=_wheels[i];
                if(wheel==null || _layers[i]<0) continue;
                var foot=wheel.position;
                foot.y=wheel.parent.position.y;
                var screen=cam.WorldToViewportPoint(foot);
                bool footInView=screen.z>0 && screen.x>=-.12f && screen.x<=1.12f && screen.y>=-.12f && screen.y<=1.12f;
                if(!footInView && Vector3.Dot(target-foot,forward)>Mathf.Max(24f,cam.orthographicSize*1.4f))
                    visible &= ~(1<<_layers[i]);
            }
            if(visible==cam.cullingMask) return;
            _savedMask=cam.cullingMask;
            cam.cullingMask=visible;
        }

        private void AfterDraw(ScriptableRenderContext context, Camera cam)
        {
            if(cam==_streetCamera) RestoreMask();
        }

        private void RestoreMask()
        {
            if(_savedMask.HasValue && _streetCamera!=null) _streetCamera.cullingMask=_savedMask.Value;
            _savedMask=null;
        }
    }
}
