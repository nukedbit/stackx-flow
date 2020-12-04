using ServiceStack.Logging;
using ServiceStack.Text;
using System;
using System.Threading.Tasks;

namespace StackX.Flow
{
    internal class LoggingCanExecuteFlowElementDecorator : CanExecuteFlowElement, ILoggingFlowElementDecorator
    {
        private readonly ILog _logger;
        private readonly CanExecuteFlowElement _element;

        public LoggingCanExecuteFlowElementDecorator(CanExecuteFlowElement element)
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
                if(_logger.IsDebugEnabled)
                    _logger.Debug($"ExecuteAsyncInternal result={res}, args={JsonSerializer.SerializeToString(args)}");
                
                return res;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error on ExecuteAsyncInternal with args={JsonSerializer.SerializeToString(args)}");
                throw;
            }
        }

        internal override async Task<bool> CanExecuteInternalAsync(object args, FlowState state)
        {
            if (!IsLoggingEnabled)
                return await _element.CanExecuteInternalAsync(args, state);
            try
            {
                var res = await _element.CanExecuteInternalAsync(args, state);
                if (_logger.IsDebugEnabled)
                {
                    _logger.Debug($"CanExecuteAsyncInternal result={res}, args={JsonSerializer.SerializeToString(args)}");
                }
                return res;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error on CanExecuteAsyncInternal with args={JsonSerializer.SerializeToString(args)}");
                throw;
            }
        }
    }
}