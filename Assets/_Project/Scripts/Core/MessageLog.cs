using System.Collections.Generic;

namespace OSistema.Core
{
    public enum MessageKind { Info, Good, Bad }

    public readonly struct Message
    {
        public readonly string Text;
        public readonly MessageKind Kind;
        public readonly long Tick;

        public Message(string text, MessageKind kind, long tick)
        {
            Text = text;
            Kind = kind;
            Tick = tick;
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

        public void Add(string text, MessageKind kind = MessageKind.Info)
        {
            messages.Add(new Message(text, kind, ticks.TicksGame));
            if (messages.Count > MaxMessages) messages.RemoveAt(0);
        }
    }
}
