using ServiceStack.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StackX.Flow
{
    public sealed class FlowBuilder
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(FlowBuilder));
        readonly List<IFlowElementExecute> _pipelineElements = new();
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

        private void AddDecorated(IFlowElementExecute elementExecute)
        {
            if (_logger is null)
            {
                _pipelineElements.Add(elementExecute);
                return;
            }
            if (elementExecute is IFlowElementCanExecute canExecutePipeElement)
            {
                var decorator = new LoggingCanExecuteDecorator(canExecutePipeElement);
                decorator.SetLogging(IsLoggingEnabled);
                _pipelineElements.Add(decorator);
            }
            else
            {
                var decorator = new LoggingExecuteDecorator(elementExecute);
                decorator.SetLogging(IsLoggingEnabled);
                _pipelineElements.Add(decorator);
            }
        }

        public FlowBuilder Add(IFlowElementExecute elementExecute)
        {
            if (UnWrap(_pipelineElements.LastOrDefault()) is DecisionElement)
            {
                throw new ArgumentException("You can't add another element after a Decision");
            }
            AddDecorated(elementExecute);
            return this;
        }
        
        public FlowBuilder Add(params IFlowElementExecute[] elements)
        {
            foreach (var elementExecute in elements)
            {
                if (UnWrap(_pipelineElements.LastOrDefault()) is DecisionElement)
                {
                    throw new ArgumentException("You can't add another element after a Decision");
                }
                AddDecorated(elementExecute);
            }
            
            return this;
        }

        private IFlowElementExecute UnWrap(IFlowElementExecute elementExecute)
        {
            IFlowElementExecute element = elementExecute;
            while (true)
            {
                if (element is LoggingExecuteDecorator ex)
                {
                    element = ex.WrappedElement;
                    continue;
                } 
                
                if (element is LoggingCanExecuteDecorator el)
                {
                    element = el.WrappedElement;
                    continue;
                }
                break;
            }

            return element;
        }
        
        public FlowBuilder Add<TElement>()
            where TElement : IFlowElementExecute
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

        public IFlow Build()
        {
            if (_errorHandler is null) throw new NullReferenceException("Error handler should not be null");
            return new Flow(_pipelineElements, _errorHandler, 
                _restartFilter, _defaultStatusManager, _restartCountLimit);
        }
    }
}
