using System;

namespace Hyperletter.Typed {
    public class AnswerCallbackEventArgs<TRequest, TReply> : System.EventArgs {
        public AnswerCallbackEventArgs(IAnswerable<TReply> answer, TRequest answerFor) {
            Answer = answer;
            AnswerFor = answerFor;
        }

        public AnswerCallbackEventArgs(TRequest answerFor, Exception exception) {
            AnswerFor = answerFor;
            Exception = exception;
        }

        public TRequest AnswerFor { get; private set; }
        public IAnswerable<TReply> Answer { get; private set; }
        public Exception Exception { get; private set; }

        public bool Success {
            get { return Exception == null; }
        }
    }
}