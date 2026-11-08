using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StockPriceGraphic : MaskableGraphic
    {
        private StockQuote[] _quotes=Array.Empty<StockQuote>();
        private long _low,_high;
        public void Initialize(StockQuote[] quotes)
        {
            _quotes=quotes??Array.Empty<StockQuote>();raycastTarget=false;
            if(_quotes.Length==0)return;
            long low=_quotes.Min(q=>q.PriceCents),high=_quotes.Max(q=>q.PriceCents),pad=Math.Max(10,(high-low)/12);
            _low=Math.Max(0,low-pad);_high=high+pad;
            for(int i=0;i<3;i++)
            {
                var label=Ui.Label(transform,((_low+(_high-_low)*i/2d)/100).ToString("0.00"),12,TextAnchor.MiddleRight,Ui.Dim);
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(0,i/2f);
                label.rectTransform.pivot=new Vector2(1,.5f);label.rectTransform.anchoredPosition=new Vector2(68,Mathf.Lerp(28,-12,i/2f));
                label.rectTransform.sizeDelta=new Vector2(65,20);
            }
            for(int i=0;i<2;i++)
            {
                var q=_quotes[i==0?0:_quotes.Length-1];var date=new GameDate((int)(q.Minute/1440));int min=(int)(q.Minute%1440);
                var label=Ui.Label(transform,date.Month+"/"+date.Day+" "+(min/60).ToString("00")+":"+(min%60).ToString("00"),12,TextAnchor.MiddleCenter,Ui.Dim);
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(i,0);label.rectTransform.pivot=new Vector2(.5f,0);
                label.rectTransform.anchoredPosition=new Vector2(i==0?105:-60,0);label.rectTransform.sizeDelta=new Vector2(110,22);
            }
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(_quotes.Length==0)return;
            var rect=rectTransform.rect;float left=rect.xMin+76,right=rect.xMax-12,bottom=rect.yMin+28,top=rect.yMax-12;
            for(int i=0;i<3;i++)AssetTrendGraphic.Stroke(mesh,new Vector2(left,Mathf.Lerp(bottom,top,i/2f)),new Vector2(right,Mathf.Lerp(bottom,top,i/2f)),1,new Color(.6f,.65f,.7f,.2f));
            var previous=Vector2.zero;long first=_quotes[0].Minute,last=_quotes[_quotes.Length-1].Minute;
            for(int i=0;i<_quotes.Length;i++)
            {
                float x=last==first?.5f:(float)((_quotes[i].Minute-first)/(double)(last-first));
                var point=new Vector2(Mathf.Lerp(left,right,x),Mathf.Lerp(bottom,top,(float)((_quotes[i].PriceCents-_low)/(double)Math.Max(1,_high-_low))));
                if(i>0)AssetTrendGraphic.Stroke(mesh,previous,point,2.2f,Ui.Accent);
                AssetTrendGraphic.Stroke(mesh,point-Vector2.right*1.8f,point+Vector2.right*1.8f,3.6f,Ui.Accent);previous=point;
            }
        }
    }
}
