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

        private void BuildEventCg(Transform parent, EventCgFrame frame, float height)
        {
            var background = Resources.Load<Texture2D>("EventCg/" + frame.Background);
            if (background == null) return;
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
                    else if (frame.Seated)
                        propFrame.Anchor(.47f, .055f, .69f, .24f, 0, 0, 0, 0);
                    else
                        propFrame.Anchor(edge - .20f, .08f, edge, .49f, 0, 0, 0, 0);
                    EventCgImage(propFrame, frame.Prop.ToString(), prop);
                }
            }

            if (person != null)
            {
                var seatedCharacter = frame.Seated ? Resources.Load<Texture2D>("EventCg/person_" + person.Id + "_seated") : null;
                var character = seatedCharacter != null ? seatedCharacter : Resources.Load<Texture2D>("EventCg/person_" + person.Id);
                if (character != null)
                {
                    var holder = Ui.Rect(canvas, "SceneParticipant");
                    if (seatedCharacter != null)
                        holder.Anchor(.63f, .28f, .98f, .86f, 0, 0, 0, 0); // The dining tabletop begins at this lower edge.
                    else
                        holder.Anchor(.71f, 0f, 1f, .98f, 0, 0, 0, 0);
                    EventCgImage(holder, person.Id, character);
                }
                else
                {
                    var holder = Ui.Rect(canvas, "ParticipantPortrait").Anchor(.73f, .08f, .975f, .90f, 0, 0, 0, 0);
                    var portrait = FitPortrait(holder, person.Id);
                    portrait.raycastTarget = false;
                    var border = portrait.gameObject.AddComponent<Outline>();
                    border.effectColor = Ui.Chrome;
                    border.effectDistance = new Vector2(2, -2);
                }
                EventCgCaption(canvas, person.Name, .73f, .98f, TextAnchor.MiddleCenter);
            }
            if (!string.IsNullOrEmpty(frame.Caption))
                EventCgCaption(canvas, frame.Caption, .02f, person == null ? .75f : .49f, TextAnchor.MiddleLeft);
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
