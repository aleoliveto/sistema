using System;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.UI
{
    /// <summary>The family bar, the arms and rival sections, event cards and the game-over screen.</summary>
    public sealed partial class Hud
    {
        /// <summary>Raised when the player starts over from the game-over screen.</summary>
        public event Action NewGameRequested;

        void DrawFamilyBar()
        {
            var org = game.Org;
            var boss = org.Boss(game);
            string bossText = boss == null ? "<color=#ff7a70>No boss</color>" : $"<b>{boss.DisplayName}</b> {boss.LastName}";
            string state = org.Hiding ? $"  ·  <color=#ffb070>IN LATITANZA (day {game.Clock.Day - org.HidingSinceDay + 1})</color>"
                : org.WarrantActive ? "  ·  <color=#ff7a70>WARRANT</color>" : "";
            string pizzini = org.PendingPizzini > 0 ? $"  ·  {org.PendingPizzini} pizzini pending" : "";
            string rival = game.Rival.Destroyed ? $"{game.Rival.Name}: destroyed"
                : game.Rival.InTruce(game) ? $"{game.Rival.Name}: truce"
                : $"{game.Rival.Name} hostility {game.Rival.Hostility:0}";
            GUI.Label(new Rect(10, 32, Width - 20, 22),
                $"Generation {org.Generation}  ·  {bossText}  ·  Evidence {org.Evidence:0}/100{state}{pizzini}  ·  {rival}", textStyle);
        }

        void DrawArms()
        {
            GUILayout.Label("Arms", headerStyle);
            GUILayout.Label($"Pistols in the stash: {game.Stock.Pistols}. Armed crew defend themselves and fight when drafted, but get longer sentences if arrested.", smallStyle);
            if (GUILayout.Button($"Buy a pistol (€{Prices.Pistol})", GUILayout.Width(200)))
            {
                if (game.Stock.Cash < Prices.Pistol) game.Log.Add($"A pistol costs €{Prices.Pistol}.", OSistema.Core.MessageKind.Bad);
                else
                {
                    game.Stock.Cash -= Prices.Pistol;
                    game.Stock.Pistols++;
                    game.Log.Add("A pistol, wrapped in a cloth, now sits in the stash.");
                }
            }
        }

        void DrawRival()
        {
            var rival = game.Rival;
            GUILayout.Label(rival.Name, headerStyle);
            if (rival.Destroyed)
            {
                GUILayout.Label("Destroyed. The district is yours.", smallStyle);
                return;
            }
            GUILayout.Label($"Boss: {rival.BossName}  ·  Soldiers: {rival.Soldiers}  ·  Piazze: {rival.Squares.Count}", smallStyle);
            string mood = rival.InTruce(game) ? $"Truce until day {rival.TruceUntilDay}"
                : rival.Hostility >= RivalClan.HitHostility ? "<color=#ff7a70>They want blood: ambushes likely</color>"
                : rival.Hostility >= RivalClan.StesaHostility ? "<color=#ffb070>Angry: expect a stesa</color>"
                : "Watching you";
            GUILayout.Label($"Hostility {rival.Hostility:0}/100 · {mood}", smallStyle);
            GUILayout.Label("Piazze near their territory anger them. Draft armed crew and send them at a rival pusher to take a piazza by force.", smallStyle);
            if (GUILayout.Button("Send a gift (€500, hostility −25)", GUILayout.Width(240)) && !rival.SendGift(500, 25f, game))
                game.Log.Add("Not enough cash for a gift.", OSistema.Core.MessageKind.Bad);
        }

        // ---------- Event cards ----------

        void DrawEvent(GameEvent e)
        {
            // Dim the world behind the card.
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Width, Height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = 560f;
            float h = 210f + e.Options.Count * 52f;
            var r = new Rect((Width - w) / 2f, (Height - h) / 2f, w, h);
            GUI.Box(r, GUIContent.none);
            GUI.Box(r, GUIContent.none); // double box = more opaque in the default skin

            float x = r.x + 18, y = r.y + 14, cw = w - 36;
            GUI.Label(new Rect(x, y, cw, 26), $"<size=18><b>{e.Title}</b></size>", titleStyle);
            y += 34;
            GUI.Label(new Rect(x, y, cw, 22), $"<i>“{e.Neapolitan}”</i>", barkLeftStyle);
            y += 20;
            GUI.Label(new Rect(x, y, cw, 20), $"<i>{e.English}</i>", smallStyle);
            y += 28;
            GUI.Label(new Rect(x, y, cw, 70), e.Body, textStyle);
            y += 78;

            for (int i = 0; i < e.Options.Count; i++)
            {
                var option = e.Options[i];
                bool available = game.Events.IsAvailable(option, game);
                GUI.enabled = available;
                if (GUI.Button(new Rect(x, y, cw, 28), option.Label)) game.Events.Choose(i, game);
                GUI.enabled = true;
                GUI.Label(new Rect(x + 6, y + 28, cw, 18), available ? option.Hint : "<color=#ff7a70>Not possible right now.</color>", smallStyle);
                y += 52;
            }
        }

        void DrawGameOver()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Width, Height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = 520f, h = 220f;
            var r = new Rect((Width - w) / 2f, (Height - h) / 2f, w, h);
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 18, r.y + 16, w - 36, 30), "<size=22><b>The family is finished</b></size>", titleStyle);
            GUI.Label(new Rect(r.x + 18, r.y + 56, w - 36, 60), game.Org.GameOverReason, textStyle);
            GUI.Label(new Rect(r.x + 18, r.y + 120, w - 36, 22),
                $"Survived {game.Clock.Day} days across {game.Org.Generation} generation(s).", textStyle);
            if (GUI.Button(new Rect(r.x + 18, r.yMax - 46, 200, 30), "Start a new family")) NewGameRequested?.Invoke();
        }
    }
}
