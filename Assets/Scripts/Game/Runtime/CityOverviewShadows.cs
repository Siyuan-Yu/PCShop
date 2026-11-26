using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.Game.Runtime
{
    // A street camera can also survey the district. Extend cascades only for that
    // draw, according to the farthest visible ground, and restore the user's quality setting.
    [RequireComponent(typeof(Camera))]
    internal sealed class CityOverviewShadows : MonoBehaviour
    {
        internal StreetWorldBuilder World { private get; set; }
        private Camera _camera;
        private Vector3 _originalPosition;
        private float _originalFar;
        private bool _shifted;
        private UniversalRenderPipelineAsset _active;
        private float _savedDistance;

        private void OnEnable()
        {
            _camera=GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering+=Begin;
            RenderPipelineManager.endCameraRendering+=End;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=Begin;
            RenderPipelineManager.endCameraRendering-=End;
            Restore();
        }

        private void Begin(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=_camera || !camera.orthographic || camera.orthographicSize<75f ||
                !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline))return;
            // Orthographic retreat keeps the exact same projected framing. At a city zoom the
            // lower near-plane corners can start inside the forested hillside and see sky through it.
            if(World!=null && camera.transform.forward.y<-.15f)
            {
                float retreat=0;
                for(int step=0;step<4;step++)
                {
                    float deficit=0;
                    for(int x=0;x<=8;x++)
                    {
                        var corner=camera.ViewportPointToRay(new Vector3(x/8f,0,0)).origin-camera.transform.forward*retreat;
                        deficit=Mathf.Max(deficit,World.SurveyCanopyHeight(corner)-corner.y);
                    }
                    if(deficit<.02f)break;
                    retreat+=(deficit+2f)/-camera.transform.forward.y;
                }
                if(retreat>0)
                {
                    _originalPosition=camera.transform.position;
                    _originalFar=camera.farClipPlane;
                    camera.transform.position-=camera.transform.forward*retreat;
                    camera.farClipPlane+=retreat;
                    _shifted=true;
                }
            }
            var ray=camera.ViewportPointToRay(new Vector3(.5f,1,0));
            var plane=new Plane(Vector3.up,Vector3.zero);
            if(!plane.Raycast(ray,out var farEdge))return;
            float needed=Mathf.Min(camera.farClipPlane,farEdge+camera.nearClipPlane+35f);
            if(needed<=pipeline.shadowDistance)return;
            _active=pipeline;
            _savedDistance=pipeline.shadowDistance;
            pipeline.shadowDistance=needed;
        }

        private void End(ScriptableRenderContext context,Camera camera)
        {
            if(camera==_camera)Restore();
        }

        private void Restore()
        {
            if(_shifted)
            {
                _camera.transform.position=_originalPosition;
                _camera.farClipPlane=_originalFar;
                _shifted=false;
            }
            if(_active==null)return;
            _active.shadowDistance=_savedDistance;
            _active=null;
        }
    }
}
