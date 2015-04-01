using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class AbstractAnswerable {
        public const LetterOptions AnswerDefaultOptions = LetterOptions.Ack;

        protected AbstractAnswerable(Guid receivedFrom, Guid conversationId) {
            RemoteNodeId = receivedFrom;
            ConversationId = conversationId;
        }

        public Guid RemoteNodeId { get; private set; }
        public Guid ConversationId { get; private set; }
    }
}