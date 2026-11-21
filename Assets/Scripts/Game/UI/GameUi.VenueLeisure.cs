using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _karaokeWindow;
        private Window _karaokePicker;
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
                .rectTransform.Anchor(.02f, 0, .15f, 1).name = "VenueClock";
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

        private Window _barCounterWindow;

        private void BuildBarVisitors(Window w, Place place, string bartenderId, CityActivity booking)
        {
            var participants = booking?.Participants.Select(person => person.PersonId).ToArray() ?? Array.Empty<string>();
            var visitors = S.PeopleInPlace(place.Id).Where(person => person.PersonId != bartenderId && S.CityPerson(person.PersonId) != null)
                .OrderByDescending(person => participants.Contains(person.PersonId))
                .ThenBy(person => person.Task == CityPersonTask.Working).ToArray();
            if (visitors.Length == 0) return;

            const int columns = 3, rowHeight = 46, rowGap = 5;
            int visibleRows = Mathf.Min(2, (visitors.Length + columns - 1) / columns);
            float rosterHeight = visibleRows * rowHeight + (visibleRows - 1) * rowGap;
            var panel = Ui.FlatPanel(w.Content, "BarVisitors", Ui.PanelBg).rectTransform
                .Anchor(.58f, .608f, .97f, .608f, top: -(rosterHeight + 55));
            Ui.VList(panel, rowGap, 12);
            Line(panel, "在场人物", 18, Ui.Accent);
            var list = visitors.Length > columns * visibleRows ? Ui.Scroll(panel, rosterHeight) : panel;
            for (int start = 0; start < visitors.Length; start += columns)
            {
                var row = Ui.Row(list, rowHeight, 8);
                for (int column = 0; column < columns; column++)
                {
                    var cell = Ui.Size(Ui.Rect(row, "BarVisitor"), 0, rowHeight, 1);
                    if (start + column >= visitors.Length) continue;
                    var visitor = visitors[start + column];
                    string role = participants.Contains(visitor.PersonId) ? "本次同行"
                        : visitor.Task == CityPersonTask.Working ? "值班" : "在场";
                    Ui.VList(cell, 0, 0);
                    PersonSummary(cell, visitor.PersonId, S.CityPerson(visitor.PersonId).Name, role, rowHeight,
                        () => OpenCityPerson(visitor.PersonId));
                }
            }
        }

        private void OpenBarCounter(Place place, string activityId = null)
        {
            if (S.CurrentPlace != place || _barCounterWindow is { Closed: false }) return;
            var session = S;
            session.PrepareVenueCompanions(place, activityId);
            var running = session.RunningCityActivity;
            var booking = running?.VenueId == place.Id && GameSession.IsBarVisit(running)
                ? running : session.PendingVenueActivity(place, activityId);
            bool drinking = booking?.State == CityActivityPhase.Running;
            bool confirmed = booking?.ServiceConfirmed == true;
            var definitions = confirmed ? new[] { session.CityActivityService(booking) }
                : CityLifeCatalog.Activities.Where(def => !def.EventOnly && def.VenueId == place.Id).ToArray();
            var drinks = definitions.Where(def => def != null)
                .SelectMany(def => Enumerable.Range(0, def.ServiceCount)
                    .Where(option => !confirmed || option == Mathf.Clamp(booking.ServiceChoice, 0, def.ServiceCount - 1))
                    .Select(option => (Definition: def, Choice: option)))
                .OrderByDescending(item => GameSession.IsBarDrinkService(item.Definition)).ToArray();
            if (drinks.Length == 0) return;
            int drinkCount = drinks.Count(item => GameSession.IsBarDrinkService(item.Definition));
            int selected = 0;
            bool honey = booking?.BarHoneyAdded ?? false;
            float playback = 0;
            bool finishDrink = false;

            _barCounterWindow = OpenWindow(place.Name, 1920, 1080, w =>
            {
                BuildLeisureBackdrop(w.Content, "event_lighthouse_bar");
                var header = BuildLeisureHeader(w, place.Name, "返回街上", () =>
                {
                    if (drinking) return;
                    w.Close();
                    _ctrl.LeaveCurrentPlace();
                });
                header.GetComponentInChildren<Button>().interactable = !drinking;
                w.Closable = !drinking;

                var (definition, choice) = drinks[selected];
                var service = definition.Service(choice);
                bool basic = service.BarDrink != BarDrinkStyle.None;
                string name = GameSession.VenueServiceName(definition, choice);
                VenueDrinkGraphic cup = null;
                if (basic)
                {
                    cup = Ui.Rect(w.Content, "PreparedDrink").Anchor(.31f, .35f, .48f, .64f)
                        .gameObject.AddComponent<VenueDrinkGraphic>();
                    cup.Drink = service.BarDrink;
                    cup.Honey = honey;
                    cup.raycastTarget = false;
                }

                var box = Ui.FlatPanel(w.Content, "BarCounter", Ui.PanelBg).rectTransform
                    .Anchor(.58f, .08f, .97f, .59f);
                var details = Ui.Rect(box, "Order").Fill(22);
                Ui.VList(details, 6, 0);
                var bartender = session.BartenderAt(place);
                BuildBarVisitors(w, place, bartender?.PersonId, booking);
                if (bartender != null)
                {
                    var worker = session.CityPerson(bartender.PersonId);
                    var character = ScenePersonTexture(worker.Id, false, out _);
                    if (character != null)
                    {
                        var behindCounter = Ui.Rect(w.Content, "Bartender").Anchor(.13f, .47f, .29f, .86f);
                        EventCgImage(behindCounter, worker.Id, character);
                    }
                    var row = Ui.Row(details, 48, 8);
                    Ui.Size(Ui.Label(row, worker.Name + " · 当班调酒师", 20, color: Ui.Accent), 0, 46, 1);
                    if (!drinking)
                        Ui.Size(Ui.Button(row, "交谈", () => OpenCityPerson(worker.Id), 16), 82, 36);
                }
                if (drinking)
                {
                    Line(details, name, 27, Ui.Accent);
                    var status = Line(details, "", 21);
                    var topics = ActivityTopicIndices(booking, definition);
                    var conversation = topics.Length == 0 ? null : Ui.Row(details, 42, 6);
                    if (conversation != null)
                    {
                        foreach (int topic in topics)
                        {
                            Button button = null;
                            button = Ui.Size(Ui.Button(conversation, definition.Choices[topic], () =>
                            {
                                if (SelectCityActivityOption(booking, definition, topic, "回到吧台")) button.interactable = false;
                            }, 16, Ui.ButtonBg, !booking.Choices.Contains(topic.ToString())), 0, 40, 1);
                        }
                        Ui.Size(Ui.Button(conversation, "喝完结算", () => finishDrink = true, 16, Ui.ButtonHot), 0, 40, 1);
                        conversation.gameObject.SetActive(playback >= .8f);
                    }
                    if (cup != null) cup.enabled = false;
                    w.RefreshContent = () =>
                    {
                        if (S != session || session.RunningCityActivity?.Id != booking.Id || session.CurrentPlace != place)
                        { w.Close(); return; }
                        if (_windows.LastOrDefault() != w || !Application.isFocused) return;
                        playback += Time.unscaledDeltaTime;
                        if (cup != null) cup.enabled = playback >= .8f;
                        if (conversation != null) conversation.gameObject.SetActive(playback >= .8f);
                        status.text = playback < .8f && bartender != null
                            ? $"{session.CityPerson(bartender.PersonId).Name}正在调饮" : basic ? "喝饮品中…" : "品尝套餐中…";
                        if (playback < 1.8f || conversation != null && !finishDrink || !session.FinishManualCityActivity(booking.Id)) return;
                        w.Close();
                    };
                    return;
                }
                if (BuildVenueWalkIn(w, details)) return;

                BuildOptions(0, drinkCount, "无酒精饮品");
                BuildOptions(drinkCount, drinks.Length - drinkCount, "套餐");

                void BuildOptions(int first, int count, string heading)
                {
                    if (count == 0) return;
                    Ui.Size(Ui.Label(details, heading, 17, color: Ui.Accent), -1, 24);
                    for (int start = first; start < first + count; start += 2)
                    {
                        var row = Ui.Row(details, 40, 8);
                        for (int i = start; i < Math.Min(start + 2, first + count); i++)
                        {
                            int option = i;
                            var drink = drinks[i];
                            string caption = $"{GameSession.VenueServiceName(drink.Definition, drink.Choice)} · {session.CityActivityEntryPrice(drink.Definition, drink.Choice, booking)}";
                            Ui.Size(Ui.Button(row, caption, () => { selected = option; w.RequestRebuild(); }, 17,
                                Ui.SelectedButton(selected == i)), 0, 40, 1);
                        }
                    }
                }
                bool canInvite = booking == null && definition.WalkInCapacity(choice) > 1;
                BuildVenueEffectPreview(details, service, canInvite || booking?.Participants.Length > 0);
                if (basic)
                {
                    var flavors = Ui.Row(details, 42, 8);
                    Ui.Size(Ui.Button(flavors, service.BarDrink == BarDrinkStyle.LemonSoda ? "保留柠檬酸味" : "原味", () =>
                    { honey = false; w.RequestRebuild(); }, 17, Ui.SelectedButton(!honey)), 0, 42, 1);
                    Ui.Size(Ui.Button(flavors, "加一点蜂蜜", () =>
                    { honey = true; w.RequestRebuild(); }, 17, Ui.SelectedButton(honey)), 0, 42, 1);
                }

                string blocker = confirmed ? session.VenueAppointmentStartBlocker(booking)
                    : session.VenueServiceBlocker(definition.Id, choice, booking?.Id,
                        walkInCompanions: canInvite ? Array.Empty<string>() : null, checkPrice: !canInvite);
                if (blocker != null) Para(details, blocker, 16, Ui.Warn);
                string action = canInvite ? "下一步 · 选择同行" : basic ? "点这杯" : "点这份套餐";
                Ui.Size(Ui.Button(details, $"{action} · {definition.Minutes} 分钟", () =>
                {
                    if (drinking) return;
                    if (canInvite)
                    {
                        SelectVenueWalkIn(w, definition, choice, OrderStarted, basic && honey);
                        return;
                    }
                    bool started = confirmed ? session.StartVenueAppointment(booking.Id, basic && honey)
                        : session.SelectVenueService(definition.Id, choice, booking?.Id, basic && honey);
                    if (started) OrderStarted();
                    else w.RequestRebuild();
                }, 20, Ui.ButtonHot, blocker == null), -1, 47);

                void OrderStarted()
                {
                    if (session.RunningCityActivity is CityActivity order && order.VenueId == place.Id)
                    {
                        booking = order;
                        drinking = true;
                        w.Closable = false;
                    }
                    w.RequestRebuild();
                }
            }, onClosed: () => _barCounterWindow = null, scrollContent: false, showTitleBar: false, fullScreen: true);
        }

        private void OpenKaraokeRoom()
        {
            var session = S;
            var activity = session.RunningCityActivity;
            var place = session.CurrentPlace;
            if (KaraokeInputActive || _karaokePicker is { Closed: false } || !GameSession.IsKaraokeActivity(activity) || place?.Business != "venue_ktv"
                || place.Id != activity.VenueId) return;

            int song = Mathf.Clamp(activity.KaraokeSongIndex, 0, GameSession.KaraokeSongs.Length - 1);
            int difficulty = Mathf.Clamp(activity.KaraokeDifficulty, 0, 2);
            bool ready = false;
            _karaokePicker = OpenWindow(place.Name + " · 选曲", 820, 440, w =>
            {
                var difficulties = Ui.Row(w.Content, 42, 6);
                string[] difficultyLabels = { "简单", "标准", "困难" };
                for (int i = 0; i < difficultyLabels.Length; i++)
                {
                    int option = i;
                    Ui.Size(Ui.Button(difficulties, difficultyLabels[i],
                        () => { difficulty = option; w.RequestRebuild(); }, 18, Ui.SelectedButton(difficulty == i)), 0, 42, 1);
                }

                var songs = GameSession.KaraokeSongs;
                for (int i = 0; i < songs.Length; i++)
                {
                    int option = i;
                    var reward = GameSession.KaraokeSongRewards(i, difficulty);
                    string bonus = reward.Mood > 0 ? $"心情 +{reward.Mood}"
                        : reward.Charm > 0 ? $"魅力 +{reward.Charm}" : $"见识 +{reward.Intelligence}";
                    Ui.Size(Ui.Button(w.Content, $"《{songs[i].Title}》　{ActivityDescriptionText(songs[i].Genre)}　·　{Ui.Emphasize("最高 " + bonus, Ui.Good)}",
                        () => { song = option; w.RequestRebuild(); }, 18, Ui.SelectedButton(song == i)), -1, 42);
                }
                Line(w.Content, "每首约 18 秒 · 命中越多，歌曲加成越高。", 16, Ui.Dim);
                var actions = Ui.Row(w.Content, 45, 8);
                Ui.Size(Ui.Button(actions, "听伴奏", () => Play(false), 17), 0, 44, 1);
                Ui.Size(Ui.Button(actions, "开始接唱", () => Play(true), 20, Ui.ButtonHot), 0, 44, 2);

                void Play(bool singing)
                {
                    if (!session.SelectKaraokeSong(activity.Id, song, difficulty)) return;
                    ready = true;
                    w.Close();
                    PlayKaraokeSong(session, activity, singing);
                }
            }, onClosed: () =>
            {
                _karaokePicker = null;
                if (!ready && S == session && session.RunningCityActivity?.Id == activity.Id)
                    session.CancelCityActivity(activity.Id, early: true);
            }, fitContent: true);
        }

        private void PlayKaraokeSong(GameSession session, CityActivity activity, bool singing)
        {
            var place = session.CurrentPlace;
            var partner = activity.Participants.FirstOrDefault(p => p.Phase == CityParticipantPhase.Participating);
            var service = S.CityActivityService(activity);
            string serviceName = GameSession.VenueServiceName(service, Math.Max(0, activity.ServiceChoice));
            bool duet = partner != null && !serviceName.Contains("独唱") && !serviceName.Contains("录音指导");
            var song = new KaraokeChorus(duet, activity.KaraokeSongIndex, activity.KaraokeDifficulty, singing);
            string title = GameSession.KaraokeSongs[activity.KaraokeSongIndex].Title;
            _karaokeWindow = OpenWindow(place.Name, 1920, 1080, w =>
            {
                BuildLeisureBackdrop(w.Content, "event_karaoke");
                var header = BuildLeisureHeader(w, place.Name + " · 包间", "结束包间", w.Close);
                var clock = header.Find("VenueClock").GetComponent<Text>();
                var topics = ActivityTopicIndices(activity, service);
                bool hasChoices = topics.Length > 0;
                float top = hasChoices ? .66f : partner != null ? .61f : .55f;
                var sheet = Ui.Panel(w.Content, "KaraokeControls", Ui.PanelBg).rectTransform.Anchor(
                    singing ? .16f : .535f, .065f, singing ? .84f : .97f, singing ? top : top - .17f);
                var box = Ui.Rect(sheet, "Song").Fill(23);
                Ui.VList(box, 8, 0);
                var heading = Ui.Row(box, 40, 8);
                Ui.Size(Ui.Label(heading, $"《{title}》", 28), 0, 38, 1);
                var score = singing ? Ui.Size(Ui.Label(heading, "", 21, TextAnchor.MiddleRight, Ui.Accent), 320, 38) : null;
                var progress = Ui.Size(Ui.Label(heading, "", 17, TextAnchor.MiddleRight, Ui.Dim), 145, 38);
                var lyric = Ui.Size(Ui.Label(box, "", 31, color: Ui.Accent), -1, 46);
                var next = Ui.Size(Ui.Label(box, "", 18, color: Ui.Dim), -1, 33);
                var tracks = new RectTransform[KaraokeChorus.Arrows.Length];
                var hitMarkers = new Image[tracks.Length];
                var notes = new (Image Head, Image Tail)[song.Notes.Length];
                Color trackColor = ArtDirection.Hex("292839");
                Color markerColor = ArtDirection.Hex("E8D9B4");
                Color hitColor = ArtDirection.Hex("FFF3C4");
                Color[] laneColors = { ArtDirection.Hex("D6B7F1"), ArtDirection.Hex("9FCBC6"),
                    ArtDirection.Hex("F5C998"), ArtDirection.Hex("A7BFF0") };
                if (singing)
                {
                    for (int i = 0; i < tracks.Length; i++)
                    {
                        var row = Ui.Size(Ui.Panel(box, "KaraokeLane", trackColor), -1, 43).rectTransform;
                        var key = Ui.Label(row, $"{KaraokeChorus.Arrows[i]}   {KaraokeChorus.Letters[i]}", 21,
                            TextAnchor.MiddleCenter, laneColors[i]);
                        key.rectTransform.Anchor(0, 0, .105f, 1);
                        key.raycastTarget = false;
                        var track = tracks[i] = Ui.Rect(row, "Track").Anchor(.105f, 0, 1, 1, 0, 0, 8);
                        track.gameObject.AddComponent<RectMask2D>();
                        var guide = Ui.Panel(track, "TrackGuide", ArtDirection.Hex("484657"));
                        guide.rectTransform.Anchor(0, .5f, 1, .5f, 0, -1, 0, -1);
                        guide.raycastTarget = false;
                        var marker = hitMarkers[i] = Ui.Panel(track, "HitLine", markerColor);
                        marker.rectTransform.Anchor(KaraokeChorus.HitLine, .10f, KaraokeChorus.HitLine, .90f, -2, 0, -2);
                        marker.raycastTarget = false;
                    }
                    for (int i = 0; i < notes.Length; i++)
                    {
                        var note = song.Notes[i];
                        var track = tracks[note.Lane];
                        var color = laneColors[note.Lane];
                        Image tail = note.Length > 0 ? Ui.Panel(track, "HoldTail", color) : null;
                        if (tail != null) tail.raycastTarget = false;
                        var head = Ui.Panel(track, "KaraokeNote", color);
                        head.raycastTarget = false;
                        var arrow = Ui.Label(head.transform, KaraokeChorus.Arrows[note.Lane], 21,
                            TextAnchor.MiddleCenter, trackColor);
                        arrow.rectTransform.Fill();
                        arrow.raycastTarget = false;
                        notes[i] = (head, tail);
                    }
                    Ui.Size(Ui.Label(box, "方向键 / WASD · 音符到亮线时按键，尾巴过线再松开", 17, color: Ui.Dim), -1, 28);
                }
                var feedback = Ui.Size(Ui.Label(box, "", 18, color: Ui.Good), -1, 32);
                if (partner != null)
                    BuildLeisureReply(box, partner.PersonId, duet ? "我们轮流来，你接的时候我就听着。" : "你先唱，我在这儿听。");

                if (hasChoices)
                {
                    var checks = Ui.Row(box, 42, 8);
                    foreach (int index in topics)
                    {
                        Button check = null;
                        check = Ui.Size(Ui.Button(checks, S.EventActivityChoiceLabel(service, index), () =>
                        {
                            if (SelectCityActivityOption(activity, service, index, "回到包间")) check.interactable = false;
                        }, 16, Ui.ButtonBg, !activity.Choices.Contains(index.ToString())), 0, 40, 1);
                    }
                }

                w.RefreshContent = () =>
                {
                    if (S != session || session.RunningCityActivity?.Id != activity.Id || session.CurrentPlace?.Id != place.Id)
                    { w.Close(); return; }
                    bool active = _windows.LastOrDefault() == w && Application.isFocused;
                    song.Advance(Time.unscaledDeltaTime, active);
                    if (active) song.JudgeInput();
                    if (active && (song.Playing || song.Ended))
                    {
                        string result = song.Ended
                            ? singing ? $"唱完了《{title}》。{song.Result}。" : $"听完了《{title}》。"
                            : null;
                        if (session.AdvanceKaraokePlayback(activity.Id, song.Position / song.Duration, result, singing, song.Accuracy))
                        {
                            w.Close();
                            return;
                        }
                        if (w.Closed || session.RunningCityActivity?.Id != activity.Id) { w.Close(); return; }
                    }
                    clock.text = Ui.Hm(session.Clock.MinuteOfDay);
                    progress.text = $"{song.Position:0.0} / {song.Duration:0.0} 秒";
                    lyric.text = song.Lyric;
                    next.text = song.NextLine;
                    feedback.text = song.Feedback;
                    if (score != null) score.text = $"得分 {song.Score}　连击 {song.Combo}";
                    if (singing)
                        for (int i = 0; i < hitMarkers.Length; i++)
                            hitMarkers[i].color = active && KaraokeChorus.LaneHeld(i) ? laneColors[i] : markerColor;
                    for (int i = 0; i < notes.Length; i++)
                    {
                        var note = song.Notes[i];
                        var (head, tail) = notes[i];
                        bool holding = note.State == KaraokeChorus.NoteState.Holding;
                        float x = holding ? KaraokeChorus.HitLine : TrackPosition(note.Start);
                        bool visible = song.Playing && (holding || note.State == KaraokeChorus.NoteState.Waiting)
                            && x > -.06f && x < 1.06f;
                        if (head.gameObject.activeSelf != visible) head.gameObject.SetActive(visible);
                        if (tail != null && tail.gameObject.activeSelf != visible) tail.gameObject.SetActive(visible);
                        if (!visible) continue;
                        bool lit = holding || song.InHitWindow(note);
                        head.color = lit ? hitColor : laneColors[note.Lane];
                        head.rectTransform.Anchor(x, .5f, x, .5f, -17, -15, -17, -15);
                        if (tail != null)
                        {
                            tail.color = head.color;
                            tail.rectTransform.Anchor(x, .5f, Mathf.Max(x, TrackPosition(note.End)), .5f, 0, -6, 0, -6);
                        }
                    }

                    float TrackPosition(float time) => KaraokeChorus.HitLine + (time - song.Position) / KaraokeChorus.PreviewSpan;
                };
                song.Start(w.Root, singing);
                w.RefreshContent();
            }, onClosed: () =>
            {
                song.Dispose();
                _karaokeWindow = null;
                if (S == session && session.RunningCityActivity?.Id == activity.Id)
                    session.CancelCityActivity(activity.Id, early: true);
            }, scrollContent: false,
                showTitleBar: false, fullScreen: true);
        }

        private sealed class KaraokeChorus
        {
            public enum NoteState { Waiting, Holding, Hit, Missed }

            public sealed class Note
            {
                public int Lane;
                public float Start, Length;
                public float End => Start + Length;
                public NoteState State;
                public int Points;
            }

            public const float HitLine = .17f, PreviewSpan = 2.75f;
            public static readonly string[] Arrows = { "←", "↓", "↑", "→" };
            public static readonly string[] Letters = { "A", "S", "W", "D" };
            private const float Beat = .625f;
            private const int IntroBeats = 4, BeatsPerPhrase = 5, ShortPhraseCount = 5;
            private const int Rate = 22050;
            private static readonly string[][] Lyrics =
            {
                new[] { "晚风慢一点，等灯亮起来", "我们就沿着湖边走", "窗里的歌，唱到这一句", "别急着把灯关掉",
                    "走过旧街口，雨就停了", "风吹过空空的站台", "等下一趟晚班车开走", "我们再唱一遍" },
                new[] { "绿灯亮起来，出门正是时候", "跟着鞋底敲的节拍", "店门口有人笑着招手", "喊我再走快一点",
                    "绕过街角，风追上来", "今天还有大把时间", "把没说完的话唱出来", "下一拍我们一起唱" },
                new[] { "石桥下面，水推着小船", "沿街的店收了遮阳伞", "修钟的人把门窗擦亮", "老树下还有人聊天",
                    "每次回来都认得这条路", "口袋里装着一张旧车票", "等屋檐下一盏盏灯亮了", "我就慢慢走回家" }
            };
            private static readonly int[][] Melodies =
            {
                new[] { 60, 64, 67, 64, 67, 69, 67, 64, 62, 64, 67, 72, 69, 67,
                    69, 72, 69, 67, 64, 65, 67, 65, 64, 62, 67, 69, 67, 64, 62,
                    64, 67, 69, 67, 64, 69, 67, 64, 62, 60, 65, 64, 62, 60, 60 },
                new[] { 72, 72, 76, 79, 76, 76, 79, 81, 79, 74, 74, 79, 83, 79,
                    76, 76, 81, 84, 81, 77, 77, 81, 84, 81, 76, 79, 81, 79, 76,
                    74, 76, 79, 83, 79, 76, 79, 81, 79, 76, 77, 76, 74, 72, 72 },
                new[] { 55, 60, 62, 64, 60, 60, 62, 67, 64, 62, 62, 67, 69, 67,
                    64, 64, 67, 69, 67, 60, 64, 67, 64, 62, 60, 60, 64, 67, 69,
                    67, 64, 62, 67, 64, 60, 62, 64, 62, 60, 55, 57, 60, 60, 60 }
            };
            private static readonly int[] Roots = { 48, 43, 45, 41, 48, 43, 45, 41 };
            private static readonly int[][] PhraseBeats = { new[] { 0, 3 }, new[] { 0, 2, 4 }, new[] { 0, 1, 3, 4 } };
            private readonly bool _duet;
            private readonly int _songIndex, _phraseCount;
            private int TrackBeats => IntroBeats + _phraseCount * BeatsPerPhrase;
            public float Duration => TrackBeats * Beat;
            private float Tolerance { get; }
            private AudioSource _source;
            private AudioClip _clip;
            private bool _suspended;
            private int _hits;
            private float _feedbackUntil;
            private string _feedback;
            public Note[] Notes { get; }
            public int Score { get; private set; }
            public int Combo { get; private set; }
            public int BestCombo { get; private set; }
            public double Accuracy => Notes.Length == 0 ? 0 : (double)_hits / Notes.Length;
            public string Result => $"得分 {Score} · 命中 {_hits}/{Notes.Length} · 最长连击 {BestCombo}";
            public bool InHitWindow(Note note) => Mathf.Abs(note.Start - Position) <= Tolerance;
            public bool Playing { get; private set; }
            public bool Singing { get; private set; }
            public bool Ended { get; private set; }
            public float Position { get; private set; }
            private string[] Lines => Lyrics[_songIndex];
            private int Phrase => Mathf.Clamp(Mathf.FloorToInt((Position / Beat - IntroBeats) / BeatsPerPhrase), 0, _phraseCount - 1);
            // The short arrangement keeps the song's closing phrase and the original tempo.
            private int SourcePhrase(int phrase) => phrase == _phraseCount - 1 ? Lines.Length - 1 : phrase;
            public string Lyric => Position < IntroBeats * Beat ? "前奏" : Lines[SourcePhrase(Phrase)];
            public string NextLine => Position < IntroBeats * Beat ? Lines[0]
                : Phrase < _phraseCount - 1 ? "下一句　" + Lines[SourcePhrase(Phrase + 1)] : "";
            public string Feedback => !Singing ? "听伴奏" : Notes.Any(note => note.State == NoteState.Holding) ? "继续按住"
                : Position < _feedbackUntil ? _feedback
                : _duet && Position >= IntroBeats * Beat && Phrase % 2 == 1 ? "同行者接唱" : "";

            public KaraokeChorus(bool duet, int song, int difficulty, bool singing)
            {
                _duet = duet;
                _songIndex = song;
                _phraseCount = Math.Min(ShortPhraseCount, Lines.Length);
                int level = Mathf.Clamp(difficulty, 0, PhraseBeats.Length - 1);
                Tolerance = level switch { 0 => .27f, 1 => .23f, _ => .19f };
                Notes = singing ? Enumerable.Range(0, _phraseCount)
                    .Where(phrase => !duet || phrase % 2 == 0)
                    .SelectMany(phrase => PhraseBeats[level].Select(beat =>
                    {
                        int melody = Melodies[_songIndex][IntroBeats + SourcePhrase(phrase) * BeatsPerPhrase + beat];
                        bool hold = level switch
                        {
                            0 => phrase == 2 && beat == 3,
                            1 => phrase % 2 == 0 && beat == 2,
                            _ => phrase != 0 && beat == 1
                        };
                        return new Note
                        {
                            Lane = (melody + phrase + beat + _songIndex) % Arrows.Length,
                            Start = (IntroBeats + BeatsPerPhrase * phrase + beat) * Beat,
                            Length = hold ? Beat : 0
                        };
                    })).ToArray() : Array.Empty<Note>();
            }

            public void Start(RectTransform parent, bool singing)
            {
                Position = _feedbackUntil = 0;
                _hits = Score = Combo = BestCombo = 0;
                foreach (var note in Notes) note.State = NoteState.Waiting;
                Playing = true;
                Singing = singing;
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

            public static bool LaneHeld(int lane) => lane switch
            {
                0 => DirectionalInput.LeftHeld,
                1 => DirectionalInput.DownHeld,
                2 => DirectionalInput.UpHeld,
                _ => DirectionalInput.RightHeld
            };

            private static bool LanePressed(int lane) => lane switch
            {
                0 => DirectionalInput.LeftPressed,
                1 => DirectionalInput.DownPressed,
                2 => DirectionalInput.UpPressed,
                _ => DirectionalInput.RightPressed
            };

            public void JudgeInput()
            {
                if (!Playing || !Singing) return;
                foreach (var note in Notes)
                {
                    if (note.State == NoteState.Holding)
                    {
                        bool held = LaneHeld(note.Lane);
                        if (Position >= note.End || !held && Position >= note.End - .13f) Hit(note, "长音完成");
                        else if (!held) Miss(note, "松得早了");
                        continue;
                    }
                    if (note.State != NoteState.Waiting) continue;
                    float delay = Position - note.Start;
                    if (delay > Tolerance) { Miss(note, "漏了一拍"); continue; }
                    if (delay < -Tolerance || !LanePressed(note.Lane)) continue;
                    bool precise = Mathf.Abs(delay) <= .115f;
                    note.Points = precise ? 100 : 75;
                    if (note.Length > 0)
                    {
                        note.State = NoteState.Holding;
                    }
                    else Hit(note, precise ? "精准" : "接上了");
                }
            }

            private void Hit(Note note, string label)
            {
                note.State = NoteState.Hit;
                _hits++;
                Score += note.Points;
                Combo++;
                BestCombo = Math.Max(BestCombo, Combo);
                ShowJudgment($"{label}　+{note.Points}");
            }

            private void Miss(Note note, string label)
            {
                note.State = NoteState.Missed;
                Combo = 0;
                ShowJudgment(label);
            }

            private void ShowJudgment(string text)
            {
                _feedback = text;
                _feedbackUntil = Position + .56f;
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

            private AudioClip CreateTrack()
            {
                var samples = new float[(int)(Duration * Rate)];
                var melodyNotes = Melodies[_songIndex];
                for (int beatIndex = 0; beatIndex < TrackBeats; beatIndex++)
                {
                    int offset = beatIndex - IntroBeats;
                    int phrase = SourcePhrase(Math.Max(0, offset / BeatsPerPhrase));
                    int melodyIndex = offset < 0 ? beatIndex : IntroBeats + phrase * BeatsPerPhrase + offset % BeatsPerPhrase;
                    double melodyHz = Frequency(melodyNotes[melodyIndex]);
                    int root = Roots[phrase];
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
                var clip = AudioClip.Create(GameSession.KaraokeSongs[_songIndex].Title, samples.Length, 1, Rate, false);
                clip.SetData(samples, 0);
                return clip;
            }

            private static double Frequency(int midi) => 2 * Math.PI * 440 * Math.Pow(2, (midi - 69) / 12.0);
        }
    }

    public sealed class VenueDrinkGraphic : MaskableGraphic
    {
        public BarDrinkStyle Drink;
        public bool Honey;

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
            bool hot = Drink == BarDrinkStyle.HotOolong;
            bool sparkling = Drink is BarDrinkStyle.LemonSoda or BarDrinkStyle.BerrySoda or BarDrinkStyle.MintAppleSoda;
            Color body = hot ? ArtDirection.Hex("B97C50") : new Color(.91f, .9f, .75f, .6f);
            Color liquid = ArtDirection.Hex(Drink switch
            {
                BarDrinkStyle.HotOolong => "92522D",
                BarDrinkStyle.BerrySoda => "BC546B",
                BarDrinkStyle.MintAppleSoda => "96B96A",
                BarDrinkStyle.JasmineColdBrew => "D2BD65",
                BarDrinkStyle.PeachBlackTea => "C47F53",
                _ => "ECC769"
            });
            if (Honey) liquid = Color.Lerp(liquid, ArtDirection.Hex("C17D38"), .32f);
            float bottom = .15f;
            float rim = hot ? .68f : .87f;
            if (hot)
            {
                Ellipse(.79f, .43f, .16f, .13f, ArtDirection.Hex("9C6347"));
                Ellipse(.8f, .44f, .092f, .072f, ArtDirection.Hex("3B322A"));
            }
            Polygon(body, new(.19f, rim), new(.79f, rim), new(.70f, bottom), new(.27f, bottom));
            Ellipse(.485f, bottom, .216f, .059f, body);
            if (!hot)
            {
                Polygon(liquid, new(.222f, .73f), new(.757f, .73f), new(.667f, bottom + .02f), new(.303f, bottom + .02f));
                Ellipse(.485f, .73f, .268f, .055f, Color.Lerp(liquid, Color.white, .34f));
                if (Honey) Ellipse(.485f, bottom + .026f, .18f, .025f, ArtDirection.Hex("BA782E"));
                for (int i = 0; sparkling && i < 8; i++)
                {
                    float x = .33f + (i * .111f) % .32f;
                    float y = .25f + (i * .141f) % .43f;
                    Ellipse(x, y, .009f + i % 2 * .005f, .012f, new Color(1f, .94f, .75f, .73f));
                }
                if (Drink == BarDrinkStyle.LemonSoda)
                {
                    Ellipse(.78f, .80f, .155f, .13f, ArtDirection.Hex("F3BE51"));
                    Ellipse(.78f, .80f, .125f, .103f, ArtDirection.Hex("FFF2B5"));
                    Ellipse(.78f, .80f, .108f, .088f, ArtDirection.Hex("F7D078"));
                }
            }
            Ellipse(.49f, rim, .30f, .055f, hot ? ArtDirection.Hex("D49E76") : new Color(1, .97f, .87f, .65f));
            Ellipse(.49f, rim, .258f, .039f, hot ? liquid : new Color(liquid.r, liquid.g, liquid.b, .52f));
            Polygon(new Color(1, .96f, .88f, .31f), new(.23f, rim - .04f), new(.26f, rim - .04f),
                new(.337f, bottom + .04f), new(.315f, bottom + .04f));
        }
    }
}
