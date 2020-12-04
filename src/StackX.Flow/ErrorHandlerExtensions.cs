using System.Diagnostics.CodeAnalysis;

namespace StackX.Flow
{
    public static class ErrorHandlerExtensions
    {
        public static FlowElementResult Success<TResult>(this ErrorHandler errorHandler, [NotNull]TResult result)
        {
            return new FlowSuccessResult() { Result = result };
        }

        public static FlowElementResult Error<TResult>(this ErrorHandler errorHandler, [NotNull]TResult error)
        {
            return new FlowErrorResult() {ErrorObject = error};
        }
        
        public static FlowElementResult Error<TResult, TError>(this ErrorHandler errorHandler, [NotNull]TError error, [NotNull]TResult result)
        {
            return new FlowErrorResult() {ErrorObject = error, Result = result};
        }
        
        public static FlowElementResult GoToEnd<TResult>(this ErrorHandler errorHandler, [NotNull]TResult result)
        {
            return new FlowGoToEndResult() {Result = result};
        }
        
        public static FlowElementResult Restart<TResult>(this ErrorHandler errorHandler, [NotNull]TResult result)
        {
            return new FlowRestartResult() {Result = result};
        }
    }
}