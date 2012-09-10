using System;

namespace Hyperletter.Typed {
    public class TypedSocketOptions {
        public SocketOptions SocketOptions { get; set; }
        public TimeSpan AnswerTimeout { get; set; }

        public TypedSocketOptions() {
            SocketOptions = new SocketOptions();
            AnswerTimeout = TimeSpan.FromSeconds(10);
        }
    }
}