using System.Collections.Generic;

namespace OSistema.Core
{
    public enum MessageKind { Info, Good, Bad }

    public readonly struct Message
    {
        public readonly string Text;
        public readonly MessageKind Kind;
        public readonly long Tick;
        public readonly UnityEngine.Vector2Int? Location; // click the notification to go there

        public Message(string text, MessageKind kind, long tick, UnityEngine.Vector2Int? location)
        {
            Text = text;
            Kind = kind;
            Tick = tick;
            Location = location;
        }
    }

    /// <summary>Event feed shown on the left of the HUD. Later this becomes the newspaper and wiretap feed.</summary>
    public sealed class MessageLog
    {
        const int MaxMessages = 50;

        readonly List<Message> messages = new List<Message>();
        readonly TickManager ticks;

        public MessageLog(TickManager ticks) => this.ticks = ticks;

        public IReadOnlyList<Message> All => messages;

        public int Version { get; private set; }

        /// <summary>Developer soak runs copy every message here (to the editor log), to read afterwards.</summary>
        public static System.Action<string> Trace;

        public void Add(string text, MessageKind kind = MessageKind.Info, UnityEngine.Vector2Int? location = null)
        {
            Version++;
            Trace?.Invoke(text);
            messages.Add(new Message(text, kind, ticks.TicksGame, location));
            if (messages.Count > MaxMessages) messages.RemoveAt(0);
        }
    }
}
