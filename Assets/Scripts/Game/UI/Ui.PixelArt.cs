using UnityEngine;

namespace PCShopSell.Game.UI
{
    public static partial class Ui
    {
        private static Material pixelPortrait;
        public static Material PixelPortraitMaterial
        {
            get
            {
                if(pixelPortrait==null)
                {
                    var shader=Resources.Load<Shader>("Shaders/PixelPortrait");
                    if(shader!=null)pixelPortrait=new Material(shader){name="Pixel portrait / keep character identity"};
                }
                return pixelPortrait;
            }
        }
    }
}
