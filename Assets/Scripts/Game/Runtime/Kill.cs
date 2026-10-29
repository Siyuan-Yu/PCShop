using UnityEngine;

namespace PCShopSell.Game
{
    public static class Kill
    {
        /// <summary>运行时用 Destroy，编辑器非运行状态（自动化冒烟测试）用 DestroyImmediate。</summary>
        public static void It(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
