using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class AbstractAnswerable {
        public static LetterOptions AnswerDefaultOptions = LetterOptions.Ack;

        protected AbstractAnswerable(Guid receivedFrom, Guid conversationId) {
            RemoteNodeId = receivedFrom;
            ConversationId = conversationId;
        }

        public Guid RemoteNodeId { get; set; }
        public Guid ConversationId { get; set; }
    }
}