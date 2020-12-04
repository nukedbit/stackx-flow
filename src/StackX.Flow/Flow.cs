using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace StackX.Flow
{
    class Flow<TInput> : IFlowElement<TInput>
    {
        private readonly List<FlowElement> _elements;
        private readonly ErrorHandler _errorHandler;
        private readonly RestartFilter? _restartFilter;
        private readonly DefaultStatusManager _defaultStatusManager;
        private readonly int? _restartCountLimit;

        internal Flow(List<FlowElement> elements, ErrorHandler errorHandler,
            RestartFilter? restartFilter, DefaultStatusManager defaultStatusManager, int? restartCountLimit)
        {
            _elements = elements;
            _errorHandler = errorHandler;
            _restartFilter = restartFilter;
            _defaultStatusManager = defaultStatusManager;
            _restartCountLimit = restartCountLimit;
        }

        public async Task<FlowElementResult> RunAsync(TInput input)
        {
            _defaultStatusManager.Reset();
            _defaultStatusManager.SetInitialInput(input);
            return await RunInternalAsync(input);
        }

        private async Task<FlowElementResult> OnRestartAsync(FlowRestartResult result, FlowState state)
        {
            if (_restartCountLimit.HasValue && _defaultStatusManager.RestartCount > _restartCountLimit.Value - 1)
                return new FlowRestartLimitReachedResult { Result = result };
            var value = TryExecuteRestartFilter(result, state);
            _defaultStatusManager.IncRestartCount();
            return await RunInternalAsync(@value);
        }

        private object TryExecuteRestartFilter(FlowRestartResult result, FlowState state)
        {
            var @value = result.Result;
            if (_restartFilter != null)
            {
                @value = _restartFilter.ExecuteInternalAsync(result,state).Result;
            }
            return value;
        }

        private async Task<FlowElementResult> RunInternalAsync(object input)
        {
            FlowElementResult result = new FlowSuccessResult {Result = input};
            var pipeState = _defaultStatusManager.BuildPipelineState(result);
            var elements = _elements;
            decision:
            var enumerator = elements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var element = enumerator.Current;
                bool canExecute = await CheckCanExecuteAsync(element, result.Result, pipeState);
                if (!canExecute)
                    continue;
                
                if (element is FlowElement flowElement)
                {
                    var r = await flowElement.ExecuteInternalAsync(result.Result, pipeState);
                    if (r is DecisionFlowElementSuccess decisionResult)
                    {
                        elements = decisionResult.Result as List<FlowElement>;
                        goto decision;
                    }

                    result = r;
                }

                if (result is FlowErrorResult errorResult)
                {
                    result = await _errorHandler.ExecuteInternalAsync(errorResult);
                    if (result is FlowErrorResult)
                    {
                        break;
                    }
                }

                if (IsExitResult(result))
                {
                    break;
                }
                pipeState = _defaultStatusManager.BuildPipelineState(result);
            }

            if (result is FlowRestartResult restartResult)
                result = await OnRestartAsync(restartResult, pipeState);
            return result;
        }

        private static bool IsExitResult(FlowElementResult result)
        {
            return result is FlowGoToEndResult || result is FlowRestartResult || result is FlowRestartLimitReachedResult;
        }

        private static async Task<bool> CheckCanExecuteAsync(IFlowElement element, object currentInput, FlowState state)
        {
            try
            {
                if (element is CanExecuteFlowElement canExecute)
                {
                    return await canExecute.CanExecuteInternalAsync(currentInput, state);
                }
                return true;
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;
                throw;
            }
        }
    }
}