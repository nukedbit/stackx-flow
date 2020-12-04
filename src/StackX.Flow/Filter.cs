using System;
using System.Linq;
using System.Threading.Tasks;

namespace StackX.Flow
{
    public interface IFilter
    {

    }

    public abstract class Filter<TInput> : FlowElement, IFilter
    {

        protected virtual Task<FlowElementResult> ExecuteAsync(TInput input, FlowState state)
        {
            throw new NotImplementedException(nameof(ExecuteAsync));
        }

        internal override Task<FlowElementResult> ExecuteInternalAsync(object args, FlowState state)
        {
            var converter = Converters.SingleOrDefault(t => t.CanConvert(args.GetType()));
            var input = converter == null ? args : converter.Convert(args);
            return ExecuteAsync((TInput)input, state);
        }
    }
}