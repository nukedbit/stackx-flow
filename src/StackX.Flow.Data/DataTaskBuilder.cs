using ServiceStack;
using ServiceStack.OrmLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace StackX.Flow.Data
{
    public record QueryBuilderArgs<TQuery, TArgs>(SqlExpression<TQuery> Expression, TArgs PipeArgs);

    internal record QuerySqlSelect(string sql, object? anonType);

    internal enum SelectType
    {
        Single,
        List
    }
    
    public class DataTaskBuilder
    {
        protected IDbConnection? _connection;
        
        public DataTaskBuilder SetConnection(IDbConnection connection)
        {
            _connection = connection;
            return this;
        }

        public IReadQueryBuilder<TTable,TArgs> Read<TTable,TArgs>()
        {
            return new DataTaskBuilderRead<TTable,TArgs>(_connection);
        }
        
        public IWriteQueryBuilder<TTable,TArgs> Write<TTable,TArgs>()
        {
            return new DataQueryWriteBuilder<TTable,TArgs>(_connection);
        }
        
        internal DataTaskBuilder () {}

        public static DataTaskBuilder New() => new();

    }

    public interface IReadQueryBuilder<TTable, TArgs>
    {
        public IReadQueryModifiers<TTable, TArgs> Query(
            Func<QueryBuilderArgs<TTable, TArgs>, SqlExpression<TTable>> builder);

        public IReadQueryModifiers<TTable, TArgs> Query(string sql);

        public IReadQueryModifiers<TTable, TArgs> Query(string sql, object anonType);
        
        public IReadQueryModifiers<TTable, TArgs> Query(string sql, Dictionary<string, object> dic);
    }

    public interface IReadQueryModifiers<TTable, TArgs>
    {
        public IReadQueryModifiers<TTable, TArgs> OnEmptyOrNullRaiseError(string message = "no results found");
        
        public IQueryPipeBuilder List();

        public IQueryPipeBuilder Single();
    }
    
    public interface IQueryPipeBuilder
    {
        FlowElement Build();
    }

    internal class DataTaskBuilderRead<TTable,TArgs> : DataTaskBuilder, IReadQueryBuilder<TTable, TArgs>, IReadQueryModifiers<TTable, TArgs>, IQueryPipeBuilder
    {
        private Func<QueryBuilderArgs<TTable,TArgs>, SqlExpression<TTable>> _queryBuilder;
        private string? _onEmptyOrNullRaiseError = null;
        private QuerySqlSelect _sqlSelect;
        private SelectType _selectType = SelectType.List;
        
        internal DataTaskBuilderRead(IDbConnection? connection)
        {
            _connection = connection;
        }

        public IReadQueryModifiers<TTable,TArgs> Query(Func<QueryBuilderArgs<TTable,TArgs>, SqlExpression<TTable>> builder)
        {
            _queryBuilder = builder;
            return this;
        }
        
        public IReadQueryModifiers<TTable,TArgs> Query(string sql)
        {
            _sqlSelect = new QuerySqlSelect(sql, null);
            return this;
        } 
        
        public IReadQueryModifiers<TTable,TArgs> Query(string sql, object anonType)
        {
            _sqlSelect = new QuerySqlSelect(sql, anonType);
            return this;
        }

        public IReadQueryModifiers<TTable, TArgs> Query(string sql, Dictionary<string, object> dic)
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
        

        public IReadQueryModifiers<TTable, TArgs> OnEmptyOrNullRaiseError(string message = "no results found")
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
            return new DataQueryElement<TTable, TArgs>(_connection, _queryBuilder, _onEmptyOrNullRaiseError, _selectType, _sqlSelect);
        }
    }


    internal class DataQueryElement<TTable, TArgs> : FlowElement<TArgs>
    {
        private IDbConnection? _connection;
        private readonly Func<QueryBuilderArgs<TTable, TArgs>, SqlExpression<TTable>>? _queryBuilder;
        private readonly string? _onEmptyOrNullRaiseError;
        private readonly SelectType _selectType;
        private readonly QuerySqlSelect? _querySqlSelect;

        internal DataQueryElement(IDbConnection? connection,
            Func<QueryBuilderArgs<TTable, TArgs>, SqlExpression<TTable>>? queryBuilder, string? onEmptyOrNullRaiseError,
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
        
        protected override async Task<FlowElementResult> OnExecuteAsync(TArgs args, FlowState state)
        {
            var exp = _queryBuilder?.Invoke(new QueryBuilderArgs<TTable, TArgs>(Db.From<TTable>(), args));

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
                (result: TArgs[] {Length: 0}, false) => new FlowErrorResult {ErrorObject = _onEmptyOrNullRaiseError!},
                (null, false) => new FlowErrorResult {ErrorObject = _onEmptyOrNullRaiseError!},
                _ => new FlowSuccessResult() {Result = result!}
            };
        }
    }


    public enum SaveAction
    {
        Save,
        Update,
        Insert
    }
    
    public interface IWriteQueryBuilder<TTable, TArgs>
    {
        public IWriteQueryWriteActionMethodBuilder<TTable, TArgs> Map(Func<TArgs, List<TTable>> mapAction);
        public IWriteQueryWriteActionMethodBuilder<TTable, TArgs> Map(Func<TArgs, TTable> mapAction);
    }

    public interface IWriteQueryWriteActionMethodBuilder<TTable, TArgs>
    {
        public IWriteQueryBuilderBuild Save();
        public IWriteQueryBuilderBuild Update();
        public IWriteQueryBuilderBuild Insert();
        public IWriteQueryBuilderBuild Decision(Func<TArgs, TTable, SaveAction> decision);
        public IWriteQueryBuilderBuild Decision(Func<TArgs, List<TTable>, SaveAction> decision);
    }

    public interface IWriteQueryBuilderBuild
    {
        public FlowElement Build();
    }
    
    public class DataQueryWriteBuilder<TTable, TArgs> : IWriteQueryBuilder<TTable, TArgs>, 
        IWriteQueryWriteActionMethodBuilder<TTable, TArgs>, IWriteQueryBuilderBuild
    {
        private readonly IDbConnection? _connection;
        private object _mapAction;
        private object _writeAction;
        
        internal DataQueryWriteBuilder(IDbConnection? connection) {
            _connection = connection;
        }

        public IWriteQueryWriteActionMethodBuilder<TTable, TArgs> Map(Func<TArgs, List<TTable>> mapAction)
        {
            this._mapAction = mapAction;
            return this;
        }

        public IWriteQueryWriteActionMethodBuilder<TTable, TArgs> Map(Func<TArgs, TTable> mapAction)
        {
            this._mapAction = mapAction;
            return this;
        }

        public IWriteQueryBuilderBuild Save()
        {
            _writeAction = SaveAction.Save;
            return this;
        }

        public IWriteQueryBuilderBuild Update()
        {
            _writeAction = SaveAction.Update;
            return this;
        }

        public IWriteQueryBuilderBuild Insert()
        {
            _writeAction = SaveAction.Insert;
            return this;
        }

        public IWriteQueryBuilderBuild Decision(Func<TArgs, TTable, SaveAction> decision)
        {
            _writeAction = decision;
            return this;
        }
        
        public IWriteQueryBuilderBuild Decision(Func<TArgs, List<TTable>, SaveAction> decision)
        {
            _writeAction = decision;
            return this;
        }

        public FlowElement Build()
        {
            return new FlowElementImpl(_connection,_mapAction, _writeAction);
        }
        
        private class FlowElementImpl: FlowElement<TArgs>
        {
            private IDbConnection? _connection;
            private readonly object _mapAction;
            private readonly object _writeAction;

            public FlowElementImpl(IDbConnection? connection, object mapAction, object writeAction)
            {
                _connection = connection;
                _mapAction = mapAction;
                _writeAction = writeAction;
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
                var result = (_mapAction as Delegate)!.DynamicInvoke(args);
                
                var action = (_writeAction, result) switch
                {
                    (SaveAction act, _) => act,
                    (Func<TArgs, TTable, SaveAction> decision, TTable record) => decision(args, record),
                    (Func<TArgs, List<TTable>, SaveAction> decision, List<TTable> records) => decision(args, records),
                    (_, _) => throw new ArgumentException("Invalid write action")
                };

                switch ((result, action))
                {
                    case (List<TTable> list, SaveAction.Insert):
                        await Db.InsertAllAsync(list);
                        break;
                    case (List<TTable> list, SaveAction.Save):
                        await Db.SaveAllAsync(list);
                        break;
                    case (List<TTable> list, SaveAction.Update):
                        await Db.UpdateAsync(list);
                        break;
                    case (TTable table, SaveAction.Insert):
                        await Db.InsertAsync(table);
                        break;
                    case (TTable table, SaveAction.Save):
                        await Db.SaveAsync(table);
                        break;
                    case (TTable table, SaveAction.Update):
                        await Db.UpdateAsync(table);
                        break;
                }

                return new FlowSuccessResult {Result = result};
            }
        }
    }
}