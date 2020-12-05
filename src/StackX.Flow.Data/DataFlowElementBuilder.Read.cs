using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.OrmLite;

namespace StackX.Flow.Data
{
  public interface IReadQueryBuilder<TTable>
    {
        public IReadQueryModifiers<TTable> Query(
            Func<QueryBuilderArgs<TTable>, SqlExpression<TTable>> builder);

        public IReadQueryModifiers<TTable> Query(string sql);

        public IReadQueryModifiers<TTable> Query(string sql, object anonType);
        
        public IReadQueryModifiers<TTable> Query(string sql, Dictionary<string, object> dic);
    }

    public interface IReadQueryModifiers<TTable>
    {
        public IReadQueryModifiers<TTable> OnEmptyOrNullRaiseError(string message = "no results found");
        
        public IQueryPipeBuilder List();

        public IQueryPipeBuilder Single();
    }
    
    public interface IQueryPipeBuilder
    {
        FlowElement Build();
    }

    internal class DataFlowElementBuilderRead<TTable> : DataFlowElementBuilder, IReadQueryBuilder<TTable>, IReadQueryModifiers<TTable>, IQueryPipeBuilder
    {
        private Func<QueryBuilderArgs<TTable>, SqlExpression<TTable>> _queryBuilder;
        private string? _onEmptyOrNullRaiseError = null;
        private QuerySqlSelect _sqlSelect;
        private SelectType _selectType = SelectType.List;
        
        internal DataFlowElementBuilderRead(IDbConnection? connection)
        {
            _connection = connection;
        }

        public IReadQueryModifiers<TTable> Query(Func<QueryBuilderArgs<TTable>, SqlExpression<TTable>> builder)
        {
            _queryBuilder = builder;
            return this;
        }
        
        public IReadQueryModifiers<TTable> Query(string sql)
        {
            _sqlSelect = new QuerySqlSelect(sql, null);
            return this;
        } 
        
        public IReadQueryModifiers<TTable> Query(string sql, object anonType)
        {
            _sqlSelect = new QuerySqlSelect(sql, anonType);
            return this;
        }

        public IReadQueryModifiers<TTable> Query(string sql, Dictionary<string, object> dic)
        {
            _sqlSelect = new QuerySqlSelect(sql, dic);
            return this;
        }

        public IQueryPipeBuilder List()
        {
            _selectType = SelectType.List;
            return this;
        }
        
        public IQueryPipeBuilder Single()
        {
            _selectType = SelectType.Single;
            return this;
        }
        

        public IReadQueryModifiers<TTable> OnEmptyOrNullRaiseError(string message = "no results found")
        {
            _onEmptyOrNullRaiseError = message;
            return this;
        }


        public FlowElement Build()
        {
            if (_queryBuilder is not null && _sqlSelect is not null)
            {
                throw new ArgumentException("You can't  configure both query with expression and sql");
            }
            return new DataQueryElement<TTable>(_connection, _queryBuilder, _onEmptyOrNullRaiseError, _selectType, _sqlSelect);
        }
    }


    internal class DataQueryElement<TTable> : FlowElement
    {
        private IDbConnection? _connection;
        private readonly Func<QueryBuilderArgs<TTable>, SqlExpression<TTable>>? _queryBuilder;
        private readonly string? _onEmptyOrNullRaiseError;
        private readonly SelectType _selectType;
        private readonly QuerySqlSelect? _querySqlSelect;

        internal DataQueryElement(IDbConnection? connection,
            Func<QueryBuilderArgs<TTable>, SqlExpression<TTable>>? queryBuilder, string? onEmptyOrNullRaiseError,
            SelectType selectType, QuerySqlSelect? querySqlSelect)
        {
            _connection = connection;
            _queryBuilder = queryBuilder;
            _onEmptyOrNullRaiseError = onEmptyOrNullRaiseError;
            _selectType = selectType;
            _querySqlSelect = querySqlSelect;
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
            var exp = _queryBuilder?.Invoke(new QueryBuilderArgs<TTable>(Db.From<TTable>(), args));

            object result = (exp, _querySqlSelect, _selectType) switch
            {
                //List
                (SqlExpression<TTable> expression, null, SelectType.List) => await Db.SelectAsync(expression),
                (null, {sql: string sql,anonType: null}, SelectType.List) => await Db.SelectAsync<TTable>(sql),
                (null, {sql: string sql,anonType: Dictionary<string, object> anonType}, SelectType.List) => await
                    Db.SelectAsync<TTable>(
                        sql, anonType),
                (null, {sql: string sql, anonType: object anonType}, SelectType.List) => await Db.SelectAsync<TTable>(sql,
                    anonType),
                //Single
                (SqlExpression<TTable> expression, null, SelectType.Single) => await Db.SingleAsync(expression),
                (null, {sql: string sql, anonType: null}, SelectType.Single) => await Db.SingleAsync<TTable>(sql),
                (null, {sql: string sql, anonType: Dictionary<string, object> anonType}, SelectType.Single) => await Db
                    .SingleAsync<TTable>(
                        sql, anonType),
                (null, {sql: string sql, anonType: object anonType}, SelectType.Single) => await Db.SingleAsync<TTable>(sql,
                    anonType),
                (_, _,_) => throw new ArgumentException("Action not supported")
            };

            return (result, _onEmptyOrNullRaiseError.IsNullOrEmpty()) switch
            {
                (result: IList {Count: 0}, false) => new FlowErrorResult {ErrorObject = _onEmptyOrNullRaiseError!},
                (result: object[] {Length: 0}, false) => new FlowErrorResult {ErrorObject = _onEmptyOrNullRaiseError!},
                (null, false) => new FlowErrorResult {ErrorObject = _onEmptyOrNullRaiseError!},
                _ => new FlowSuccessResult() {Result = result!}
            };
        }
    }
}