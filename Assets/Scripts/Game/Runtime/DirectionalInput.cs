using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    internal static class DirectionalInput
    {
        public static bool UpHeld => Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        public static bool DownHeld => Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        public static bool LeftHeld => Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        public static bool RightHeld => Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

        public static bool UpPressed => Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        public static bool DownPressed => Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        public static bool LeftPressed => Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        public static bool RightPressed => Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
        public static bool AnyPressed => UpPressed || DownPressed || LeftPressed || RightPressed;

        public static float Horizontal => (RightHeld ? 1f : 0f) - (LeftHeld ? 1f : 0f);
        public static Vector2 Held => new Vector2(Horizontal, (UpHeld ? 1f : 0f) - (DownHeld ? 1f : 0f));
    }
}
