using System;
using System.Threading.Tasks;

namespace StackX.Flow
{
    public static class FlowExtensions
    {
        class MapFlowElement : FlowElement
        {
            private readonly Func<FlowElementResult, object, FlowState, Task<object>> _map;
            private readonly IFlowElementExecute _decorated;

            public MapFlowElement(Func<FlowElementResult, object, FlowState, Task<object>> map, IFlowElementExecute decorated)
            {
                _map = map;
                _decorated = decorated;
            }

            protected override async Task<CanExecuteResult> OnCanExecuteAsync(object args, FlowState state)
            {
                if (_decorated is IFlowElementCanExecute canExecute)
                {
                    return await canExecute.CanExecuteAsync(args, state);
                }
                return CanExecuteResult.Continue;
            }

            protected override async Task<FlowElementResult> OnExecuteAsync(object args, FlowState state)
            {
                var stepResult = await _decorated.ExecuteAsync(args, state);
                var mappedResult = await _map(stepResult, args, state);
                if (mappedResult is FlowElementResult el)
                {
                    return el;
                }

                return new FlowSuccessResult()
                {
                    Result = mappedResult
                };
            }
        }

        /// <summary>
        /// Execute the step you are mapping and do something on its result
        /// </summary>
        /// <param name="from">The step for which the value is being mapped</param>
        /// <param name="map">Map Action parameters are The FlowElementResult, the argument object, and flow state</param>
        /// <returns></returns>
        public static IFlowElementExecute Map(this IFlowElementExecute from, Func<FlowElementResult,object, FlowState, Task<object>> map)
        {
            return new MapFlowElement(map, from);
        }
    }
}