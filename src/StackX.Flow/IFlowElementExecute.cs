using System.Threading.Tasks;

namespace StackX.Flow
{
    public interface IFlowElementExecute
    {
        Task<FlowElementResult> ExecuteInternalAsync(object args, FlowState state);
    }  
    
    public interface IFlowElementCanExecute : IFlowElementExecute
    {
        Task<bool> CanExecuteInternalAsync(object args, FlowState state);
    }  
    
    public interface IFlowElement : IFlowElementCanExecute {}
}