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
        protected abstract Task<FlowElementResult> ExecuteAsync(object input, FlowState state);

        public Task<FlowElementResult> ExecuteInternalAsync(object args, FlowState state)
        {
            return ExecuteAsync(args, state);
        }
    }
}