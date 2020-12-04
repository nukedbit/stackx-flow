using System;
using System.Collections;
using System.Data;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.OrmLite;

namespace StackX.Flow.Data
{
    public interface IDeleteQueryBuilder<TTable, TArgs>
    {
        public IDeleteQueryBuild<TTable, TArgs> DeleteByIds(Func<TArgs,IEnumerable> mapIds);
        public IDeleteQueryBuild<TTable, TArgs> DeleteById(Func<TArgs, object> mapId);
        public IDeleteQueryBuild<TTable, TArgs> DeleteAll();
        public IDeleteQueryBuild<TTable, TArgs> DeleteBy(Func<TArgs, Expression<Func<TTable, bool>>> expressionBuilder);
    }

    public interface IDeleteQueryBuild<TTable, TArgs>
    {
        FlowElement Build();
    }

    internal abstract record Delete;
    internal record DeleteById<TArgs>(Func<TArgs, object> MapId) : Delete;
    internal record DeleteByIds<TArgs>(Func<TArgs,IEnumerable> MapIds) : Delete;
    internal record DeleteAll : Delete;
    internal record DeleteAll<TArgs,TTable>(Func<TArgs, Expression<Func<TTable, bool>>> ExpressionBuilder) : Delete;

    internal class DeleteQueryBuilder<TTable, TArgs> : IDeleteQueryBuilder<TTable, TArgs>, IDeleteQueryBuild<TTable, TArgs>
    {
        private readonly IDbConnection? _connection;
        private Delete _delete;

        public DeleteQueryBuilder(IDbConnection? connection)
        {
            _connection = connection;
        }

        public IDeleteQueryBuild<TTable, TArgs> DeleteByIds(Func<TArgs,IEnumerable> mapIds)
        {
            _delete = new DeleteByIds<TArgs>(mapIds);
            return this;
        }

        public IDeleteQueryBuild<TTable, TArgs> DeleteById(Func<TArgs, object> mapId)
        {
            _delete = new DeleteById<TArgs>(mapId);
            return this;
        }

        public IDeleteQueryBuild<TTable, TArgs> DeleteAll()
        {
            _delete = new DeleteAll();
            return this;
        }

        public IDeleteQueryBuild<TTable, TArgs> DeleteBy(Func<TArgs, Expression<Func<TTable, bool>>> expressionBuilder)
        {
            _delete = new DeleteAll<TArgs,TTable>(expressionBuilder);
            return this;
        }

        public FlowElement Build()
        {
            return new DeleteFlowElement(_delete, _connection);
        }
        
        
        private class DeleteFlowElement : FlowElement<TArgs>
        {
            private readonly Delete _delete;
            private IDbConnection _connection;

            public DeleteFlowElement(Delete delete, IDbConnection connection)
            {
                _delete = delete;
                _connection = connection;
            }
            
            private IDbConnection Db
            {
                get
                {
                    return _connection ??= HostContext.AppHost.GetDbConnection();
                }
            }
            protected override async Task<FlowElementResult> OnExecuteAsync(TArgs args, FlowState state)
            {
                object result = _delete switch
                {
                    DeleteById<TArgs> byId => await Db.DeleteByIdAsync<TTable>(byId.MapId(args)),
                    DeleteByIds<TArgs> byIds => await Db.DeleteByIdsAsync<TTable>(byIds.MapIds(args)),
                    DeleteAll _ => await Db.DeleteAllAsync<TTable>(),
                    DeleteAll<TArgs,TTable> all => await Db.DeleteAsync(all.ExpressionBuilder(args)),
                    _ => new NotImplementedException("Delete action not implemented")
                };
                return new FlowSuccessResult
                {
                    Result = result
                };
            }
        }
    }
}