using System;

namespace Hyperletter.Typed {
    public class Metadata {
        public Metadata() {
        }

        public Metadata(Type type) {
            Type = type.AssemblyQualifiedName;
        }

        public string Type { get; set; }
        public Guid ConversationId { get; set; }
    }
}