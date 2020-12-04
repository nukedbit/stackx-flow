using System.Diagnostics.CodeAnalysis;

namespace StackX.Flow
{
    public static class FlowElementExtensions
    {
        public static FlowElementResult Success<TResult>(this IFlowElement element, [NotNull] TResult result)
        {
            return new FlowSuccessResult {Result = result};
        }

        public static FlowElementResult Error<TResult>(this IFlowElement element, [NotNull] TResult error)
        {
            return new FlowErrorResult {ErrorObject = error};
        }

        public static FlowElementResult Error<TResult, TError>(this IFlowElement element, [NotNull] TError error,
            [NotNull] TResult result)
        {
            return new FlowErrorResult {ErrorObject = error, Result = result};
        }

        public static FlowElementResult GoToEnd<TResult>(this IFlowElement element, [NotNull] TResult result)
        {
            return new FlowGoToEndResult {Result = result};
        }

        public static FlowElementResult Restart<TResult>(this IFlowElement element, [NotNull] TResult result)
        {
            return new FlowRestartResult {Result = result};
        }
    }
}
