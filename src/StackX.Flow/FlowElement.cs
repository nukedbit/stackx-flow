using System;
using System.Linq;
using StackX.Flow.Converters;
using System.Threading.Tasks;

namespace StackX.Flow
{
    /// <summary>
    /// This is the base class for all pipe tasks, each one can override CanExecute, to indicated if
    /// based on the received parameters if it can execute this task.
    /// In the Execute override you can write your own task logic
    /// </summary>
    /// <typeparam name="TSArgs"></typeparam> 
    public abstract class FlowElement : IFlowElementExecute, IFlowElementCanExecute
    {
        protected virtual Converter[] Converters => new Converter[0];


        /// <summary>
        /// Determine if the task can be executed by default it's always true
        /// </summary>
        /// <param name="args">Task Argument Object</param>
        /// <param name="state">Pipe Status Object</param>
        /// <returns></returns>
        protected virtual Task<bool> CanExecuteAsync(object args, FlowState state)
        {
            return Task.FromResult(true);
        }


        public async Task<bool> CanExecuteInternalAsync(object args, FlowState state)
        {
            if (Converters.Length == 0)
                return await CanExecuteAsync(args, state);
            var converter = Converters.SingleOrDefault(t => t.CanConvert(args.GetType()));
            var input = converter == null ? args : converter.Convert(args);
            return await CanExecuteAsync(input, state);
        }

        /// <summary>
        /// Define here your own task logic 
        /// </summary>
        /// <param name="args">Task Argument</param>
        /// <param name="state">Pipe state</param>
        /// <returns></returns>
        protected abstract Task<FlowElementResult> OnExecuteAsync(object args, FlowState state);
        
        public async Task<FlowElementResult> ExecuteInternalAsync(object args, FlowState state)
        {
            try
            {
                var converter = Converters.SingleOrDefault(t => t.CanConvert(args.GetType()));
                var input = converter == null ? args : converter.Convert(args);
                return await OnExecuteAsync(input, state);
            }
            catch (Exception ex)
            {
                return this.Error(ex);
            }
        }
    }
}