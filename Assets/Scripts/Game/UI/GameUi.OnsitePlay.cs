using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _onsiteWindow;
        private Text _onsiteStatus,_onsiteReadout;
        private Image _onsitePower;
        private bool _onsiteHeld,_onsiteWasHeld;
        private string _onsiteLayoutKey;
        public bool OnsiteInputActive=>_onsiteWindow!=null&&!_onsiteWindow.Closed;
        public void OpenOnsitePlay()
        {
            if(S.CurrentOnsite==null)return;CloseAllWindows();S.Time.SetPaused(false);_onsiteHeld=_onsiteWasHeld=false;
            _onsiteWindow=OpenWindow("现场活动",1920,1080,BuildOnsitePlay,holds:false,onClosed:()=>{_onsiteHeld=_onsiteWasHeld=false;_onsiteWindow=null;_ctrl.HideOnsiteScene();},scrollContent:false,showTitleBar:false);
        }
        private string OnsiteLayoutKey(OnsitePlayState s)=>s==null?"ended":s.Number+":"+s.Phase+":"+s.Pending+":"+s.Round+":"+s.Feedback+":"+s.Attempts+":"+s.Subject+":"+s.Base+":"+s.Flavor+":"+s.Sweet+":"+string.Join(",",s.TalkMasks)+":"+s.ProcurementDecided+":"+s.Mode+":"+s.Angle+":"+s.Selected;
        private void BuildOnsitePlay(Window w)
        {
            var s=S.CurrentOnsite;if(s==null){Line(w.Content,"本场已结束，关闭后可查看日程结算。",22);return;}
            _onsiteLayoutKey=OnsiteLayoutKey(s);_onsiteStatus=_onsiteReadout=null;_onsitePower=null;
            var panel=w.Root.Find("Panel") as RectTransform;if(w.Fitter!=null)w.Fitter.enabled=false;if(panel!=null){panel.localScale=Vector3.one;panel.Fill();}
            w.Content.GetComponent<VerticalLayoutGroup>().enabled=false;w.Content.Fill();
            _ctrl.RenderOnsiteScene();var view=Ui.Rect(w.Content,"ActualOnsiteScene");view.Fill();var raw=view.gameObject.AddComponent<RawImage>();raw.texture=_ctrl.OnsiteTexture;raw.raycastTarget=false;
            var header=Ui.Panel(w.Content,"OnsiteHeader",new Color(.06f,.1f,.16f,.94f));header.rectTransform.Anchor(0,.91f,1,1);
            Ui.Label(header.transform,S.CityActivityService(S.RunningCityActivity)?.Name,27,color:Ui.Accent).rectTransform.Anchor(.02f,0,.53f,1);
            Ui.Label(header.transform,"¥"+S.Ledger.Balance.Yuan.ToString("N0")+" · 世界时间 "+S.Clock.Now.MinuteOfDay/60+":"+(S.Clock.Now.MinuteOfDay%60).ToString("00"),19).rectTransform.Anchor(.52f,0,.80f,1);
            Ui.Button(header.transform,"返回场景",()=>w.Close(),19).GetComponent<RectTransform>().Anchor(.85f,.18f,.985f,.8f);
            var body=Ui.Panel(w.Content,"OnsiteActions",new Color(.06f,.1f,.16f,.94f));body.rectTransform.Anchor(.70f,.20f,1,.90f);
            var list=Ui.Rect(body.transform,"Actions");list.Anchor(0,0,1,1,16,12,16,12);var layout=list.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=5;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
            void Label(string text)=>Ui.Size(Ui.Label(list,text,18),-1,34);
            void Button(string label,Action action,bool enabled=true)=>Ui.Size(Ui.Button(list,label,()=>{action();w.RequestRebuild();},17,Ui.ButtonBg,enabled),-1,38);
            if(s.Number==51)
            {
                Label("选饮品，实际品尝后评价");for(int i=0;i<3;i++){int n=i;Button((s.Selected==i?"● ":"")+new[]{"莓果气泡","桂花乌龙","姜汁柠檬"}[i],()=>S.SelectOnsiteDrink(n),s.Pending==null&&s.Round<2);}
                Button("慢慢尝一口 · 3分钟",()=>S.SipOnsiteDrink(),s.Pending==null&&s.Round<2);Label("已品尝 "+s.Round+"/2次");
                foreach(int i in new[]{0,1,2}){int n=i;Button(new[]{"建议减甜","喜欢香气","建议减少气泡"}[i],()=>S.OnsiteDrinkFeedback(n),s.Round>=2&&s.Feedback<0);}
            }
            else if(s.Number==52)
            {
                Label(S.RunningCityActivity.PlayerTreat?"本场你请客，完成后额外交情":"请客人与话题分别影响相处反馈");
                var guests=S.RunningCityActivity.Participants;for(int i=0;i<guests.Length;i++)
                {
                    int person=i;Label((s.TalkMasks[i]!=0?"✓ ":"")+S.CityPerson(guests[i].PersonId)?.Name);
                    var row=Ui.Row(list,38,5);for(int j=0;j<3;j++){int topic=j;Ui.Size(Ui.Button(row,new[]{"路线","预算","近况"}[j],()=>{S.TalkOnsiteGuest(person,topic);w.RequestRebuild();},16,Ui.ButtonBg,s.Pending==null&&s.TalkMasks[i]==0),-1,36,1);}
                }
            }
            else if(s.Number==53)
            {
                Label("目标：气泡、柠檬香、低甜度");
                string[][] ingredients={new[]{"乌龙茶","苏打水","咖啡"},new[]{"柠檬","蜂蜜","姜汁"},new[]{"低甜","中甜","高甜"}};
                for(int i=0;i<3;i++){int slot=i;int selected=i==0?s.Base:i==1?s.Flavor:s.Sweet;Label(new[]{"基底","风味","甜度"}[i]);var row=Ui.Row(list,38,4);for(int j=0;j<3;j++){int option=j;Ui.Size(Ui.Button(row,ingredients[i][j],()=>{S.SetOnsiteRecipe(slot,option);w.RequestRebuild();},16,selected==j?Ui.ButtonHot:Ui.ButtonBg,s.Pending==null&&s.Phase!="mixed"&&s.Attempts<3),-1,36,1);}}
                Button("调制这杯 · 5分钟",()=>S.MixOnsiteDrink(),s.Pending==null&&s.Phase!="mixed"&&s.Attempts<3);Button("亲自试喝 · 2分钟",()=>S.TasteOnsiteMix(),s.Pending==null&&s.Phase=="mixed");Label("已试喝"+s.Attempts+"/3杯 · 正确"+s.Success);
            }
            else if(s.Number>=54&&s.Number<=58)
            {
                Label(s.Number<=56?"看场上防守，松手出球":"选杆、方向和力度，观察落点");
                string[] modes=s.Number==55?new[]{"运球节奏","急停转向","跳投"}:s.Number>=57?new[]{"铁杆 · 100米","木杆 · 180米","挖起杆 · 55米"}:s.Number==54?new[]{"定点投篮","上篮","防守脚步"}:new[]{"外线投篮","突破上篮","调整站位"};
                for(int i=0;i<3;i++){int n=i;Button((s.Mode==i?"● ":"")+modes[i],()=>S.SelectOnsiteSportMode(n),s.Phase!="charging"&&s.Phase!="flight");}
                var aim=Ui.Row(list,40,6);Ui.Size(Ui.Button(aim,"← 方向",()=>{S.AimOnsiteSport(s.Angle-3);w.RequestRebuild();},17),-1,38,1);Ui.Size(Ui.Button(aim,"方向 →",()=>{S.AimOnsiteSport(s.Angle+3);w.RequestRebuild();},17),-1,38,1);
                Label("方向 "+s.Angle.ToString("0")+"° · 回合 "+s.Round);
                if(s.Number==55&&s.Round>=3&&s.Mask==7)for(int i=0;i<3;i++){int n=i;Button(new[]{"反馈抓地力","反馈急停脚感","反馈回弹"}[i],()=>S.OnsiteCourtFeedback(n),s.Feedback<0);}
                if(s.Number==58&&s.Round>=3)
                {Button("谈用途、型号和保修",()=>S.DecideOnsiteProcurement(0),!s.ProcurementDecided);Button("谈预算、数量和交期",()=>S.DecideOnsiteProcurement(1),!s.ProcurementDecided);Button("这次不承诺供货",()=>S.DecideOnsiteProcurement(2),!s.ProcurementDecided);}
                Label(s.Number==55?"运球约55%力度，急停约70%":"按住屏幕下方按钮／空格蓄力");
            }
            else if(s.Number==76)
            {
                Label("调整角度和远近，拍下喜欢的构图");for(int i=0;i<3;i++){int n=i;Button((s.Subject==i?"● ":"")+Gifts.Get(GameSession.OnsitePhotoSubjects[i])?.Name,()=>FramePhoto(n,s.PhotoYaw,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure));}
                void Frame(double yaw,double pitch,double distance,double offset,int exposure)=>FramePhoto(s.Subject,yaw,pitch,distance,offset,exposure);
                void Pair(string left,string right,Action a,Action b){var row=Ui.Row(list,38,5);Ui.Size(Ui.Button(row,left,()=>{a();w.RequestRebuild();},17),-1,36,1);Ui.Size(Ui.Button(row,right,()=>{b();w.RequestRebuild();},17),-1,36,1);}
                Pair("向左绕拍","向右绕拍",()=>Frame(s.PhotoYaw-15,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure),()=>Frame(s.PhotoYaw+15,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure));
                Pair("降低机位","抬高机位",()=>Frame(s.PhotoYaw,s.PhotoPitch-10,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure),()=>Frame(s.PhotoYaw,s.PhotoPitch+10,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure));
                Pair("拉近","拉远",()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance-.5,s.PhotoOffset,(int)s.Exposure),()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance+.5,s.PhotoOffset,(int)s.Exposure));
                Pair("构图左移","构图右移",()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset-.3,(int)s.Exposure),()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset+.3,(int)s.Exposure));
                Button("曝光："+new[]{"偏暗","正常","偏亮"}[(int)s.Exposure],()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset,((int)s.Exposure+1)%3));
                Button("拍照并保存",()=>{if(!_ctrl.CaptureOnsitePhoto())Toast("照片未保存，请检查现场、相机和写入空间。");});Button("查看相册",OpenOnsiteAlbum);
            }
            var rail=Ui.Panel(w.Content,"OnsiteBottom",new Color(.06f,.1f,.16f,.97f));rail.rectTransform.Anchor(0,0,1,.19f);
            _onsiteStatus=Ui.Label(rail.transform,s.Message,21,color:Ui.Accent);_onsiteStatus.rectTransform.Anchor(.025f,.48f,.70f,.98f);
            _onsiteReadout=Ui.Label(rail.transform,"",18);_onsiteReadout.rectTransform.Anchor(.025f,.10f,.70f,.48f);
            if(s.Number>=54&&s.Number<=58)
            {
                var charge=Ui.Button(rail.transform,"按住蓄力 · 松手出球",null,23,Ui.ButtonHot);charge.GetComponent<RectTransform>().Anchor(.73f,.2f,.98f,.85f);charge.gameObject.AddComponent<FishingHoldInput>().Held=v=>_onsiteHeld=v;
                var meter=Ui.Panel(rail.transform,"PowerBackground",Ui.ButtonBg);meter.rectTransform.Anchor(.025f,.02f,.68f,.09f);_onsitePower=Ui.Panel(meter.transform,"Power",Ui.Accent);_onsitePower.raycastTarget=false;
            }
            else Ui.Button(rail.transform,"回到场景继续相处",()=>w.Close(),20).GetComponent<RectTransform>().Anchor(.74f,.3f,.98f,.75f);
        }
        private void FramePhoto(int subject,double yaw,double pitch,double distance,double offset,int exposure)=>S.SetOnsitePhotoFrame(subject,yaw,pitch,distance,offset,exposure);
        private void UpdateOnsitePlay(float dt)
        {
            if(!OnsiteInputActive)return;var s=S.CurrentOnsite;
            if(s==null){_onsiteWindow.Close();return;}
            bool held=_onsiteHeld||Input.GetKey(KeyCode.Space);
            if(S.Time.IsRunning)
            {if(held&&!_onsiteWasHeld)S.BeginOnsiteCharge();if(!held&&_onsiteWasHeld)S.ReleaseOnsiteShot();S.StepOnsiteSport(dt);}
            _onsiteWasHeld=held;s=S.CurrentOnsite;
            if(OnsiteLayoutKey(s)!=_onsiteLayoutKey&&s.Phase!="charging")_onsiteWindow.RequestRebuild();
            _ctrl.RenderOnsiteScene();if(_onsiteStatus!=null)_onsiteStatus.text=s.Message;
            if(_onsiteReadout!=null)_onsiteReadout.text=s.Pending!=null?"实际操作中 · 剩"+Math.Max(0,s.ReadyAt-S.Clock.Now.TotalMinutes)+"游戏分钟":s.Number>=57&&s.Number<=58?$"力度{s.Power:P0} · 方向{s.Angle:0}° · 已打{s.Round}杆 · 目标({s.TargetX:0}, {s.TargetZ:0})米 · 风偏{s.Wind:+0.0;-0.0;0}米":s.Number>=54&&s.Number<=56?$"力度{s.Power:P0} · 方向{s.Angle:0}° · 完成{s.Round}回合 · 命中{s.Success}"+(s.Number==56?$" · 比分{s.Success*2}:{s.OpponentScore}":""):s.Number==76?$"已保存{s.PhotoCount}张 · 合格{s.GoodPhotos}张 · 当前构图{S.OnsitePhotoScore}分":"完成现场项目后，继续相处至日程结束。";
            if(_onsitePower!=null)_onsitePower.rectTransform.Anchor(0,0,(float)s.Power,1);
        }
        public void OpenOnsiteAlbum()
        {
            OpenWindow("拍摄相册",850,720,w=>
            {
                foreach(var p in S.OnsitePhotos.Reverse())
                {var photo=p;Line(w.Content,(Gifts.Get(p.SubjectId)?.Name??p.SubjectId)+" · 构图"+p.Score+"分 · "+p.Width+"×"+p.Height,18);Ui.Size(Ui.Button(w.Content,"查看照片",()=>OpenOnsitePhoto(photo),17),-1,38);}
                if(S.OnsitePhotos.Length==0)Line(w.Content,"尚无已保存照片。",18);
            });
        }
        private void OpenOnsitePhoto(OnsitePhoto photo)
        {
            Texture2D texture=null;
            OpenWindow("照片 · "+(Gifts.Get(photo.SubjectId)?.Name??photo.SubjectId),1000,700,w=>
            {
                string path=_ctrl.OnsitePhotoPath(photo.File);if(!File.Exists(path)){Para(w.Content,"此设备上没有原照片，拍摄记录仍保留。",18);return;}
                texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));var rect=Ui.Rect(w.Content,"ActualSavedPhoto");Ui.Size(rect,-1,550);var image=rect.gameObject.AddComponent<RawImage>();image.texture=texture;
            },onClosed:()=>{if(texture!=null)UnityEngine.Object.Destroy(texture);});
        }
    }
}
