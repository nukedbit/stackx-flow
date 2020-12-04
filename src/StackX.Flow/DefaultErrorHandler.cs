using System.Threading.Tasks;

namespace StackX.Flow
{
    public class DefaultErrorHandler : ErrorHandler
    {
        protected override Task<FlowElementResult> OnExecuteAsync(FlowErrorResult error)
        {
            return Task.FromResult<FlowElementResult>(error);
        }
    }
}