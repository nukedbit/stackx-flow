using System;
using System.Linq;
using System.Threading.Tasks;

namespace StackX.Flow
{
    public interface IFilter : IFlowElementExecute
    {

    }

    public abstract class Filter : IFilter
    {
        protected abstract Task<FlowElementResult> OnExecuteAsync(object input, FlowState state);

        public Task<FlowElementResult> ExecuteAsync(object args, FlowState state)
        {
            return OnExecuteAsync(args, state);
        }
    }
}