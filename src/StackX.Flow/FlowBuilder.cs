using ServiceStack.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StackX.Flow
{
    public sealed class FlowBuilder
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(FlowBuilder));
        readonly List<FlowElement> _pipelineElements = new();
        private ErrorHandler _errorHandler;
        private RestartFilter? _restartFilter;
        private int? _restartCountLimit;
        private DefaultStatusManager _defaultStatusManager;


        public FlowBuilder() : this(false)
        {
            
        }
        public FlowBuilder(bool enableLogging)
        {
            IsLoggingEnabled = enableLogging;
            _errorHandler = new DefaultErrorHandler();
            _defaultStatusManager = new DefaultStatusManager();
        }

        public bool IsLoggingEnabled { get; private set; }

        public void SetLogging(bool enable)
        {
            IsLoggingEnabled = enable;
            foreach (var el in _pipelineElements)
            {
                if (el is ILoggingFlowElementDecorator decorator)
                {
                    decorator.SetLogging(enable);
                }
            }
        }

        private void AddDecorated(FlowElement element)
        {
            if (_logger is null)
            {
                _pipelineElements.Add(element);
                return;
            }
            if (element is CanExecuteFlowElement canExecutePipeElement)
            {
                var decorator = new LoggingCanExecuteFlowElementDecorator(canExecutePipeElement);
                decorator.SetLogging(IsLoggingEnabled);
                _pipelineElements.Add(decorator);
            }
            else
            {
                var decorator = new LoggingFlowElementDecorator(element);
                decorator.SetLogging(IsLoggingEnabled);
                _pipelineElements.Add(decorator);
            }
        }

        public FlowBuilder Add(FlowElement element)
        {
            if (UnWrap(_pipelineElements.LastOrDefault()) is DecisionElement)
            {
                throw new ArgumentException("You can't add another element after a Decision");
            }
            AddDecorated(element);
            return this;
        }

        private FlowElement UnWrap(FlowElement element)
        {
            if (element is LoggingFlowElementDecorator el)
            {
                return el.WrappedElement;
            }

            return element;
        }
        
        public FlowBuilder Add<TElement>()
            where TElement : FlowElement
        {
            if (UnWrap(_pipelineElements.LastOrDefault()) is DecisionElement)
            {
                throw new ArgumentException("You can't add another element after a Decision");
            }
            var newElement = Activator.CreateInstance<TElement>();
            AddDecorated(newElement);
            return this;
        }

        public FlowBuilder OnError(ErrorHandler errorHandler)
        {
            _errorHandler = errorHandler;
            return this;
        }

        public FlowBuilder SetRestartLimit(int restartLimit)
        {
            _restartCountLimit = restartLimit;
            return this;
        }

        public FlowBuilder OnRestart(RestartFilter restartFilter)
        {
            _restartFilter = restartFilter;
            return this;
        }

        public FlowBuilder SetStatusManager(DefaultStatusManager defaultStatusManager)
        {
            _defaultStatusManager = defaultStatusManager;
            return this;
        }

        public IFlowElement<TInput> Build<TInput>()
        {
            if (_errorHandler is null) throw new NullReferenceException("Error handler should not be null");
            return new Flow<TInput>(_pipelineElements, _errorHandler, 
                _restartFilter, _defaultStatusManager, _restartCountLimit);
        }
    }
}
