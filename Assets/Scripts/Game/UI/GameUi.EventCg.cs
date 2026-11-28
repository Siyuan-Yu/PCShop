using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static RawImage EventCgImage(Transform parent, string name, Texture2D texture, bool fill = false)
        {
            var image = Ui.Rect(parent, name).Fill().gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            if (fill)
            {
                var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = (float)texture.width / texture.height;
            }
            else FitImageToParent(image);
            return image;
        }

        private static Texture2D ScenePersonTexture(string id, bool seated, out bool hasSeatedArt)
        {
            var seatedArt = seated ? Resources.Load<Texture2D>("EventCg/person_" + id + "_seated") : null;
            hasSeatedArt = seatedArt != null;
            return seatedArt ?? Resources.Load<Texture2D>("EventCg/person_" + id)
                ?? ScenePortraits.FromPortrait(Resources.Load<Texture2D>("PortraitsAnime/" + id) ?? PortraitOf(id));
        }

        private RectTransform BuildEventCg(Transform parent, EventCgFrame frame, float height, bool featureDish = false)
        {
            var background = Resources.Load<Texture2D>("EventCg/" + frame.Background);
            if (background == null) return null;
            var canvas = Ui.Size(Ui.Rect(parent, "EventIllustration"), -1, height);
            canvas.gameObject.AddComponent<RectMask2D>();
            var location = EventCgImage(canvas, "Location", background, true);
            location.color = frame.Lighting switch
            {
                EventCgLighting.Blackout => new Color(.34f, .38f, .49f),
                EventCgLighting.Emergency => new Color(.67f, .65f, .55f),
                EventCgLighting.Candle => new Color(.49f, .40f, .36f),
                EventCgLighting.Evening => new Color(.94f, .84f, .79f),
                EventCgLighting.Rain => new Color(.72f, .79f, .89f),
                _ => Color.white
            };

            var person = string.IsNullOrEmpty(frame.PersonId) ? null : S.CityPerson(frame.PersonId);
            if (frame.Prop != EventCgProp.None)
            {
                var prop = Resources.Load<Texture2D>("EventCg/prop_" + frame.Prop.ToString().ToLowerInvariant());
                if (prop != null)
                {
                    float edge = person == null ? .97f : .69f;
                    var propFrame = Ui.Rect(canvas, "OutcomeProp");
                    if (frame.Prop == EventCgProp.EmergencyLamp)
                        propFrame.Anchor(.69f, .71f, .93f, .94f, 0, 0, 0, 0);
                    else if (featureDish)
                        propFrame.Anchor(.32f, .015f, .70f, .53f, 0, 0, 0, 0);
                    else if (frame.Seated)
                        propFrame.Anchor(.47f, .055f, .69f, .24f, 0, 0, 0, 0);
                    else
                        propFrame.Anchor(edge - .20f, .08f, edge, .49f, 0, 0, 0, 0);
                    EventCgImage(propFrame, frame.Prop.ToString(), prop);
                }
            }

            if (person != null)
            {
                var character = ScenePersonTexture(person.Id, frame.Seated, out bool hasSeatedArt);
                if (character != null)
                {
                    var holder = Ui.Rect(canvas, "SceneParticipant");
                    if (hasSeatedArt)
                        holder.Anchor(.63f, .28f, .98f, .86f, 0, 0, 0, 0); // The dining tabletop begins at this lower edge.
                    else
                        holder.Anchor(.71f, 0f, 1f, .98f, 0, 0, 0, 0);
                    EventCgImage(holder, person.Id, character);
                }
                EventCgCaption(canvas, person.Name, .73f, .98f, TextAnchor.MiddleCenter);
            }
            if (!string.IsNullOrEmpty(frame.Caption))
                EventCgCaption(canvas, frame.Caption, .02f, person == null ? .75f : .49f, TextAnchor.MiddleLeft);
            return canvas;
        }

        private static void EventCgCaption(Transform parent, string caption, float left, float right, TextAnchor alignment)
        {
            var strip = Ui.Panel(parent, "IllustrationCaption", Ui.Chrome);
            strip.raycastTarget = false;
            strip.rectTransform.Anchor(left, 0f, right, 0f, 0, 10, 0, -43);
            var label = Ui.Label(strip.transform, caption, 16, alignment);
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 16;
            label.rectTransform.Fill(7);
        }
    }
}
