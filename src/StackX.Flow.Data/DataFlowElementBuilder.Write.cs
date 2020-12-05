using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.OrmLite;

namespace StackX.Flow.Data
{
    public interface IWriteQueryBuilder<TTable>
    {
        public IWriteQueryWriteActionMethodBuilder<TTable> Map(Func<object, List<TTable>> mapAction);
        public IWriteQueryWriteActionMethodBuilder<TTable> Map(Func<object, TTable> mapAction);
    }

    public interface IWriteQueryWriteActionMethodBuilder<TTable>
    {
        public IWriteQueryBuilderBuild Save();
        public IWriteQueryBuilderBuild Update();
        public IWriteQueryBuilderBuild Insert();
        public IWriteQueryBuilderBuild Decision(Func<object, TTable, WriteAction> decision);
        public IWriteQueryBuilderBuild Decision(Func<object, List<TTable>, WriteAction> decision);
    }

    public interface IWriteQueryBuilderBuild
    {
        public FlowElement Build();
    }

    public record WriteAction();

    public record WriteActionSave() : WriteAction;

    public record WriteActionInsert() : WriteAction;

    public record WriteActionUpdate() : WriteAction;

    internal record WriteActionDecisionSingle<TTable>(Func<object, TTable, WriteAction> Decision) : WriteAction;

    internal record WriteActionDecisionList<TTable>
        (Func<object, List<TTable>, WriteAction> Decision) : WriteAction;



    public class DataQueryWriteBuilder<TTable> : IWriteQueryBuilder<TTable>,
        IWriteQueryWriteActionMethodBuilder<TTable>, IWriteQueryBuilderBuild
    {
        private readonly IDbConnection? _connection;
        private object _mapAction;
        private WriteAction _writeAction;

        internal DataQueryWriteBuilder(IDbConnection? connection)
        {
            _connection = connection;
        }

        public IWriteQueryWriteActionMethodBuilder<TTable> Map(Func<object, List<TTable>> mapAction)
        {
            _mapAction = mapAction;
            return this;
        }

        public IWriteQueryWriteActionMethodBuilder<TTable> Map(Func<object, TTable> mapAction)
        {
            _mapAction = mapAction;
            return this;
        }

        public IWriteQueryBuilderBuild Save()
        {
            _writeAction = new WriteActionSave();
            return this;
        }

        public IWriteQueryBuilderBuild Update()
        {
            _writeAction = new WriteActionUpdate();
            return this;
        }

        public IWriteQueryBuilderBuild Insert()
        {
            _writeAction = new WriteActionInsert();
            return this;
        }

        public IWriteQueryBuilderBuild Decision(Func<object, TTable, WriteAction> decision)
        {
            _writeAction = new WriteActionDecisionSingle<TTable>(decision);
            return this;
        }

        public IWriteQueryBuilderBuild Decision(Func<object, List<TTable>, WriteAction> decision)
        {
            _writeAction = new WriteActionDecisionList<TTable>(decision);
            return this;
        }

        public FlowElement Build()
        {
            return new FlowElementImpl(_connection, _mapAction, _writeAction);
        }

        private class FlowElementImpl : FlowElement
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
                get { return _connection ??= HostContext.AppHost.GetDbConnection(); }
            }

            protected override async Task<FlowElementResult> OnExecuteAsync(object args, FlowState state)
            {
                var result = (_mapAction as Delegate)!.DynamicInvoke(args);

                var action = (_writeAction, result) switch
                {
                    (WriteActionDecisionSingle<TTable> single, TTable record) => single.Decision(args, record),
                    (WriteActionDecisionList<TTable> list, List<TTable> records) => list.Decision(args, records),
                    (WriteAction act, _) => act,
                };
                switch ((result, action))
                {
                    case (List<TTable> list, WriteActionInsert):
                        await Db.InsertAllAsync(list);
                        break;
                    case (List<TTable> list, WriteActionSave):
                        await Db.SaveAllAsync(list);
                        break;
                    case (List<TTable> list, WriteActionUpdate):
                        await Db.UpdateAsync(list);
                        break;
                    case (TTable table, WriteActionInsert):
                        await Db.InsertAsync(table);
                        break;
                    case (TTable table, WriteActionSave):
                        await Db.SaveAsync(table);
                        break;
                    case (TTable table, WriteActionUpdate):
                        await Db.UpdateAsync(table);
                        break;
                }

                return new FlowSuccessResult {Result = result};
            }
        }
    }
}