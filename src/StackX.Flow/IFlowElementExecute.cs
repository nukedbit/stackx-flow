using System.Threading.Tasks;

namespace StackX.Flow
{
    public interface IFlowElementExecute
    {
        Task<FlowElementResult> ExecuteAsync(object args, FlowState state);
    }


    public abstract record CanExecuteResult
    {
        public static CanExecuteResult Continue { get; } = new CanExecuteYesResult();
        public static CanExecuteResult Skip { get; } = new CanExecuteSkipResult();
        public static CanExecuteResult Error(object errorObject) => new CanExecuteErrorResult(errorObject);
        
        public static implicit operator CanExecuteResult(bool d) => d ? Continue : Skip;
    };

    public record CanExecuteYesResult : CanExecuteResult;
    
    public record CanExecuteSkipResult : CanExecuteResult;
    
    public record CanExecuteErrorResult(object ErrorResult) : CanExecuteResult;
    
    
    
    
    
    public interface IFlowElementCanExecute : IFlowElementExecute
    {
        Task<CanExecuteResult> CanExecuteAsync(object args, FlowState state);
    }  
    
    public interface IFlowElement : IFlowElementCanExecute {}
}