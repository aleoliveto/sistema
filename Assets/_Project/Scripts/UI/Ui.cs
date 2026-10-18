using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>Tiny helpers for building UI Toolkit trees from code with the classes in OSistema.uss.</summary>
    public static class Ui
    {
        public static T Cls<T>(this T e, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static T Into<T>(this T e, VisualElement parent) where T : VisualElement
        {
            parent.Add(e);
            return e;
        }

        public static VisualElement Box(params string[] classes) => new VisualElement().Cls(classes);

        public static VisualElement Row(VisualElement parent = null)
        {
            var e = Box("row");
            parent?.Add(e);
            return e;
        }

        /// <summary>A vertical scroll area that takes the room its parent has left and scrolls the rest.
        /// Give the parent "fit" so the fixed parts around it keep their size. Returns where to add content.</summary>
        public static VisualElement Scroll(VisualElement parent, params string[] contentClasses)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical).Cls("scroll").Into(parent);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.contentContainer.Cls(contentClasses);
            return scroll.contentContainer;
        }

        public static Label Text(string text, VisualElement parent, params string[] classes)
        {
            var l = new Label(text).Cls(classes);
            l.pickingMode = PickingMode.Ignore;
            parent?.Add(l);
            return l;
        }

        public static Button Btn(string text, Action onClick, VisualElement parent, params string[] classes)
        {
            var b = new Button(() =>
            {
                OSistema.Views.SoundManager.PlayUi(OSistema.Views.AudioSynth.Click, 0.25f);
                onClick?.Invoke();
            }) { text = text };
            b.Cls("btn").Cls(classes);
            parent?.Add(b);
            return b;
        }

        public static void Toggle(this VisualElement e, string cls, bool on)
        {
            if (on) e.AddToClassList(cls);
            else e.RemoveFromClassList(cls);
        }

        public static void Show(this VisualElement e, bool visible) =>
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>A labelled progress bar. Returns the fill element so callers can update it.</summary>
        public static (Label value, VisualElement fill) Bar(string label, Color color, VisualElement parent, bool thin = false)
        {
            Label valueLabel = null;
            if (!thin)
            {
                var head = Box("bar-label").Into(parent);
                Text(label, head, "muted");
                valueLabel = Text("", head);
            }
            var bar = Box("bar").Into(parent);
            if (thin) bar.AddToClassList("bar--thin");
            var fill = Box("bar__fill").Into(bar);
            fill.style.backgroundColor = color;
            fill.pickingMode = PickingMode.Ignore;
            bar.pickingMode = PickingMode.Ignore;
            return (valueLabel, fill);
        }

        public static void SetFill(VisualElement fill, float t) =>
            fill.style.width = Length.Percent(Mathf.Clamp01(t) * 100f);

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>A vertical gradient texture for panel backgrounds (top colour to bottom colour).</summary>
        public static Texture2D Gradient(Color top, Color bottom)
        {
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(h - 1)));
            tex.Apply();
            return tex;
        }
    }
}
