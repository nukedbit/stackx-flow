using ServiceStack;
using ServiceStack.OrmLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace StackX.Flow.Data
{
    public record QueryBuilderArgs<TQuery>(SqlExpression<TQuery> Expression, object PipeArgs);

    internal record QuerySqlSelect(string sql, object? anonType);

    internal enum SelectType
    {
        Single,
        List
    }


    public record OrmLiteFlowElementArgs(IDbConnection Db, object args, FlowState State);
    
    internal class OrmLiteFlowElement : FlowElement
    {
        private readonly Func<OrmLiteFlowElementArgs, Task<object>> _onExecute;
        private readonly Func<OrmLiteFlowElementArgs, Task<CanExecuteResult>> _onCanExecute;
        private readonly IDbConnection? _connection;

        public OrmLiteFlowElement(Func<OrmLiteFlowElementArgs,Task<object>> onExecute, Func<OrmLiteFlowElementArgs, Task<CanExecuteResult>> onCanExecute, IDbConnection? connection)
        {
            _onExecute = onExecute;
            _onCanExecute = onCanExecute;
            _connection = connection;
        }
        protected override async Task<FlowElementResult> OnExecuteAsync(object args, FlowState state)
        {
            var db = _connection ?? HostContext.AppHost.GetDbConnection();
            var result = await _onExecute(new OrmLiteFlowElementArgs(db, args, state));
            if (result is FlowElementResult flowElementResult)
            {
                return flowElementResult;
            }

            return new FlowSuccessResult()
            {
                Result = result
            };
        }
    }

    public interface IDbStepBuilderCanExecute
    {
        public IDbStepBuilderExecute CanExecuteContinue();
        
        public IDbStepBuilderExecute CanExecute(Func<OrmLiteFlowElementArgs, Task<CanExecuteResult>> canExecute);
    }

    public interface IDbStepBuilderExecute
    {
        IDbStepBuilderBuild OnExecute(Func<OrmLiteFlowElementArgs, Task<object>> onExecute);
    }
    
    public interface IDbStepBuilderBuild
    {
        IFlowElementExecute Build();
    }

    class DbStepBuilderImpl : IDbStepBuilderCanExecute, IDbStepBuilderExecute, IDbStepBuilderBuild
    {
        private readonly IDbConnection? _connection;

        public DbStepBuilderImpl(IDbConnection? connection)
        {
            _connection = connection;
        }
        
        private Func<OrmLiteFlowElementArgs, Task<CanExecuteResult>> _canExecute;
        private Func<OrmLiteFlowElementArgs, Task<object>> _onExecute;
        
        public IDbStepBuilderExecute CanExecuteContinue()
        {
            _canExecute = o => Task.FromResult(CanExecuteResult.Continue);
            return this;
        }

        public IDbStepBuilderExecute CanExecute(Func<OrmLiteFlowElementArgs, Task<CanExecuteResult>> canExecute)
        {
            _canExecute = canExecute;
            return this;
        }

        public IDbStepBuilderBuild OnExecute(Func<OrmLiteFlowElementArgs, Task<object>> onExecute)
        {
            _onExecute = onExecute;
            return this;
        }
        
        public IFlowElementExecute Build()
        {
            return new OrmLiteFlowElement(_onExecute, _canExecute, _connection);
        }
        
    }

    public class DataFlowElementBuilder
    {
        protected IDbConnection? _connection;
        
        public DataFlowElementBuilder SetConnection(IDbConnection connection)
        {
            _connection = connection;
            return this;
        }

        public IDbStepBuilderCanExecute Custom() => new DbStepBuilderImpl(_connection);

        public IReadQueryBuilder<TTable> Read<TTable>()
        {
            return new DataFlowElementBuilderRead<TTable>(_connection);
        }
        
        public IWriteQueryBuilder<TTable> Write<TTable>()
        {
            return new DataQueryWriteBuilder<TTable>(_connection);
        }

        public IDeleteQueryBuilder<TTable> Delete<TTable>()
        {
            return new DeleteQueryBuilder<TTable>(_connection);
        }
        
        internal DataFlowElementBuilder () {}

        public static DataFlowElementBuilder New() => new();

    }
}