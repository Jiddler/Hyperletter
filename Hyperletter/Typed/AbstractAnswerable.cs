using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class AbstractAnswerable {
        public static LetterOptions AnswerDefaultOptions = LetterOptions.Ack;

        public Guid RemoteNodeId { get; set; }
        public Guid ConversationId { get; set; }

        protected AbstractAnswerable(Guid receivedFrom, Guid conversationId) {
            RemoteNodeId = receivedFrom;
            ConversationId = conversationId;
        }
    }
}