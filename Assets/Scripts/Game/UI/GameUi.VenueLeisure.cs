using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _karaokeWindow;
        public bool KaraokeInputActive => _karaokeWindow != null && !_karaokeWindow.Closed;

        private static bool BuildLeisureBackdrop(Transform parent, string resource, bool fill = true)
        {
            var texture = Resources.Load<Texture2D>("EventCg/" + resource);
            if (texture == null) return false;
            var backdrop = Ui.Rect(parent, "VenueBackdrop").Fill();
            if (fill)
            {
                backdrop.gameObject.AddComponent<RectMask2D>();
                EventCgImage(backdrop, "Room", texture, true);
            }
            else
            {
                var image = backdrop.gameObject.AddComponent<RawImage>();
                image.texture = texture;
                image.raycastTarget = false;
            }
            return true;
        }

        private RectTransform BuildLeisureHeader(Window w, string title, string action, Action close)
        {
            var bar = Ui.Panel(w.Content, "VenueHeader", Ui.Chrome).rectTransform;
            bar.Anchor(0, .924f, 1, 1);
            Ui.Label(bar, title, 29, TextAnchor.MiddleCenter, Ui.Accent).rectTransform.Anchor(.24f, 0, .76f, 1);
            Ui.Label(bar, $"{S.Clock.MinuteOfDay / 60:00}:{S.Clock.MinuteOfDay % 60:00}", 24)
                .rectTransform.Anchor(.02f, 0, .15f, 1);
            Ui.Button(bar, action, close, 18).GetComponent<RectTransform>().Anchor(.85f, .15f, .98f, .85f);
            return bar;
        }

        private void BuildLeisureReply(Transform parent, string personId, string words)
        {
            var person = S.CityPerson(personId);
            var line = Ui.Row(parent, 67, 12);
            if (person != null)
            {
                var portrait = Ui.Size(Ui.Rect(line, "Speaker"), 54, 64);
                FitPortrait(portrait, personId).raycastTarget = false;
            }
            Ui.Size(ProductText(line, person == null ? words : $"<b>{person.Name}</b>\n{words}", 17), 0, 66, 1);
        }

        private void BuildBarCounterEntry(Transform parent, CityActivityDefinition definition, CityActivity booking,
            bool here, bool alreadyRunning)
        {
            if (alreadyRunning) return;
            var row = Ui.Row(parent, 84, 12);
            string prices = string.Join("　·　", Enumerable.Range(0, definition.ServiceCount).Select(i =>
                $"{GameSession.VenueServiceName(definition, i)} {S.CityActivityEntryPrice(definition, i, booking)}"));
            Ui.Size(ProductText(row, $"<b>吧台饮品</b>\n<size=16>{prices}</size>", 20), 0, 82, 1);
            Ui.Size(Ui.Button(row, here ? "看饮品" : "到店点单", () => OpenBarCounter(definition, 0, booking),
                18, Ui.ButtonHot, here), 150, 42);
        }

        private void OpenBarCounter(CityActivityDefinition definition, int choice, CityActivity booking, bool ordered = false)
        {
            if (!GameSession.IsBarDrinkService(definition) || S.CurrentPlace?.Id != definition.VenueId) return;
            bool lockedDrink = ordered || booking?.ServiceConfirmed == true;
            bool honey = booking?.BarHoneyAdded ?? false;
            string venueName = S.CurrentPlace.Name;
            var options = Enumerable.Range(0, definition.ServiceCount)
                .Select(i => GameSession.VenueServiceName(definition, i)).ToArray();
            choice = Mathf.Clamp(choice, 0, options.Length - 1);
            if (!lockedDrink && S.VenueServiceBlocker(definition.Id, choice, booking?.Id) != null)
                choice = Enumerable.Range(0, options.Length).FirstOrDefault(
                    i => S.VenueServiceBlocker(definition.Id, i, booking?.Id) == null);

            OpenWindow(venueName, 1920, 1080, w =>
            {
                BuildLeisureBackdrop(w.Content, "event_lighthouse_bar");
                BuildLeisureHeader(w, venueName, ordered ? "继续小聚" : "返回价目表", () =>
                {
                    if (ordered) CloseAllWindows();
                    else w.Close();
                });

                bool tea = choice != 0;
                string taste = tea ? (honey ? "温热 · 带一点蜜香" : "温热 · 乌龙茶香")
                    : honey ? "清爽 · 酸味柔和" : "清爽 · 带柠檬酸味";
                var drink = Ui.Rect(w.Content, "PreparedDrink").Anchor(.31f, .35f, .48f, .64f)
                    .gameObject.AddComponent<VenueDrinkGraphic>();
                drink.Tea = tea;
                drink.Honey = honey;
                drink.raycastTarget = false;
                var tag = Ui.Panel(w.Content, "DrinkName", Ui.Chrome).rectTransform.Anchor(.295f, .29f, .50f, .35f);
                Ui.Label(tag, options[choice], 24, TextAnchor.MiddleCenter, Ui.Accent).rectTransform.Fill(4);

                var sheet = Ui.Panel(w.Content, "BarOrder", Ui.PanelBg).rectTransform.Anchor(.60f, .08f, .97f, ordered ? .43f : .59f);
                var box = Ui.Rect(sheet, "BarDetails").Fill(22);
                Ui.VList(box, 8, 0);
                Line(box, ordered ? "你的饮品" : "想喝点什么？", 28, Ui.TextColor);
                var drinks = Ui.Row(box, 44, 8);
                for (int i = 0; i < options.Length; i++)
                {
                    int drinkIndex = i;
                    Ui.Size(Ui.Button(drinks, options[i], () => { choice = drinkIndex; w.RequestRebuild(); }, 17,
                        Ui.SelectedButton(choice == i), !lockedDrink || choice == i), 0, 44, 1);
                }

                if (!ordered)
                {
                    Line(box, "口味", 17, Ui.Accent);
                    var flavors = Ui.Row(box, 42, 8);
                    Ui.Size(Ui.Button(flavors, tea ? "原味" : "保留柠檬酸味", () =>
                    { honey = false; w.RequestRebuild(); }, 17, Ui.SelectedButton(!honey)), 0, 42, 1);
                    Ui.Size(Ui.Button(flavors, "加一点蜂蜜", () =>
                    { honey = true; w.RequestRebuild(); }, 17, Ui.SelectedButton(honey)), 0, 42, 1);
                }

                var order = Ui.Row(box, 50, 10);
                Ui.Size(Ui.Label(order, $"{taste}\n无酒精", 17), 0, 46, 1);
                if (!ordered)
                {
                    string blocker = lockedDrink ? S.VenueAppointmentStartBlocker(booking)
                        : S.VenueServiceBlocker(definition.Id, choice, booking?.Id);
                    string price = booking?.EntryPaid == true ? "已支付"
                        : S.CityActivityEntryPrice(definition, choice, booking).ToString();
                    Ui.Size(Ui.Button(order, $"点这杯 · {price}", () =>
                    {
                        bool started = lockedDrink ? S.StartVenueAppointment(booking.Id, honey)
                            : S.SelectVenueService(definition.Id, choice, booking?.Id, honey);
                        if (started && S.RunningCityActivity is CityActivity running && running.VenueId == definition.VenueId)
                        {
                            booking = running;
                            lockedDrink = ordered = true;
                        }
                        else if (started) { w.Close(); return; }
                        w.RequestRebuild();
                    }, 18, Ui.ButtonHot, blocker == null), 186, 44);
                    if (blocker != null) Para(box, blocker, 15, Ui.Warn);
                }
                var partner = ordered ? booking?.Participants.FirstOrDefault(p => p.Phase == CityParticipantPhase.Participating) : null;
                string speaker = partner?.PersonId;
                if (speaker == null && definition.VenueId == "life_bar" && S.PeopleInPlace(definition.VenueId).Any(p => p.PersonId == "life_bartender"))
                    speaker = "life_bartender";
                string reply = ordered ? (tea ? (honey ? "蜂蜜香淡淡的，茶味还在。" : "这杯茶够热，慢慢喝正好。")
                    : honey ? "蜂蜜把柠檬的酸味压下去了一点。" : "第一口先是气泡，后面才尝到柠檬。")
                    : honey ? "一点蜂蜜就好，原来的味道还留着。" : tea ? "这杯热乌龙，先喝原味也不错。" : "柠檬味会明显一点，喝起来清爽。";
                BuildLeisureReply(box, speaker, reply);
            }, scrollContent: false, showTitleBar: false, fullScreen: true);
        }

        private void OpenKaraokeRoom()
        {
            var activity = S.RunningCityActivity;
            var place = S.CurrentPlace;
            if (KaraokeInputActive || activity == null || place?.Business != "venue_ktv" || place.Id != activity.VenueId) return;

            var partner = activity.Participants.FirstOrDefault(p => p.Phase == CityParticipantPhase.Participating);
            var service = S.CityActivityService(activity);
            string serviceName = GameSession.VenueServiceName(service, Math.Max(0, activity.ServiceChoice));
            bool duet = partner != null && !serviceName.Contains("独唱") && !serviceName.Contains("录音指导");
            var song = new KaraokeChorus(duet);
            _karaokeWindow = OpenWindow(place.Name, 1920, 1080, w =>
            {
                BuildLeisureBackdrop(w.Content, "event_karaoke");
                BuildLeisureHeader(w, place.Name + " · 包间", "回到店里", w.Close);
                var sheet = Ui.Panel(w.Content, "KaraokeControls", Ui.PanelBg).rectTransform.Anchor(.535f, .07f, .97f, partner == null ? .47f : .54f);
                var box = Ui.Rect(sheet, "Song").Fill(23);
                Ui.VList(box, 8, 0);
                var heading = Ui.Row(box, 40, 8);
                Ui.Size(Ui.Label(heading, "《晚风慢一点》", 28), 0, 38, 1);
                var progress = Ui.Size(Ui.Label(heading, "", 17, TextAnchor.MiddleRight, Ui.Dim), 145, 38);
                var lyric = Ui.Size(Ui.Label(box, "", 31, color: Ui.Accent), -1, 46);
                var next = Ui.Size(Ui.Label(box, "", 18, color: Ui.Dim), -1, 33);
                var lane = Ui.Size(Ui.Panel(box, "BeatLane", ArtDirection.Hex("382F44")), -1, 63).rectTransform;
                lane.gameObject.AddComponent<RectMask2D>();
                var line = Ui.Panel(lane, "BeatLine", ArtDirection.Hex("655875"));
                line.rectTransform.Anchor(0, .5f, 1, .5f, 0, -1, 0, -1);
                line.raycastTarget = false;
                var cueLine = Ui.Panel(lane, "HitLine", ArtDirection.Hex("EDD29A"));
                cueLine.rectTransform.Anchor(KaraokeChorus.HitLine, .09f, KaraokeChorus.HitLine, .91f, -1, 0, -1, 0);
                cueLine.raycastTarget = false;
                var notes = song.Cues.Select(_ => Ui.Panel(lane, "Note", ArtDirection.Hex("CDADDE"))).ToArray();
                foreach (var note in notes) note.raycastTarget = false;

                var controls = Ui.Row(box, 49, 10);
                var first = Ui.Size(Ui.Button(controls, "", () =>
                {
                    if (!song.Playing) song.Start(w.Root, singing: true);
                    else song.Tap();
                }, 20, Ui.ButtonHot), 0, 47, 1);
                var second = Ui.Size(Ui.Button(controls, "", () =>
                {
                    if (!song.Playing) song.Start(w.Root, singing: false);
                    else if (song.Singing) song.Listen();
                    else song.Stop();
                }, 18), 0, 47, 1);
                var primary = first.GetComponentInChildren<Text>();
                var secondary = second.GetComponentInChildren<Text>();
                var feedback = Ui.Size(Ui.Label(box, "", 18, color: Ui.Good), -1, 32);
                if (partner != null)
                    BuildLeisureReply(box, partner.PersonId, duet ? "我们轮流来，你接的时候我就听着。" : "你先唱，我在这儿听。");

                w.RefreshContent = () =>
                {
                    if (S.RunningCityActivity?.Id != activity.Id || S.CurrentPlace?.Id != place.Id) { w.Close(); return; }
                    bool active = _windows.LastOrDefault() == w && Application.isFocused;
                    song.Advance(Time.unscaledDeltaTime, active);
                    if (active && Input.GetKeyDown(KeyCode.Space) && song.Playing) song.Tap();
                    progress.text = $"{song.Position:0.0} / {KaraokeChorus.Duration:0.0} 秒";
                    lyric.text = song.Lyric;
                    next.text = song.NextLine;
                    primary.text = song.Playing ? song.Singing ? "空格 / 点击接唱" : "听歌中…"
                        : song.Ended ? "再唱一遍" : "开始接唱";
                    first.interactable = !song.Playing || song.Singing;
                    secondary.text = song.Playing ? song.Singing ? "切换为听歌" : "停止播放" : "听伴奏";
                    feedback.text = song.Feedback;
                    for (int i = 0; i < notes.Length; i++)
                    {
                        float x = KaraokeChorus.HitLine + (song.Cues[i] - song.Position) / 2.3f;
                        bool activeNote = song.Playing && song.Singing && !song.Used[i] && x > 0 && x < 1;
                        notes[i].enabled = activeNote;
                        if (!activeNote) continue;
                        bool onBeat = Mathf.Abs(song.Cues[i] - song.Position) <= KaraokeChorus.Tolerance;
                        notes[i].color = onBeat ? ArtDirection.Hex("FFE09A") : ArtDirection.Hex("CDADDE");
                        float size = onBeat ? 10 : 7;
                        notes[i].rectTransform.Anchor(x, .5f, x, .5f, -size, -size, -size, -size);
                    }
                };
                w.RefreshContent();
            }, onClosed: () => { song.Dispose(); _karaokeWindow = null; }, scrollContent: false,
                showTitleBar: false, fullScreen: true);
        }

        private sealed class KaraokeChorus
        {
            public const float HitLine = .26f;
            public const float Tolerance = .19f;
            private const float Beat = .625f;
            private const int Rate = 22050;
            public const float Duration = 44 * Beat;
            private static readonly string[] Lyrics =
            {
                "晚风慢一点，等灯亮起来", "我们就沿着湖边走", "窗里的歌，唱到这一句", "别急着把灯关掉",
                "走过旧街口，雨就停了", "风吹过空空的站台", "等下一趟晚班车开走", "我们再唱一遍"
            };
            private static readonly int[] Melody =
            {
                60, 64, 67, 64,  67, 69, 67, 64, 62,  64, 67, 72, 69, 67,
                69, 72, 69, 67, 64,  65, 67, 65, 64, 62,  67, 69, 67, 64, 62,
                64, 67, 69, 67, 64,  69, 67, 64, 62, 60,  65, 64, 62, 60, 60
            };
            private static readonly int[] Roots = { 48, 43, 45, 41, 48, 43, 45, 41 };
            private readonly bool _duet;
            private AudioSource _source;
            private AudioClip _clip;
            private bool _suspended, _participated;
            private int _hits;
            private float _hitUntil;
            public readonly float[] Cues;
            public readonly bool[] Used;
            public bool Playing { get; private set; }
            public bool Singing { get; private set; }
            public bool Ended { get; private set; }
            public float Position { get; private set; }
            private int Phrase => Mathf.Clamp(Mathf.FloorToInt((Position / Beat - 4) / 5), 0, Lyrics.Length - 1);
            public string Lyric => !Playing && !Ended ? "晚风慢一点，等灯亮起来" : Position < 4 * Beat ? "前奏" : Lyrics[Phrase];
            public string NextLine => !Playing ? "跟着伴奏，在拍点经过亮线时接唱。"
                : Position < 4 * Beat ? Lyrics[0]
                : Phrase < Lyrics.Length - 1 ? "下一句　" + Lyrics[Phrase + 1] : "";
            public string Feedback => !Playing
                ? Ended && _participated ? $"接上 {_hits} / {Cues.Length} 句" : ""
                : !Singing ? "听伴奏" : Position < _hitUntil ? "接上了"
                : _duet && Position >= 4 * Beat && Phrase % 2 == 1 ? "同行者接唱" : "跟着亮线接唱";

            public KaraokeChorus(bool duet)
            {
                _duet = duet;
                Cues = Enumerable.Range(0, Lyrics.Length).Where(i => !duet || i % 2 == 0)
                    .Select(i => (4 + 5 * i) * Beat).ToArray();
                Used = new bool[Cues.Length];
            }

            public void Start(RectTransform parent, bool singing)
            {
                Position = _hitUntil = 0;
                _hits = 0;
                Array.Clear(Used, 0, Used.Length);
                Playing = true;
                Singing = _participated = singing;
                Ended = _suspended = false;
                var events = EventSystem.current;
                if (events != null && !events.alreadySelecting) events.SetSelectedGameObject(null);
                if (!Application.isPlaying || Application.isBatchMode) return;
                if (_source == null)
                {
                    _clip = CreateTrack();
                    _source = parent.gameObject.AddComponent<AudioSource>();
                    _source.playOnAwake = false;
                    _source.spatialBlend = 0;
                    _source.clip = _clip;
                }
                _source.volume = SoundFx.Muted ? 0 : SoundFx.Volume * .63f;
                _source.Play();
            }

            public void Advance(float deltaTime, bool active)
            {
                if (!Playing) return;
                if (!active)
                {
                    if (!_suspended) _source?.Pause();
                    _suspended = true;
                    return;
                }
                if (_source != null)
                {
                    _source.volume = SoundFx.Muted ? 0 : SoundFx.Volume * .63f;
                    if (_suspended) _source.UnPause();
                    else if (_source.isPlaying) Position = Mathf.Max(Position, (float)_source.timeSamples / Rate);
                    else Position += deltaTime;
                }
                else Position += deltaTime;
                _suspended = false;
                if (Position < Duration) return;
                Position = Duration;
                Ended = true;
                Stop();
            }

            public void Tap()
            {
                if (!Playing || !Singing) return;
                for (int i = 0; i < Cues.Length; i++)
                {
                    if (Used[i] || Mathf.Abs(Cues[i] - Position) > Tolerance) continue;
                    Used[i] = true;
                    _hits++;
                    _hitUntil = Position + .55f;
                    return;
                }
            }

            public void Listen()
            {
                Singing = _participated = false;
            }

            public void Stop()
            {
                Playing = Singing = false;
                if (_source != null) _source.Stop();
            }

            public void Dispose()
            {
                Stop();
                if (_clip != null) UnityEngine.Object.Destroy(_clip);
            }

            private static AudioClip CreateTrack()
            {
                var samples = new float[(int)(Duration * Rate)];
                for (int beatIndex = 0; beatIndex < Melody.Length; beatIndex++)
                {
                    double melodyHz = Frequency(Melody[beatIndex]);
                    int root = Roots[Mathf.Clamp((beatIndex - 4) / 5, 0, Roots.Length - 1)];
                    double rootHz = Frequency(root);
                    double chordHz = Frequency(root + 7);
                    int start = Mathf.RoundToInt(beatIndex * Beat * Rate);
                    int end = Mathf.Min(samples.Length, Mathf.RoundToInt((beatIndex + 1) * Beat * Rate));
                    for (int i = start; i < end; i++)
                    {
                        double t = (double)(i - start) / Rate;
                        double envelope = Math.Min(1, t * 100) * Math.Min(1, (Beat - t) * 55);
                        double melody = (Math.Sin(melodyHz * t) + .22 * Math.Sin(2 * melodyHz * t)) * Math.Exp(-t * 4);
                        double chord = (Math.Sin(rootHz * t) + .45 * Math.Sin(chordHz * t)) * Math.Exp(-t * 2.3);
                        samples[i] = (float)(envelope * (.30 * melody + .16 * chord));
                    }
                }
                var clip = AudioClip.Create("晚风慢一点", samples.Length, 1, Rate, false);
                clip.SetData(samples, 0);
                return clip;
            }

            private static double Frequency(int midi) => 2 * Math.PI * 440 * Math.Pow(2, (midi - 69) / 12.0);
        }
    }

    public sealed class VenueDrinkGraphic : MaskableGraphic
    {
        public bool Tea, Honey;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            void Polygon(Color color, params Vector2[] points)
            {
                int start = mesh.currentVertCount;
                foreach (var point in points)
                    mesh.AddVert(rectTransform.rect.min + Vector2.Scale(point, rectTransform.rect.size), color, Vector2.zero);
                for (int i = 2; i < points.Length; i++) mesh.AddTriangle(start, start + i - 1, start + i);
            }
            void Ellipse(float x, float y, float rx, float ry, Color color)
            {
                var points = new Vector2[28];
                for (int i = 0; i < points.Length; i++)
                {
                    float angle = i * 2 * Mathf.PI / points.Length;
                    points[i] = new Vector2(x + rx * Mathf.Cos(angle), y + ry * Mathf.Sin(angle));
                }
                Polygon(color, points);
            }
            Ellipse(.51f, .09f, .46f, .075f, new Color(.11f, .08f, .07f, .53f));
            Color body = Tea ? ArtDirection.Hex("B97C50") : new Color(.91f, .9f, .75f, .6f);
            Color liquid = Tea ? ArtDirection.Hex(Honey ? "BE742F" : "92522D") : ArtDirection.Hex(Honey ? "DDA64C" : "ECC769");
            float bottom = .15f;
            float rim = Tea ? .68f : .87f;
            if (Tea)
            {
                Ellipse(.79f, .43f, .16f, .13f, ArtDirection.Hex("9C6347"));
                Ellipse(.8f, .44f, .092f, .072f, ArtDirection.Hex("3B322A"));
            }
            Polygon(body, new(.19f, rim), new(.79f, rim), new(.70f, bottom), new(.27f, bottom));
            Ellipse(.485f, bottom, .216f, .059f, body);
            if (!Tea)
            {
                Polygon(liquid, new(.222f, .73f), new(.757f, .73f), new(.667f, bottom + .02f), new(.303f, bottom + .02f));
                Ellipse(.485f, .73f, .268f, .055f, ArtDirection.Hex(Honey ? "EDBF66" : "FFE59A"));
                if (Honey) Ellipse(.485f, bottom + .026f, .18f, .025f, ArtDirection.Hex("BA782E"));
                for (int i = 0; i < 8; i++)
                {
                    float x = .33f + (i * .111f) % .32f;
                    float y = .25f + (i * .141f) % .43f;
                    Ellipse(x, y, .009f + i % 2 * .005f, .012f, new Color(1f, .94f, .75f, .73f));
                }
                Ellipse(.78f, .80f, .155f, .13f, ArtDirection.Hex("F3BE51"));
                Ellipse(.78f, .80f, .125f, .103f, ArtDirection.Hex("FFF2B5"));
                Ellipse(.78f, .80f, .108f, .088f, ArtDirection.Hex("F7D078"));
            }
            Ellipse(.49f, rim, .30f, .055f, Tea ? ArtDirection.Hex("D49E76") : new Color(1, .97f, .87f, .65f));
            Ellipse(.49f, rim, .258f, .039f, Tea ? liquid : new Color(.89f, .71f, .43f, .52f));
            Polygon(new Color(1, .96f, .88f, .31f), new(.23f, rim - .04f), new(.26f, rim - .04f),
                new(.337f, bottom + .04f), new(.315f, bottom + .04f));
        }
    }
}
