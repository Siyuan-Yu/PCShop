using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed class GameDisplayConfig
    {
        public int Width=1920,Height=1080;
        public FullScreenMode Mode=FullScreenMode.Windowed;
        public GameDisplayConfig Copy()=>new GameDisplayConfig{Width=Width,Height=Height,Mode=Mode};
    }
    /// <summary>Display preferences are separate from progress. Unconfirmed changes never persist.</summary>
    public static class GameDisplaySettings
    {
        public const string WidthKey="pcss_display_width",HeightKey="pcss_display_height",ModeKey="pcss_display_mode";
        public static readonly FullScreenMode[] Modes={FullScreenMode.Windowed,FullScreenMode.FullScreenWindow,FullScreenMode.ExclusiveFullScreen};
        private static GameDisplayConfig _restore,_requested;
        private static Action<bool> _finished;
        private static double _deadline;
        private static DisplayChangeGuard _guard;
        public static bool PreviewActive=>_restore!=null;
        public static double PreviewDeadline=>_deadline;
        public static int SecondsLeft=>(int)Math.Max(0,Math.Ceiling(_deadline-Time.realtimeSinceStartupAsDouble));
        public static bool Valid(GameDisplayConfig c)=>c!=null&&c.Width>=640&&c.Height>=360&&c.Width<=7680&&c.Height<=4320&&Modes.Contains(c.Mode);
        public static GameDisplayConfig Saved()
        {
            var c=new GameDisplayConfig{Width=PlayerPrefs.GetInt(WidthKey,1920),Height=PlayerPrefs.GetInt(HeightKey,1080),Mode=(FullScreenMode)PlayerPrefs.GetInt(ModeKey,(int)FullScreenMode.Windowed)};
            return Valid(c)?c:new GameDisplayConfig();
        }
        public static GameDisplayConfig Current()=>new GameDisplayConfig{Width=Math.Max(640,Screen.width),Height=Math.Max(360,Screen.height),Mode=Modes.Contains(Screen.fullScreenMode)?Screen.fullScreenMode:FullScreenMode.Windowed};
        public static Vector2Int[] Resolutions()
        {
            int maxW=Math.Max(1920,Display.main.systemWidth),maxH=Math.Max(1080,Display.main.systemHeight);
            var common=new[]{new Vector2Int(1280,720),new Vector2Int(1366,768),new Vector2Int(1600,900),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3840,2160)};
            return common.Concat(Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)))
                .Where(r=>r.x>=1280&&r.y>=720&&r.x<=maxW&&r.y<=maxH)
                .Concat(new[]{new Vector2Int(Math.Max(640,Screen.width),Math.Max(360,Screen.height))})
                .Distinct().OrderBy(r=>r.x).ThenBy(r=>r.y).ToArray();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            Apply(Saved());
#endif
        }
        private static void Apply(GameDisplayConfig c)
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            Screen.SetResolution(c.Width,c.Height,c.Mode);
#endif
        }
        public static bool BeginPreview(GameDisplayConfig requested,Action<bool> finished)
        {
            if(!Valid(requested))return false;
            CancelPreview();_restore=Current();_requested=requested.Copy();_finished=finished;
            _deadline=Time.realtimeSinceStartupAsDouble+15;
            if(Application.isPlaying&&_guard==null)
            {var go=new GameObject("DisplayChangeGuard");UnityEngine.Object.DontDestroyOnLoad(go);_guard=go.AddComponent<DisplayChangeGuard>();}
            Apply(_requested);return true;
        }
        public static void TickPreview(double now) {if(PreviewActive&&now>=_deadline)CancelPreview();}
        public static void ConfirmPreview()
        {
            if(!PreviewActive)return;
            var requested=_requested;var callback=_finished;_restore=null;_requested=null;_finished=null;
            PlayerPrefs.SetInt(WidthKey,requested.Width);PlayerPrefs.SetInt(HeightKey,requested.Height);PlayerPrefs.SetInt(ModeKey,(int)requested.Mode);PlayerPrefs.Save();callback?.Invoke(true);
        }
        public static void CancelPreview()
        {
            if(!PreviewActive)return;
            var restore=_restore;var callback=_finished;_restore=null;_requested=null;_finished=null;Apply(restore);callback?.Invoke(false);
        }
    }
    public sealed class DisplayChangeGuard:MonoBehaviour
    {
        private void Update()=>GameDisplaySettings.TickPreview(Time.realtimeSinceStartupAsDouble);
        private void OnApplicationQuit()=>GameDisplaySettings.CancelPreview();
    }
}
