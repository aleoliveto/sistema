using OSistema.Core;
using OSistema.Pawns;
using OSistema.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// "Who are you?" Name, face, clothes and hair seen from above, where he grew up, and a handful
    /// of skill points. Then the game starts with him.
    /// </summary>
    public sealed partial class Hud
    {
        CharacterSetup creatorSetup;

        void BuildCreator()
        {
            creatorSetup ??= new CharacterSetup();
            var s = creatorSetup;

            var card = Ui.Box("panel", "card", "creator").Into(menuDim);
            var band = Ui.Box("card__band").Into(card);
            band.style.backgroundColor = new Color(0.16f, 0.09f, 0.06f);
            Ui.Text("WHO ARE YOU?", band, "menu__title");
            Ui.Text("Seventeen years old, a basso in the rione, and nothing yet. Every family starts with one guaglione.", band, "menu__sub");

            var cols = Ui.Row(Ui.Box("menu__body").Into(card));
            cols.style.alignItems = Align.FlexStart;

            // ---- Left: face and looks ----
            var left = Ui.Box("col").Into(cols);
            left.style.width = 360;
            left.style.marginRight = 24;
            var pics = Ui.Row(left);
            var portrait = Ui.Box("creator__portrait").Into(pics);
            var preview = Ui.Box("creator__preview").Into(pics);
            var layers = new VisualElement[5];
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = Ui.Box("creator__layer").Into(preview);
                layers[i].pickingMode = PickingMode.Ignore;
            }

            void Refresh()
            {
                var l = s.Look;
                portrait.style.backgroundImage = new StyleBackground(CharacterArt.Face(l.Face));
                SetLayer(layers[0], CharacterArt.Texture("body", l.Body));
                SetLayer(layers[1], CharacterArt.Texture("neck", l.Neck));
                SetLayer(layers[2], CharacterArt.Texture("head", l.Head));
                SetLayer(layers[3], CharacterArt.Texture("hair", l.Hair));
                SetLayer(layers[4], CharacterArt.Texture("hat", l.Hat));
            }

            Picker(left, "Face", () => s.Look.Face, v => s.Look.Face = v, CharacterArt.Count("face"), false, Refresh);
            Picker(left, "Clothes", () => s.Look.Body, v => s.Look.Body = v, CharacterArt.Count("body"), false, Refresh);
            Picker(left, "Skin", () => s.Look.Head, v => s.Look.Head = v, CharacterArt.Count("head"), false, Refresh);
            Picker(left, "Hair", () => s.Look.Hair, v => s.Look.Hair = v, CharacterArt.Count("hair"), false, Refresh);
            Picker(left, "Hat", () => s.Look.Hat, v => s.Look.Hat = v, CharacterArt.Count("hat"), true, Refresh);
            Picker(left, "Chain", () => s.Look.Neck, v => s.Look.Neck = v, CharacterArt.Count("neck"), true, Refresh);
            Refresh();

            // ---- Right: name, roots, skills ----
            var right = Ui.Box("col", "grow").Into(cols);
            right.style.width = 420;
            Ui.Text("NAME", right, "section");
            var first = Field(right, "First name", s.FirstName, v => s.FirstName = v);
            var last = Field(right, "Surname", s.LastName, v => s.LastName = v);
            var nick = Field(right, "Nickname", s.Nickname, v => s.Nickname = v);
            Ui.Btn("Roll a name", () =>
            {
                var rng = new System.Random();
                s.FirstName = Story.Names.Pick(Story.Names.FirstNames, rng);
                s.LastName = Story.Names.Pick(Story.Names.Surnames, rng);
                s.Nickname = Story.Names.Pick(Story.Names.Nicknames, rng);
                first.value = s.FirstName;
                last.value = s.LastName;
                nick.value = s.Nickname;
            }, right, "btn--small").style.alignSelf = Align.FlexStart;

            Ui.Text("WHERE HE COMES FROM", right, "section");
            var bgRow = Ui.Row(right);
            var bgText = Ui.Text("", right, "small", "muted", "wrap");
            var bgButtons = new Button[3];
            foreach (OSistema.Core.Background b in System.Enum.GetValues(typeof(OSistema.Core.Background)))
            {
                var bg = b;
                bgButtons[(int)b] = Ui.Btn(CharacterSetup.BackgroundName(b), () => { s.Background = bg; UpdateBg(); }, bgRow, "btn--small");
            }
            void UpdateBg()
            {
                for (int i = 0; i < bgButtons.Length; i++) bgButtons[i].Toggle("btn--on", (int)s.Background == i);
                bgText.text = CharacterSetup.BackgroundText(s.Background);
            }
            UpdateBg();

            Ui.Text("SKILLS", right, "section");
            var pointsLabel = Ui.Text("", right, "small");
            var values = new Label[6];
            for (int i = 0; i < 6; i++)
            {
                int k = i;
                var row = Ui.Row(right);
                var name = Ui.Text(CharacterSetup.SkillNames[i], row);
                name.style.width = 110;
                Tip(name, () => CharacterSetup.SkillHints[k]);
                Ui.Btn("−", () => { if (s.Skills[k] > 0) s.Skills[k]--; UpdateSkills(); }, row, "btn--small");
                values[i] = Ui.Text("", row, "bold");
                values[i].style.width = 30;
                values[i].style.unityTextAlign = TextAnchor.MiddleCenter;
                Ui.Btn("+", () => { if (s.Skills[k] < CharacterSetup.MaxSkill && s.PointsUsed < CharacterSetup.SkillPoints) s.Skills[k]++; UpdateSkills(); }, row, "btn--small");
                Ui.Text(CharacterSetup.SkillHints[i], row, "tiny").style.marginLeft = 8;
            }
            void UpdateSkills()
            {
                for (int i = 0; i < 6; i++) values[i].text = s.Skills[i].ToString();
                int left = CharacterSetup.SkillPoints - s.PointsUsed;
                pointsLabel.text = left > 0 ? $"<color=#E8C26A>{left} point{(left == 1 ? "" : "s")} left to spend</color>" : "All points spent.";
            }
            UpdateSkills();

            var buttons = Ui.Row(right);
            buttons.style.marginTop = 14;
            Ui.Btn("Back", () => OpenMenu(Menu.Title), buttons, "menu-btn");
            var start = Ui.Btn("Start", () =>
            {
                if (string.IsNullOrWhiteSpace(s.FirstName)) s.FirstName = "Gennaro";
                if (string.IsNullOrWhiteSpace(s.LastName)) s.LastName = "Iaccarino";
                if (string.IsNullOrWhiteSpace(s.Nickname)) s.Nickname = "'o Russo";
                var chosen = s;
                creatorSetup = null;
                CloseMenu();
                NewGameRequested?.Invoke(chosen);
            }, buttons, "menu-btn");
            start.style.marginLeft = 10;
            start.AddToClassList("btn--on");
        }

        static void SetLayer(VisualElement e, Texture2D tex)
        {
            e.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground((Texture2D)null);
            e.Show(tex != null);
        }

        void Picker(VisualElement parent, string label, System.Func<int> get, System.Action<int> set, int count, bool allowNone, System.Action changed)
        {
            var row = Ui.Row(parent);
            row.style.alignItems = Align.Center;
            Ui.Text(label, row, "small").style.width = 70;
            var value = Ui.Text("", row, "small");
            void Show() => value.text = get() < 0 ? "none" : $"{get() + 1} / {count}";
            int Step(int v, int d)
            {
                int min = allowNone ? -1 : 0;
                int span = count - min;
                if (span <= 0) return v;
                return ((v - min + d) % span + span) % span + min;
            }
            Ui.Btn("◀", () => { set(Step(get(), -1)); Show(); changed(); }, row, "btn--small").style.marginLeft = 4;
            Ui.Btn("▶", () => { set(Step(get(), 1)); Show(); changed(); }, row, "btn--small");
            value.style.marginLeft = 8;
            value.BringToFront();
            Show();
        }

        static TextField Field(VisualElement parent, string label, string value, System.Action<string> set)
        {
            var row = Ui.Row(parent);
            row.style.alignItems = Align.Center;
            Ui.Text(label, row, "small").style.width = 90;
            var f = new TextField { value = value, maxLength = 24 };
            f.style.flexGrow = 1;
            f.RegisterValueChangedCallback(e => set(e.newValue));
            row.Add(f);
            return f;
        }
    }
}
