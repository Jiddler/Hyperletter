using System;

namespace Hyperletter.Typed {
    public class TypedSocketOptions {
        public TypedSocketOptions() {
            Socket = new SocketOptions();
            AnswerTimeout = TimeSpan.FromSeconds(10);
        }

        public SocketOptions Socket { get; set; }
        public TimeSpan AnswerTimeout { get; set; }
    }
}