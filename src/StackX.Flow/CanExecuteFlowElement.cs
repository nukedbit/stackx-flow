using System.Threading.Tasks;

namespace StackX.Flow
{
    public abstract class CanExecuteFlowElement : FlowElement
    {
        internal abstract Task<bool> CanExecuteInternalAsync(object args, FlowState state);
    }
}