using OSistema.Core;
using OSistema.Social;

namespace OSistema.Crime
{
    /// <summary>
    /// Product doesn't come from a menu. Nobody sells to a stranger: Don Saverio has to see you're
    /// reliable on his corner before he sends you to the man who sells by the kilo, and every order
    /// is then placed with him in person, at night.
    /// </summary>
    public sealed partial class Organization
    {
        public const int ShiftsToVouch = 3;
        public const float RespectToVouch = 8f;

        public int LookoutShifts;
        public bool SupplierIntroduced;

        public string SupplierProgress =>
            $"Keep watch for Don Saverio until he vouches for you: shifts {LookoutShifts}/{ShiftsToVouch}, respect {Respect:0}/{RespectToVouch:0}.";

        void CheckIntroduction(Game game)
        {
            if (SupplierIntroduced || LookoutShifts < ShiftsToVouch || Respect < RespectToVouch) return;
            int hour = game.Clock.Hour;
            if (hour < 16 || hour > 23) return;
            foreach (var m in game.Phone.Messages)
                if (m.KindOf == MsgKind.Intro && (m.Open(game.Ticks.TicksGame) || game.Ticks.TicksGame - m.Expires < GameClock.TicksPerDay)) return;

            var msg = game.Phone.Send(game, MsgKind.Intro, ContactBook.Saverio,
                "Si' 'nu guaglione serio. Stasera te faccio cunoscere a uno ca te po' servì.",
                "You're a serious kid. Tonight I'll introduce you to someone who can be useful to you.",
                "Don Saverio will vouch for you with Zì Tore, who sells product by the kilo. After this, you answer for what you buy.", 12f);
            msg.Choices.Add("Thank him");
            msg.Hints.Add("Zì Tore will meet you at night. Orders are made with him, in person.");
            msg.Choices.Add("Not yet");
            msg.Hints.Add("Stay out of the drug trade for now. He'll ask again.");
        }

        public void OnIntroAnswer(PhoneMessage m, int choice, Game game)
        {
            if (choice != 0) return;
            SupplierIntroduced = true;
            var tore = game.Contacts.ByName(ContactBook.Supplier);
            game.Log.Add($"Don Saverio vouched for you. Zì Tore 'o Grossista meets buyers at night ({tore?.Hours}). Go and see him to order product.",
                MessageKind.Good, tore?.Spot);
        }
    }
}
