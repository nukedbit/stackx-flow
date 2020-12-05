using ServiceStack.Logging;
using ServiceStack.Text;
using System;
using System.Threading.Tasks;

namespace StackX.Flow
{
    internal class LoggingExecuteDecorator : IFlowElementExecute, ILoggingFlowElementDecorator
    {
        private readonly ILog _logger;
        private readonly IFlowElementExecute _element;

        internal IFlowElementExecute WrappedElement => _element;

        public LoggingExecuteDecorator(IFlowElementExecute element)
        {
            _logger = LogManager.GetLogger(element.GetType());
            _element = element;
        }

        public void SetLogging(bool enable) => IsLoggingEnabled = enable;

        public bool IsLoggingEnabled { get; private set; }


        public async Task<FlowElementResult> ExecuteInternalAsync(object args, FlowState state)
        {
            if (!IsLoggingEnabled)
                return await _element.ExecuteInternalAsync(args, state);
            try
            {
                var res = await _element.ExecuteInternalAsync(args, state);
                _logger.Info($"ExecuteAsyncInternal result={res}, args={JsonSerializer.SerializeToString(args)}");
                return res;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error on ExecuteAsyncInternal with args={JsonSerializer.SerializeToString(args)}");
                throw;
            }
        }
    }
}