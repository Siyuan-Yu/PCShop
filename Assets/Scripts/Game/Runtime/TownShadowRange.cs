using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.Game.Runtime
{
    [DisallowMultipleComponent,RequireComponent(typeof(Camera))]
    internal sealed class TownShadowRange : MonoBehaviour
    {
        private Camera view;
        private UniversalRenderPipelineAsset activeAsset;
        private float original;

        private void OnEnable()
        {
            view=GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;
            RenderPipelineManager.endCameraRendering+=AfterCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            RenderPipelineManager.endCameraRendering-=AfterCamera;
            Restore();
        }
        private void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            if(activeAsset!=null || camera!=view || !view.orthographic || view.orthographicSize<30)return;
            var asset=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(asset==null || asset.shadowDistance<=0)return;
            float down=-view.transform.forward.y;
            if(down<.2f)return;
            // Cover the far edge of the visible ground without enlarging street-level shadow cascades.
            float range=(view.transform.position.y+view.orthographicSize*view.transform.up.y)/down+12;
            range=Mathf.Min(view.farClipPlane,range);
            if(range<=asset.shadowDistance)return;
            original=asset.shadowDistance;activeAsset=asset;asset.shadowDistance=range;
        }
        private void AfterCamera(ScriptableRenderContext context,Camera camera)
        {
            if(camera==view)Restore();
        }
        private void Restore()
        {
            if(activeAsset==null)return;
            activeAsset.shadowDistance=original;activeAsset=null;
        }
    }
}
