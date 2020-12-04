using ServiceStack.Logging;
using ServiceStack.Text;
using System;
using System.Threading.Tasks;

namespace StackX.Flow
{
    internal class LoggingFlowElementDecorator : FlowElement, ILoggingFlowElementDecorator
    {
        private readonly ILog _logger;
        private readonly FlowElement _element;

        internal FlowElement WrappedElement => _element;

        public LoggingFlowElementDecorator(FlowElement element)
        {
            _logger = LogManager.GetLogger(element.GetType());
            _element = element;
        }

        public void SetLogging(bool enable) => IsLoggingEnabled = enable;

        public bool IsLoggingEnabled { get; private set; }


        internal override async Task<FlowElementResult> ExecuteInternalAsync(object args, FlowState state)
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