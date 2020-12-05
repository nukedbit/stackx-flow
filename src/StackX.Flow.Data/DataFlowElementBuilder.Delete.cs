using System;
using System.Collections;
using System.Data;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.OrmLite;

namespace StackX.Flow.Data
{
    public interface IDeleteQueryBuilder<TTable>
    {
        public IDeleteQueryBuild<TTable> DeleteByIds(Func<object,IEnumerable> mapIds);
        public IDeleteQueryBuild<TTable> DeleteById(Func<object, object> mapId);
        public IDeleteQueryBuild<TTable> DeleteAll();
        public IDeleteQueryBuild<TTable> DeleteBy(Func<object, Expression<Func<TTable, bool>>> expressionBuilder);
    }

    public interface IDeleteQueryBuild<TTable>
    {
        FlowElement Build();
    }

    internal abstract record Delete;
    internal record DeleteById(Func<object, object> MapId) : Delete;
    internal record DeleteByIds(Func<object,IEnumerable> MapIds) : Delete;
    internal record DeleteAll : Delete;
    internal record DeleteAll<TTable>(Func<object, Expression<Func<TTable, bool>>> ExpressionBuilder) : Delete;

    internal class DeleteQueryBuilder<TTable> : IDeleteQueryBuilder<TTable>, IDeleteQueryBuild<TTable>
    {
        private readonly IDbConnection? _connection;
        private Delete _delete;

        public DeleteQueryBuilder(IDbConnection? connection)
        {
            _connection = connection;
        }

        public IDeleteQueryBuild<TTable> DeleteByIds(Func<object,IEnumerable> mapIds)
        {
            _delete = new DeleteByIds(mapIds);
            return this;
        }

        public IDeleteQueryBuild<TTable> DeleteById(Func<object, object> mapId)
        {
            _delete = new DeleteById(mapId);
            return this;
        }

        public IDeleteQueryBuild<TTable> DeleteAll()
        {
            _delete = new DeleteAll();
            return this;
        }

        public IDeleteQueryBuild<TTable> DeleteBy(Func<object, Expression<Func<TTable, bool>>> expressionBuilder)
        {
            _delete = new DeleteAll<TTable>(expressionBuilder);
            return this;
        }

        public FlowElement Build()
        {
            return new DeleteFlowElement(_delete, _connection);
        }
        
        
        private class DeleteFlowElement : FlowElement
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

            protected override async Task<FlowElementResult> OnExecuteAsync(object args, FlowState state)
            {
                object result = _delete switch
                {
                    DeleteById byId => await Db.DeleteByIdAsync<TTable>(byId.MapId(args)),
                    DeleteByIds byIds => await Db.DeleteByIdsAsync<TTable>(byIds.MapIds(args)),
                    DeleteAll _ => await Db.DeleteAllAsync<TTable>(),
                    DeleteAll<TTable> all => await Db.DeleteAsync(all.ExpressionBuilder(args)),
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