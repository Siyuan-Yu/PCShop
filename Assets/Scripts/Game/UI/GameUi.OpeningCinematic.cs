using System;
using System.Collections;
using System.IO;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private const string OpeningCinematicPath = "/Opening/OpeningCg.mp4";
        private const float OpeningCinematicTimeout = 8f;
        private Window _openingCinematic;

        internal void ShowOpeningCinematic()
        {
            if (!CanAnimateUi || Application.isBatchMode || !OpeningMealIntroPending)
            {
                ShowIntro();
                return;
            }

            string source = Application.streamingAssetsPath + OpeningCinematicPath;
            if (!source.Contains("://"))
            {
                if (!File.Exists(source))
                {
                    ShowIntro();
                    return;
                }
                source = new Uri(source).AbsoluteUri;
            }

            RawImage picture = null;
            Text loading = null;
            var window = OpenWindow("OpeningCinematic", 0, 0, w =>
            {
                picture = Ui.Rect(w.Content, "CinematicPicture").Fill().gameObject.AddComponent<RawImage>();
                picture.raycastTarget = false;
                picture.enabled = false;
                var aspect = picture.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = 16f / 9f;

                loading = Ui.Label(w.Content, L("opening_cinematic_loading"), 24, TextAnchor.MiddleCenter, Ui.ChromeInk);
                loading.rectTransform.Anchor(.1f, .44f, .9f, .56f);
                var skip = Ui.Button(w.Content, L("opening_cinematic_skip"),
                    () => FinishOpeningCinematic(w, enterTutorial: true), 16);
                skip.name = "OpeningCinematicSkip";
                ((RectTransform)skip.transform).Anchor(1, 0, 1, 0, -278, 20, 24, -62);
            }, closable: false, scrollContent: false, allowDuringOpeningMeal: true,
                showTitleBar: false, fullScreen: true);

            _openingCinematic = window;
            var cover = window.Root.GetComponent<Image>();
            cover.sprite = null;
            cover.color = Color.black;
            var canvas = window.Root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            // Keep the movie above tutorial UI and below the initial world loading cover.
            canvas.sortingOrder = 90;
            window.Root.gameObject.AddComponent<GraphicRaycaster>();

            var player = window.Root.gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = source;
            player.renderMode = VideoRenderMode.APIOnly;
            player.waitForFirstFrame = true;
            player.audioOutputMode = SoundFx.Muted || SoundFx.Volume <= .001f ? VideoAudioOutputMode.None : VideoAudioOutputMode.Direct;
            window.OnClosed = () =>
            {
                if (player != null) player.Stop();
                _openingCinematic = null;
            };
            _ctrl.StartCoroutine(PlayOpeningCinematic(window, player, picture, loading));
        }

        internal bool HandleOpeningCinematicInput()
        {
            if (_openingCinematic is not { Closed: false } window) return false;
            if (Input.GetKeyDown(KeyCode.Escape)) FinishOpeningCinematic(window, enterTutorial: true);
            return true;
        }

        private IEnumerator PlayOpeningCinematic(Window window, VideoPlayer player, RawImage picture, Text loading)
        {
            string error = null;
            bool ended = false;
            player.errorReceived += (_, message) => error = message;
            player.loopPointReached += _ => ended = true;
            player.Prepare();

            float deadline = Time.realtimeSinceStartup + OpeningCinematicTimeout;
            while (!window.Closed && error == null && !player.isPrepared && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (window.Closed) yield break;

            if (error == null && player.isPrepared)
            {
                if (player.audioOutputMode == VideoAudioOutputMode.Direct && player.audioTrackCount > 0)
                    player.SetDirectAudioVolume(0, SoundFx.Volume);
                player.Play();
                double lastTime = -1;
                deadline = Time.realtimeSinceStartup + OpeningCinematicTimeout;
                while (!window.Closed && error == null && !ended && Time.realtimeSinceStartup < deadline)
                {
                    if (player.texture != null && player.frame >= 0 && picture.texture != player.texture)
                    {
                        picture.texture = player.texture;
                        picture.enabled = true;
                        loading.enabled = false;
                    }
                    if (player.time > lastTime)
                    {
                        lastTime = player.time;
                        deadline = Time.realtimeSinceStartup + OpeningCinematicTimeout;
                    }
                    yield return null;
                }
            }
            if (window.Closed) yield break;

            if (!ended) Debug.LogWarning("[开场影片] " + (error ?? "等待视频播放超时。"));
            FinishOpeningCinematic(window, enterTutorial: ended);
        }

        private void FinishOpeningCinematic(Window window, bool enterTutorial)
        {
            if (window.Closed || _openingCinematic != window) return;
            window.Close();
            if (enterTutorial) S.Flags.Add(OpeningMealIntroSeen);
            ShowIntro();
        }
    }
}
