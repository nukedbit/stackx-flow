using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StackX.Flow
{
    public sealed record DecisionFlowElementSuccess : FlowElementResult;
    
    public abstract class DecisionElement : FlowElement
    {
        private readonly List<FlowElement> _trueBranch;
        private readonly List<FlowElement> _falseBranch;

        protected DecisionElement(List<FlowElement> trueBranch, List<FlowElement> falseBranch)
        {
            _trueBranch = trueBranch;
            _falseBranch = falseBranch;
        }

        internal override async Task<FlowElementResult> ExecuteInternalAsync(object args, FlowState state)
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
        
        public static IDecisionBuilder<TArgs> New<TArgs>() => new DecisionBuilder<TArgs>();
    }

    public interface IDecisionBuilder<out TArgs>
    {
        public IDecisionBranchesBuilder Decision(Func<TArgs, Task<bool>> evaluate);
    }
    
    public interface IDecisionBranchesBuilder
    {
        public IDecisionBuild SetBranches(List<FlowElement> @true,List<FlowElement> @false);
    }

    public interface IDecisionBuild
    {
        FlowElement Build();
    }
    
    internal class DecisionBuilder<TArgs> : IDecisionBuilder<TArgs>, IDecisionBuild, IDecisionBranchesBuilder
    {
        private Func<TArgs, Task<bool>> _evaluate;
        private List<FlowElement> _trueBranch = new List<FlowElement>();
        private List<FlowElement> _falseBranch = new List<FlowElement>();
        
        public IDecisionBranchesBuilder Decision(Func<TArgs, Task<bool>> evaluate)
        {
            _evaluate = evaluate;
            return this;
        }

        public FlowElement Build()
        {
            return new DecisionImpl(_trueBranch, _falseBranch, _evaluate);
        }

        class DecisionImpl : DecisionElement
        {
            private readonly Func<TArgs, Task<bool>> _evaluate;

            public DecisionImpl(List<FlowElement> trueBranch, List<FlowElement> falseBranch, Func<TArgs, Task<bool>> evaluate) : base(trueBranch, falseBranch)
            {
                _evaluate = evaluate;
            }

            protected override async Task<bool> OnEvaluateAsync(object args, FlowState state)
            {
                return await _evaluate((TArgs)args);
            }
        }

        public IDecisionBuild SetBranches(List<FlowElement> @true, List<FlowElement> @false)
        {
            _trueBranch = @true;
            _falseBranch = @false;
            return this;
        }
    }
}