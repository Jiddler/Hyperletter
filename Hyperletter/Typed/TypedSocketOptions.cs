using System;

namespace Hyperletter.Typed {
    public class TypedSocketOptions {
        public SocketOptions Socket { get; set; }
        public TimeSpan AnswerTimeout { get; set; }

        public TypedSocketOptions() {
            Socket = new SocketOptions();
            AnswerTimeout = TimeSpan.FromSeconds(10);
        }
    }
}