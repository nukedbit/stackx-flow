using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StackX.Flow
{
    public sealed record DecisionFlowElementSuccess : FlowElementResult;
    
    public abstract class DecisionElement : FlowElement
    {
        private readonly List<IFlowElementExecute> _trueBranch;
        private readonly List<IFlowElementExecute> _falseBranch;

        protected DecisionElement(List<IFlowElementExecute> trueBranch, List<IFlowElementExecute> falseBranch)
        {
            _trueBranch = trueBranch;
            _falseBranch = falseBranch;
        }

        protected override async Task<FlowElementResult> OnExecuteAsync(object args, FlowState state)
        {
            try
            {
                if (await OnEvaluateAsync(args, state))
                {
                    return new DecisionFlowElementSuccess { Result = _trueBranch};
                }

                return new DecisionFlowElementSuccess { Result = _falseBranch};
            }
            catch (Exception e)
            {
                return new FlowErrorResult {ErrorObject = e};
            }
        }

        protected abstract Task<bool> OnEvaluateAsync(object args, FlowState state);
    }

    public class DecisionBuilder
    {
        private DecisionBuilder() {}
        
        public static IDecisionBuilder New() => new DecisionBuilderImpl();
    }

    public interface IDecisionBuilder
    {
        public IDecisionBranchesBuilder Decision(Func<object, Task<bool>> evaluate);
    }
    
    public interface IDecisionBranchesBuilder
    {
        public IDecisionBuild SetBranches(List<IFlowElementExecute> @true,List<IFlowElementExecute> @false);
    }

    public interface IDecisionBuild
    {
        IFlowElementExecute Build();
    }
    
    internal class DecisionBuilderImpl : IDecisionBuilder, IDecisionBuild, IDecisionBranchesBuilder
    {
        private Func<object, Task<bool>> _evaluate;
        private List<IFlowElementExecute> _trueBranch = new List<IFlowElementExecute>();
        private List<IFlowElementExecute> _falseBranch = new List<IFlowElementExecute>();
        
        public IDecisionBranchesBuilder Decision(Func<object, Task<bool>> evaluate)
        {
            _evaluate = evaluate;
            return this;
        }

        public IFlowElementExecute Build()
        {
            return new DecisionImpl(_trueBranch, _falseBranch, _evaluate);
        }

        class DecisionImpl : DecisionElement
        {
            private readonly Func<object, Task<bool>> _evaluate;

            public DecisionImpl(List<IFlowElementExecute> trueBranch, List<IFlowElementExecute> falseBranch, Func<object, Task<bool>> evaluate) : base(trueBranch, falseBranch)
            {
                _evaluate = evaluate;
            }

            protected override async Task<bool> OnEvaluateAsync(object args, FlowState state)
            {
                return await _evaluate(args);
            }
        }

        public IDecisionBuild SetBranches(List<IFlowElementExecute> @true, List<IFlowElementExecute> @false)
        {
            _trueBranch = @true;
            _falseBranch = @false;
            return this;
        }
    }
}