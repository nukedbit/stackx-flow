namespace StackX.Flow
{
    public abstract class CanExecuteFlowElement : FlowElement
    {
        internal abstract bool CanExecuteInternal(object args, FlowState state);
    }
}