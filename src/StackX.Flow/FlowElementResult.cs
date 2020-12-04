namespace StackX.Flow
{
    public abstract record FlowElementResult
    {
        public virtual object Result { get; init; }
    }


    public record FlowErrorResult : FlowElementResult
    {
        public object ErrorObject { get; init; }
    }

    public record FlowSuccessResult : FlowElementResult;

    public record FlowGoToEndResult : FlowSuccessResult;

    public record FlowRestartResult : FlowElementResult;

    public record FlowRestartLimitReachedResult : FlowElementResult;
}
