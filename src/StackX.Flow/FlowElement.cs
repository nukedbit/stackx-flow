using StackX.Flow.Converters;
using System.Threading.Tasks;

namespace StackX.Flow
{
    public abstract class FlowElement : IFlowElement
    {
        protected virtual Converter[] Converters => new Converter[0];

        internal abstract Task<FlowElementResult> ExecuteInternalAsync(object args, FlowState state);       
    }
}