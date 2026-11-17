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
            EventCgImage(canvas, "Location", background, true);

            var person = string.IsNullOrEmpty(frame.PersonId) ? null : S.CityPerson(frame.PersonId);
            if (frame.Prop != EventCgProp.None)
            {
                var prop = Resources.Load<Texture2D>("EventCg/prop_" + frame.Prop.ToString().ToLowerInvariant());
                if (prop != null)
                {
                    float edge = frame.Seated ? .55f : person == null ? .97f : .69f;
                    var propFrame = Ui.Rect(canvas, "OutcomeProp").Anchor(edge - .20f, .08f, edge, .49f, 0, 0, 0, 0);
                    EventCgImage(propFrame, frame.Prop.ToString(), prop);
                }
            }

            if (person != null)
            {
                var character = frame.Seated ? Resources.Load<Texture2D>("EventCg/person_" + person.Id + "_seated") : null;
                character = character != null ? character : Resources.Load<Texture2D>("EventCg/person_" + person.Id);
                if (character != null)
                {
                    var holder = Ui.Rect(canvas, "SceneParticipant").Anchor(.71f, .03f, 1f, frame.Seated ? .77f : .98f);
                    EventCgImage(holder, person.Id, character);
                }
                else
                {
                    var holder = Ui.Panel(canvas, "ParticipantPortrait", Ui.Chrome);
                    holder.raycastTarget = false;
                    holder.rectTransform.Anchor(.77f, .08f, .975f, .90f, 0, 0, 0, 0);
                    var portrait = FitPortrait(holder.rectTransform, person.Id, 6);
                    portrait.raycastTarget = false;
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
