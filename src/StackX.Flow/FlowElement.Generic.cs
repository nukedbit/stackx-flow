using System;
using System.Linq;
using System.Threading.Tasks;

namespace StackX.Flow
{
    /// <summary>
    /// This is the base class for all pipe tasks, each one can override CanExecute, to indicated if
    /// based on the received parameters if it can execute this task.
    /// In the Execute override you can write your own task logic
    /// </summary>
    /// <typeparam name="TSArgs"></typeparam>
    public abstract class FlowElement<TSArgs> : CanExecuteFlowElement
    {
        /// <summary>
        /// Determine if the task can be executed by default it's always true
        /// </summary>
        /// <param name="args">Task Argument Object</param>
        /// <param name="state">Pipe Status Object</param>
        /// <returns></returns>
        protected virtual Task<bool> CanExecuteAsync(TSArgs args, FlowState state)
        {
            return Task.FromResult(true);
        }
        
        internal override async Task<bool> CanExecuteInternalAsync(object args, FlowState state)
        {
            if (args is TSArgs tsArgs)
                return await CanExecuteAsync(tsArgs, state);
            if (Converters.Length == 0)
                return await CanExecuteAsync((TSArgs)args, state);
            var converter = Converters.SingleOrDefault(t => t.CanConvert(args.GetType()));
            var input = converter == null ? args : converter.Convert(args);
            return await CanExecuteAsync((TSArgs)input, state);
        }

        /// <summary>
        /// Define here your own task logic 
        /// </summary>
        /// <param name="args">Task Argument</param>
        /// <param name="state">Pipe state</param>
        /// <returns></returns>
        protected abstract Task<FlowElementResult> OnExecuteAsync(TSArgs args, FlowState state);
        
        internal override async Task<FlowElementResult> ExecuteInternalAsync(object args, FlowState state)
        {
            try
            {
                var converter = Converters.SingleOrDefault(t => t.CanConvert(args.GetType()));
                var input = converter == null ? args : converter.Convert(args);
                return await OnExecuteAsync((TSArgs)input, state);
            }
            catch (Exception ex)
            {
                return this.Error(ex);
            }
        }
    }
}