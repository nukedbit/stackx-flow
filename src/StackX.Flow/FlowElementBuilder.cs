using System;
using System.Threading.Tasks;

namespace StackX.Flow
{
    public static class FlowElementBuilder
    {
        public static IFlowElementBuilderCanExecute<TArgs> New<TArgs>() => new FlowElementBuilder<TArgs>();
    }
    
    
    internal class FlowElementBuilder<TArgs>: IFlowElementBuilderCanExecute<TArgs>, IFlowElementBuilderOnExecute<TArgs>, IFlowElementBuilderBuild<TArgs>
    {
        private Func<TArgs, FlowState, Task<bool>> _canExecute;
        private Func<TArgs, FlowState, Task<object>> _onExecute;
         
        
        public IFlowElementBuilderOnExecute<TArgs> Yes()
        {
            _canExecute = async (_,_) => true;
            return this;
        }

        public IFlowElementBuilderOnExecute<TArgs> CanExecute(Func<TArgs, FlowState, Task<bool>> canExecute)
        {
            _canExecute = canExecute;
            return this;
        }

        public IFlowElementBuilderBuild<TArgs> OnExecute(Func<TArgs, FlowState, Task<object>> onExecute)
        {
            _onExecute = onExecute;
            return this;
        }

        public FlowElement Build()
        {
            return new FlowElementImpl<TArgs>(_canExecute, _onExecute);
        }
        
        internal class FlowElementImpl<TArgs> : FlowElement<TArgs>
        {
            private readonly Func<TArgs, FlowState, Task<bool>> _canExecute;
            private readonly Func<TArgs, FlowState, Task<object>> _onExecute;

            public FlowElementImpl(Func<TArgs, FlowState,Task<bool>> canExecute, Func<TArgs,FlowState,Task<object>> onExecute)
            {
                _canExecute = canExecute;
                _onExecute = onExecute;
            }

            protected override async Task<bool> CanExecuteAsync(TArgs args, FlowState state)
            {
                return await _canExecute(args, state);
            }

            protected override async Task<FlowElementResult> OnExecuteAsync(TArgs args, FlowState state)
            {
                var result = await _onExecute(args, state);
                return result switch
                {
                    FlowElementResult elementResult => elementResult,
                    object r => new FlowSuccessResult() {Result = r}
                };
            }
        }
    }

    public interface IFlowElementBuilderCanExecute<TArgs>
    {
        public IFlowElementBuilderOnExecute<TArgs> Yes();
        public IFlowElementBuilderOnExecute<TArgs> CanExecute(Func<TArgs, FlowState, Task<bool>> canExecute);
    }
    
    public interface IFlowElementBuilderOnExecute<TArgs>
    {
        public IFlowElementBuilderBuild<TArgs> OnExecute(Func<TArgs, FlowState, Task<object>> onExecute);
    }

    public interface IFlowElementBuilderBuild<TArgs>
    {
        public FlowElement Build();
    }
}