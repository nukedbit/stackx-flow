using System.Threading.Tasks;

namespace StackX.Flow
{
    public interface IFlowElementExecute
    {
        Task<FlowElementResult> ExecuteAsync(object args, FlowState state);
    }  
    
    public interface IFlowElementCanExecute : IFlowElementExecute
    {
        Task<bool> CanExecuteAsync(object args, FlowState state);
    }  
    
    public interface IFlowElement : IFlowElementCanExecute {}
}