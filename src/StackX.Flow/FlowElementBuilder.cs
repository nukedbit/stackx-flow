using System;
using System.Threading.Tasks;

namespace StackX.Flow
{
    public static class FlowElementBuilder
    {
        public static IFlowElementBuilderCanExecute New() => new FlowElementBuilderImpl();
    }
    
    
    internal class FlowElementBuilderImpl: IFlowElementBuilderCanExecute, IFlowElementBuilderOnExecute, IFlowElementBuilderBuild
    {
        private Func<object, FlowState, Task<CanExecuteResult>> _canExecute;
        private Func<object, FlowState, Task<object>> _onExecute;
         
        
        public IFlowElementBuilderOnExecute CanExecuteContinue()
        {
            _canExecute = async (_,_) => CanExecuteResult.Continue;
            return this;
        }

        public IFlowElementBuilderOnExecute CanExecute(Func<object, FlowState, Task<CanExecuteResult>> canExecute)
        {
            _canExecute = canExecute;
            return this;
        }

        public IFlowElementBuilderBuild OnExecute(Func<object, FlowState, Task<object>> onExecute)
        {
            _onExecute = onExecute;
            return this;
        }

        public IFlowElementExecute Build()
        {
            return new FlowElementImpl(_canExecute, _onExecute);
        }
        
        internal class FlowElementImpl : FlowElement
        {
            private readonly Func<object, FlowState, Task<CanExecuteResult>> _canExecute;
            private readonly Func<object, FlowState, Task<object>> _onExecute;

            public FlowElementImpl(Func<object, FlowState,Task<CanExecuteResult>> canExecute, Func<object,FlowState,Task<object>> onExecute)
            {
                _canExecute = canExecute;
                _onExecute = onExecute;
            }

            protected override async Task<CanExecuteResult> OnCanExecuteAsync(object args, FlowState state)
            {
                return await _canExecute(args, state);
            }

            protected override async Task<FlowElementResult> OnExecuteAsync(object args, FlowState state)
            {
                var result = await _onExecute(args, state);
                return result switch
                {
                    FlowElementResult elementResult => elementResult,
                    object r => new FlowSuccessResult {Result = r}
                };
            }
        }
    }

    public interface IFlowElementBuilderCanExecute
    {
        public IFlowElementBuilderOnExecute CanExecuteContinue();
        public IFlowElementBuilderOnExecute CanExecute(Func<object, FlowState, Task<CanExecuteResult>> canExecute);
    }
    
    public interface IFlowElementBuilderOnExecute
    {
        public IFlowElementBuilderBuild OnExecute(Func<object, FlowState, Task<object>> onExecute);
    }

    public interface IFlowElementBuilderBuild
    {
        public IFlowElementExecute Build();
    }
}