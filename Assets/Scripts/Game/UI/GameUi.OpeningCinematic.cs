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
        private const double OpeningSkipHoldSeconds = 1;
        private Window _openingCinematic;
        private Image _openingSkipFill;
        private double? _openingSkipStartedAt;

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
            var window = OpenWindow("OpeningCinematic", 0, 0, w =>
            {
                picture = Ui.Rect(w.Content, "CinematicPicture").Fill().gameObject.AddComponent<RawImage>();
                picture.raycastTarget = false;
                picture.enabled = false;
                var aspect = picture.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = 16f / 9f;

                _openingSkipFill = BuildOpeningSkipRing(w.Content);
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
                _openingSkipFill = null;
                _openingSkipStartedAt = null;
            };
            _ctrl.StartCoroutine(PlayOpeningCinematic(window, player, picture));
        }

        private static Image BuildOpeningSkipRing(Transform parent)
        {
            var sprite = CircleSprite();
            Image Disc(Transform root, string name, Color color)
            {
                var image = Ui.Panel(root, name, color);
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.raycastTarget = false;
                return image;
            }

            var track = Disc(parent, "OpeningSkipRing", Ui.ChromeEdge);
            track.rectTransform.Anchor(1, 0, 1, 0, -82, 26, 26, -82);
            var fill = Disc(track.transform, "Progress", Ui.ChromeGold);
            fill.rectTransform.Fill();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0;
            var center = Disc(track.transform, "Center", Ui.ChromeWell);
            center.rectTransform.Fill(4);
            Ui.Label(center.transform, "Esc", 16, TextAnchor.MiddleCenter, Ui.ChromeInk).rectTransform.Fill();
            track.gameObject.SetActive(false);
            return fill;
        }

        internal bool HandleOpeningCinematicInput()
        {
            if (_openingCinematic is not { Closed: false } window) return false;
            if (_openingSkipFill.fillAmount >= 1f)
            {
                // The ring was rendered full in the previous frame.
                FinishOpeningCinematic(window, enterTutorial: true);
                return true;
            }

            bool holding = Application.isFocused && Input.GetKey(KeyCode.Escape);
            double now = Time.realtimeSinceStartupAsDouble;
            if (!holding) _openingSkipStartedAt = null;
            else if (!_openingSkipStartedAt.HasValue || Input.GetKeyDown(KeyCode.Escape)) _openingSkipStartedAt = now;
            _openingSkipFill.fillAmount = holding
                ? Mathf.Clamp01((float)((now - _openingSkipStartedAt.Value) / OpeningSkipHoldSeconds)) : 0f;
            _openingSkipFill.transform.parent.gameObject.SetActive(holding);
            return true;
        }

        private IEnumerator PlayOpeningCinematic(Window window, VideoPlayer player, RawImage picture)
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
