using System;
using System.Threading.Tasks;

namespace StackX.Flow
{
    public abstract class ErrorHandler
    {
        protected virtual Task<FlowElementResult> OnExecuteAsync(FlowErrorResult error)
        {
            throw new NotImplementedException();
        }

        internal Task<FlowElementResult> ExecuteInternalAsync(FlowErrorResult error) =>
            OnExecuteAsync(error);
    }
}