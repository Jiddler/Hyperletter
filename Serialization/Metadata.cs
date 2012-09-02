using System;

namespace Hyperletter.Dispatcher {
    public class Metadata {
        public Metadata() {
        }

        public Metadata(Type type) {
            Type = type.AssemblyQualifiedName;
        }

        public string Type { get; set; }
    }
}