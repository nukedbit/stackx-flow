using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;
using StackX.Flow;
using StackX.Flow.Data;

namespace StackX.Pipeline.Tests
{
    public class PipeLinesTests
    {
        [Test]
        public async Task ExecutePipeSuccess()
        {
            var element = new Mock<FlowElement>();
            element
                .Protected()
                .Setup<Task<CanExecuteResult>>("OnCanExecuteAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<FlowState>())
                .ReturnsAsync(true);
            element
                .Protected()
                .Setup<Task<FlowElementResult>>("OnExecuteAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<FlowState>())
                .ReturnsAsync(new FlowSuccessResult() {Result = "res"});
            
             var builder = new FlowBuilder()
                 .Add(element.Object);

             var pipeline = builder.Build();

             var result = await pipeline.RunAsync("test");

             result.Result.Should().Be("res");
        }


        class FailingFlowElement : FlowElement
        {
            protected override Task<FlowElementResult> OnExecuteAsync(object args, FlowState state)
            {
                throw new Exception();
            }
        }
        
        [Test]
        public async Task ExecutePipeFailShouldReturnError()
        {
            var builder = new FlowBuilder()
                .Add(new FailingFlowElement());

            var pipeline = builder.Build();

            var result = await pipeline.RunAsync("test");

            result
                .Should()
                .BeOfType<FlowErrorResult>();
        }

        class Person
        {
            [AutoIncrement]
            public int Id { get; set; }
            public string Name { get; set; }
        }
        
        [Test]
        public async Task ExecuteQueryTaskReturnOneResult()
        {
            var factory = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);

            var db = await factory.OpenDbConnectionAsync();
            db.CreateTable<Person>();
            db.Save(new Person()
            {
                Name = "Mario"
            });

            db.Save(new Person()
            {
                Name = "Princess"
            });
            
            db.Save(new Person()
            {
                Name = "Luigi"
            });
            
            var pipeline = new FlowBuilder()
                .Add(
                    DataFlowElementBuilder.New()
                        .SetConnection(db)
                        .Read<Person>()
                        .Query(args => args.Expression.Where(p => p.Id == (int)args.PipeArgs))
                        .List()
                        .Build()
                ).Build();

            var result = await pipeline.RunAsync(2);

            result.Should()
                .BeOfType<FlowSuccessResult>();
            result.Result.Should()
                .BeOfType<List<Person>>()
                .Which.Single()
                .Id.Should().Be(2);
        }
        
        [Test]
        public async Task ExecuteQueryTaskOnEmptyResultReturnError()
        {
            var factory = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);

            var db = await factory.OpenDbConnectionAsync();
            db.CreateTable<Person>();
            
            var pipeline = new FlowBuilder()
                .Add(
                    DataFlowElementBuilder.New()
                        .SetConnection(db)
                        .Read<Person>()
                        .Query(args => args.Expression.Where(p => p.Id == (int)args.PipeArgs))
                        .OnEmptyOrNullRaiseError()
                        .List()
                        .Build()
                ).Build();

            var result = await pipeline.RunAsync(2);

            result.Should()
                .BeOfType<FlowErrorResult>()
                .Which.ErrorObject.Should().Be("no results found");
        }
        
        
        [Test]
        public async Task ExecuteQueryAsSingleReturnOneResult()
        {
            var factory = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);

            var db = await factory.OpenDbConnectionAsync();
            db.CreateTable<Person>();
            db.Save(new Person()
            {
                Name = "Mario"
            });

            db.Save(new Person()
            {
                Name = "Princess"
            });
            
            db.Save(new Person()
            {
                Name = "Luigi"
            });
            
            var pipeline = new FlowBuilder()
                .Add(
                    DataFlowElementBuilder.New()
                        .SetConnection(db)
                        .Read<Person>()
                        .Query(args => args.Expression.Limit(1))
                        .Single()
                        .Build()
                ).Build();

            var result = await pipeline.RunAsync(2);

            result.Should()
                .BeOfType<FlowSuccessResult>();
            result.Result.Should()
                .BeOfType<Person>();
        }


        class FakeElementReturnA : FlowElement
        {
            protected override async Task<FlowElementResult> OnExecuteAsync(object args, FlowState state)
            {
                return this.Success("A");
            }
        }
        
        class FakeElementReturnB : FlowElement
        {
            protected override async Task<FlowElementResult> OnExecuteAsync(object args, FlowState state)
            {
                return this.Success("B");
            }
        }
        
        
        [Test]
        public async Task FlowShouldReturnA()
        {
            var builder = new FlowBuilder()
                .Add(DecisionBuilder.New().Decision(async value => (int)value == 1)
                    .SetBranches(
                        new List<IFlowElementExecute> {new FakeElementReturnA()},
                        new List<IFlowElementExecute> {new FakeElementReturnB()}
                    ).Build());

            var pipeline = builder.Build();

            var result = await pipeline.RunAsync(1);

            result
                .Should()
                .BeOfType<FlowSuccessResult>()
                .Which.Result.Should().Be("A");
        }
        
        [Test]
        public async Task FlowThrowAddingElementAfterDecisionNew()
        {
            Assert.Throws<ArgumentException>(() => new FlowBuilder()
                .Add(DecisionBuilder.New().Decision(async value => (int)value == 1)
                    .SetBranches(
                        new List<IFlowElementExecute>(),
                        new List<IFlowElementExecute>()).Build()
                )
                .Add(new FakeElementReturnA())
            ).Message.Should().Be("You can't add another element after a Decision");
        }
        
        [Test]
        public async Task FlowThrowAddingElementAfterDecisionCreateInstance()
        {
            Assert.Throws<ArgumentException>(() => new FlowBuilder()
                .Add(DecisionBuilder.New().Decision(async value => (int)value == 1)
                    .SetBranches(
                        new List<IFlowElementExecute>(),
                        new List<IFlowElementExecute>()).Build()
                )
                .Add<FakeElementReturnA>()
            ).Message.Should().Be("You can't add another element after a Decision");
        }


        [Test]
        public async Task FlowDeleteById()
        {
            var factory = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);

            var db = await factory.OpenDbConnectionAsync();
            
            db.CreateTable<Person>();
            db.Save(new Person()
            {
                Name = "Mario"
            });
            
            var flow = new FlowBuilder()
                .Add(
                    DataFlowElementBuilder.New()
                        .SetConnection(db)
                        .Delete<Person>()
                        .DeleteById(i => i)
                        .Build()
                ).Build();

            await flow.RunAsync(1);

            var persons = await db.SelectAsync<Person>();
            persons.Should().BeEmpty();
        }
        
        [Test]
        public async Task FlowDeleteByIds()
        {
            var factory = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);

            var db = await factory.OpenDbConnectionAsync();
            db.CreateTable<Person>();
            await db.SaveAsync(new Person()
            {
                Name = "Mario"
            });
            
            await db.SaveAsync(new Person()
            {
                Name = "Luigi"
            });
            
            await db.SaveAsync(new Person()
            {
                Name = "Highlander"
            });
            
            var flow = new FlowBuilder()
                .Add(
                    DataFlowElementBuilder.New()
                        .SetConnection(db)
                        .Delete<Person>()
                        .DeleteByIds(i => (int[])i)
                        .Build()
                ).Build();

            await flow.RunAsync(new[] {1, 2});

            var persons = await db.SelectAsync<Person>();
            persons.Single()
                .Name
                .Should()
                .Be("Highlander");
        }
        
        
        [Test]
        public async Task FlowDeleteAll()
        {
            var factory = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);

            var db = await factory.OpenDbConnectionAsync();
            db.CreateTable<Person>();
            await db.SaveAsync(new Person()
            {
                Name = "Mario"
            });
            
            await db.SaveAsync(new Person()
            {
                Name = "Luigi"
            });
            
            await db.SaveAsync(new Person()
            {
                Name = "Highlander"
            });
            
            var flow = new FlowBuilder()
                .Add(
                    DataFlowElementBuilder.New()
                        .SetConnection(db)
                        .Delete<Person>()
                        .DeleteAll()
                        .Build()
                ).Build();

            await flow.RunAsync("anything");

            var persons = await db.SelectAsync<Person>();
            persons
                .Should()
                .BeEmpty();

        }
        
        [Test]
        public async Task FlowDeleteByExpression()
        {
            var factory = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);

            var db = await factory.OpenDbConnectionAsync();
            db.CreateTable<Person>();
            await db.SaveAsync(new Person()
            {
                Name = "Mario"
            });
            
            await db.SaveAsync(new Person()
            {
                Name = "Luigi"
            });
            
            await db.SaveAsync(new Person()
            {
                Name = "Highlander"
            });

            var flow = new FlowBuilder()
                .Add(
                    DataFlowElementBuilder.New()
                        .SetConnection(db)
                        .Delete<Person>()
                        .DeleteBy(ints => person => ((int[])ints).Contains(person.Id))
                        .Build()
                ).Build();

            await flow.RunAsync(new[] {1, 2});

            var persons = await db.SelectAsync<Person>();
            persons
                .Single()
                .Id
                .Should()
                .Be(3);

        }


        [Test]
        public async Task SimpleFlowElementBuilderExecute()
        {
            var flow = new FlowBuilder()
                .Add(
                    FlowElementBuilder
                        .New()
                        .CanExecuteContinue()
                        .OnExecute(async (i, _) =>  (int)i * 2)
                        .Build()
                ).Build();

            var flowResult = await flow.RunAsync(4);

            flowResult
                .Should()
                .BeOfType<FlowSuccessResult>()
                .Which.Result
                .Should().Be(8);
        }
        
        [Test]
        public async Task SimpleFlowElementCanExecuteNo()
        {
            var flow = new FlowBuilder()
                .Add(
                    FlowElementBuilder
                        .New()
                        .CanExecute(async (i, _) => (int)i == 42)
                        .OnExecute(async (i, _) =>  (int)i * 2)
                        .Build()
                ).Build();

            var flowResult = await flow.RunAsync(4);

            flowResult
                .Should()
                .BeOfType<FlowSuccessResult>()
                .Which.Result
                .Should().Be(4);
        }
        
        [Test]
        public async Task SimpleFlowElementCanExecuteSkip()
        {
            var flow = new FlowBuilder()
                .Add(
                    FlowElementBuilder
                        .New()
                        .CanExecute(async (i, _) => CanExecuteResult.Skip)
                        .OnExecute(async (i, _) =>  (int)i * 2)
                        .Build()
                ).Build();

            var flowResult = await flow.RunAsync(4);

            flowResult
                .Should()
                .BeOfType<FlowSuccessResult>()
                .Which.Result
                .Should().Be(4);
        }
        
        [Test]
        public async Task SimpleFlowElementCanExecuteError()
        {
            var flow = new FlowBuilder()
                .Add(
                    FlowElementBuilder
                        .New()
                        .CanExecute(async (i, _) => CanExecuteResult.Error("error"))
                        .OnExecute(async (i, _) =>  throw new Exception("Should not execute this"))
                        .Build()
                ).Build();

            var flowResult = await flow.RunAsync(4);

            flowResult
                .Should()
                .BeOfType<FlowErrorResult>()
                .Which.ErrorObject
                .Should().Be("error");
        }
        
        [Test]
        public async Task SimpleFlowElementCanExecuteErrorShouldNotExecuteSecondStep()
        {
            var flow = new FlowBuilder()
                .Add(
                    FlowElementBuilder
                        .New()
                        .CanExecute(async (i, _) => CanExecuteResult.Error("error"))
                        .OnExecute(async (i, _) => throw new Exception("Should not execute step 1"))
                        .Build()
                    ,
                    FlowElementBuilder
                        .New()
                        .CanExecute(async (i, _) => CanExecuteResult.Continue)
                        .OnExecute(async (i, _) => throw new Exception("Should not execute step 2"))
                        .Build()
                )
                .Build();

            var flowResult = await flow.RunAsync(4);

            flowResult
                .Should()
                .BeOfType<FlowErrorResult>()
                .Which.ErrorObject
                .Should().Be("error");
        }
        
        
        [Test]
        public async Task SimpleFlowElementMap()
        {
            var flow = new FlowBuilder()
                .Add(
                    FlowElementBuilder
                        .New()
                        .CanExecuteContinue()
                        .OnExecute(async (i, _) => (int)i * 2)
                        .Build()
                        .Map(async (result, _, _) => (int) result.Result * 2)
                )
                .Build();

            var flowResult = await flow.RunAsync(4);

            flowResult
                .Should()
                .BeOfType<FlowSuccessResult>()
                .Which.Result
                .Should()
                .Be(16);
        }
        
        [Test]
        public async Task SimpleFlowElementMapOnSuccess()
        {
            var flow = new FlowBuilder()
                .Add(
                    FlowElementBuilder
                        .New()
                        .CanExecuteContinue()
                        .OnExecute(async (i, _) => (int)i * 2)
                        .Build()
                        .MapOnSuccess(async (result, _, _) => (int) result.Result * 2)
                )
                .Build();

            var flowResult = await flow.RunAsync(4);

            flowResult
                .Should()
                .BeOfType<FlowSuccessResult>()
                .Which.Result
                .Should()
                .Be(16);
        }
        
        [Test]
        public async Task SimpleFlowElementMapOnError()
        {
            var flow = new FlowBuilder()
                .Add(
                    FlowElementBuilder
                        .New()
                        .CanExecuteContinue()
                        .OnExecute(async (_, _) => new FlowErrorResult() { ErrorObject = "failed"})
                        .Build()
                        .MapOnError(async (_, _, _) => 42)
                )
                .Build();

            var flowResult = await flow.RunAsync(4);

            flowResult
                .Should()
                .BeOfType<FlowSuccessResult>()
                .Which.Result
                .Should()
                .Be(42);
        }
        
        
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public async Task ExecuteCustomDbStep(int id)
        {
            var factory = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);

            var db = await factory.OpenDbConnectionAsync();
            db.CreateTable<Person>();
            db.Save(new Person
            {
                Name = "Mario"
            });

            db.Save(new Person
            {
                Name = "Princess"
            });
            
            db.Save(new Person
            {
                Name = "Luigi"
            });
            
            var pipeline = new FlowBuilder()
                .Add(
                    DataFlowElementBuilder.New()
                        .SetConnection(db)
                        .Custom()
                        .CanExecuteContinue()
                        .OnExecute(async args => await args.Db.SingleAsync<Person>(p => p.Id == (int)args.args))
                        .Build()
                ).Build();

            var result = await pipeline.RunAsync(id);

            result.Should()
                .BeOfType<FlowSuccessResult>()
                .Which.Result
                .Should()
                .BeOfType<Person>()
                .Which.Id
                .Should()
                .Be(id);
        }
    }
}