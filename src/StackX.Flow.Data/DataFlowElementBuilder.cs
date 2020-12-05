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
    
    public class DataFlowElementBuilder
    {
        protected IDbConnection? _connection;
        
        public DataFlowElementBuilder SetConnection(IDbConnection connection)
        {
            _connection = connection;
            return this;
        }

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