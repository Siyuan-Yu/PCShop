using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public static partial class Ui
    {
        private static readonly System.Collections.Generic.Dictionary<string,Sprite> NavigationIcons=new System.Collections.Generic.Dictionary<string,Sprite>();
        private static void NavigationArt(Button button,Text label,string title)
        {
            if(button.transform.parent?.parent?.name!="TopBar"||!new[]{"我的","背包 B","日程","人物","人脉","员工","手机","任务","经营","通知","菜单"}.Contains(title))return;
            if(!NavigationIcons.TryGetValue(title,out var sprite))
            {
                const int n=32;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Navigation / "+title,filterMode=FilterMode.Point};
                var pixels=new Color32[n*n];
                void Pixel(int x,int y){if(x>=0&&x<n&&y>=0&&y<n)pixels[y*n+x]=new Color32(255,255,255,255);}
                void Line(int x,int y,int xx,int yy)
                {int steps=Mathf.Max(Mathf.Abs(xx-x),Mathf.Abs(yy-y));for(int i=0;i<=steps;i++){int px=Mathf.RoundToInt(Mathf.Lerp(x,xx,i/(float)Mathf.Max(1,steps))),py=Mathf.RoundToInt(Mathf.Lerp(y,yy,i/(float)Mathf.Max(1,steps)));Pixel(px,py);Pixel(px+1,py);Pixel(px,py+1);}}
                void Box(int x,int y,int w,int h){Line(x,y,x+w,y);Line(x+w,y,x+w,y+h);Line(x+w,y+h,x,y+h);Line(x,y+h,x,y);}
                void Head(int x,int y,int r){for(int j=-r;j<=r;j++)for(int i=-r;i<=r;i++)if(i*i+j*j<=r*r)Pixel(x+i,y+j);}
                if(title=="我的"||title=="人物"||title=="人脉"||title=="员工")
                {Head(15,23,5);Line(7,5,7,12);Line(7,12,12,16);Line(12,16,19,16);Line(19,16,24,12);Line(24,12,24,5);Line(7,5,24,5);if(title!="我的"){Head(26,22,3);Line(27,15,30,11);}}
                else if(title=="经营")
                {Box(4,5,24,13);Line(4,18,12,23);Line(12,23,12,18);Line(13,18,21,23);Line(21,23,21,18);Box(23,18,4,10);Box(8,8,4,5);Box(18,8,4,5);}
                else if(title=="手机"){Box(9,3,14,26);Line(12,25,20,25);Line(13,7,19,7);}
                else if(title=="菜单"){for(int y=8;y<28;y+=8)Line(5,y,26,y);}
                else if(title=="背包 B"){Box(7,4,18,19);Box(12,23,8,5);Box(10,7,12,6);}
                else if(title=="通知"){Line(6,9,9,23);Line(9,23,16,27);Line(16,27,23,23);Line(23,23,26,9);Line(6,9,26,9);Head(16,5,3);}
                else {Box(6,4,20,23);Line(10,29,10,24);Line(22,29,22,24);Line(10,18,22,18);Line(10,12,22,12);}
                tex.SetPixels32(pixels);tex.Apply();sprite=Sprite.Create(tex,new Rect(0,0,n,n),Vector2.one*.5f);NavigationIcons[title]=sprite;
            }
            var icon=Rect(button.transform,"NavigationIcon").gameObject.AddComponent<Image>();icon.sprite=sprite;icon.color=TextColor;icon.raycastTarget=false;
            icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(.5f,1);icon.rectTransform.pivot=new Vector2(.5f,1);icon.rectTransform.anchoredPosition=new Vector2(0,-3);icon.rectTransform.sizeDelta=new Vector2(20,20);
            label.fontSize=13;label.rectTransform.Anchor(0,0,1,.48f,2,1,2,0);
        }
    }
}
