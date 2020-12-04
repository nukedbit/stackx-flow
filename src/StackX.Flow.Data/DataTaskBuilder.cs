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

        public IDeleteQueryBuilder<TTable, TArgs> Delete<TTable, TArgs>()
        {
            return new DeleteQueryBuilder<TTable, TArgs>(_connection);
        }
        
        internal DataTaskBuilder () {}

        public static DataTaskBuilder New() => new();

    }
}