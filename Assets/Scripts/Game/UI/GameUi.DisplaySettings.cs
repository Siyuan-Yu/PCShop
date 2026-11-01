using System;
using System.Linq;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _displayPreview;
        private Text _displayCountdown;
        private void BuildDisplayOptions(Window w,GameDisplayConfig draft)
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            Line(w.Content,L("display"),23,Ui.Accent);
            var mode=Ui.Row(w.Content,42);Ui.Size(Ui.Label(mode,L("display_mode"),18),150);
            var modeNames=new[]{L("display_windowed"),L("display_borderless"),L("display_exclusive")};
            var choice=Ui.Choice(mode,modeNames,Array.IndexOf(GameDisplaySettings.Modes,draft.Mode),i=>{draft.Mode=GameDisplaySettings.Modes[i];w.RequestRebuild();});choice.name="DisplayMode";Ui.Size(choice,-1,42,1);
            var sizes=GameDisplaySettings.Resolutions();
            int selected=Array.FindIndex(sizes,r=>r.x==draft.Width&&r.y==draft.Height);if(selected<0)selected=Array.FindIndex(sizes,r=>r.x==1920&&r.y==1080);
            var resolution=Ui.Row(w.Content,42);Ui.Size(Ui.Label(resolution,L("display_resolution"),18),150);
            var size=Ui.Choice(resolution,sizes.Select(r=>r.x+" × "+r.y),Math.Max(0,selected),i=>{draft.Width=sizes[i].x;draft.Height=sizes[i].y;w.RequestRebuild();});size.name="DisplayResolution";Ui.Size(size,-1,42,1);
            Para(w.Content,MenuLocalization.Format("display_current",Screen.width+" × "+Screen.height)+" · "+L("display_hint"),16,Ui.Dim);
            var apply=Ui.Button(w.Content,L("display_apply"),()=>PreviewDisplay(w,draft.Copy()),18,Ui.ButtonHot);apply.name="ApplyDisplay";Ui.Size(apply,-1,42);
#endif
        }
        private void PreviewDisplay(Window parent,GameDisplayConfig requested)
        {
            bool finished=false;Window preview=null;
            if(!GameDisplaySettings.BeginPreview(requested,kept=>
            {finished=true;preview?.Close();_displayPreview=null;_displayCountdown=null;parent?.RequestRebuild();if(kept)Toast(L("display_saved"));}))return;
            preview=OpenWindow(L("display_preview"),760,350,w=>
            {
                Line(w.Content,requested.Width+" × "+requested.Height,24,Ui.Accent);
                _displayCountdown=Ui.Label(w.Content,MenuLocalization.Format("display_countdown",GameDisplaySettings.SecondsLeft),19);Ui.Size(_displayCountdown,-1,65);
                var actions=Ui.Row(w.Content,48);
                var keep=Ui.Button(actions,L("display_keep"),GameDisplaySettings.ConfirmPreview,19,Ui.ButtonHot);keep.name="KeepDisplay";Ui.Size(keep,-1,48,1);
                var undo=Ui.Button(actions,L("display_restore"),GameDisplaySettings.CancelPreview,19);undo.name="RestoreDisplay";Ui.Size(undo,-1,48,1);
            },true,true,()=>{if(!finished)GameDisplaySettings.CancelPreview();});
            _displayPreview=preview;
        }
        private void UpdateDisplayPreview()
        {
            if(_displayPreview!=null&&!_displayPreview.Closed&&_displayCountdown!=null)
                _displayCountdown.text=MenuLocalization.Format("display_countdown",GameDisplaySettings.SecondsLeft);
        }
    }
}
