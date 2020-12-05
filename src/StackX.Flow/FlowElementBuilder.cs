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
        private Func<object, FlowState, Task<bool>> _canExecute;
        private Func<object, FlowState, Task<object>> _onExecute;
         
        
        public IFlowElementBuilderOnExecute CanExecuteYes()
        {
            _canExecute = async (_,_) => true;
            return this;
        }

        public IFlowElementBuilderOnExecute CanExecute(Func<object, FlowState, Task<bool>> canExecute)
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
            private readonly Func<object, FlowState, Task<bool>> _canExecute;
            private readonly Func<object, FlowState, Task<object>> _onExecute;

            public FlowElementImpl(Func<object, FlowState,Task<bool>> canExecute, Func<object,FlowState,Task<object>> onExecute)
            {
                _canExecute = canExecute;
                _onExecute = onExecute;
            }

            protected override async Task<bool> OnCanExecuteAsync(object args, FlowState state)
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
        public IFlowElementBuilderOnExecute CanExecuteYes();
        public IFlowElementBuilderOnExecute CanExecute(Func<object, FlowState, Task<bool>> canExecute);
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