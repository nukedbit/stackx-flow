using System.Threading.Tasks;

namespace StackX.Flow
{
    public interface IFlowElement
    {

    }  
    public interface IFlowElement<in TInput>
    {
        Task<FlowElementResult> RunAsync(TInput input);
    }
}