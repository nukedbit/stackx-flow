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
            var element = new Mock<FlowElement<string>>();
            element
                .Protected()
                .Setup<Task<bool>>("CanExecuteAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<FlowState>())
                .ReturnsAsync(true);
            element
                .Protected()
                .Setup<Task<FlowElementResult>>("OnExecuteAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<FlowState>())
                .ReturnsAsync(new FlowSuccessResult() {Result = "res"});
            
             var builder = new FlowBuilder()
                 .Add(element.Object);

             var pipeline = builder.Build<string>();

             var result = await pipeline.RunAsync("test");

             result.Result.Should().Be("res");
        }


        class FailingFlowElement : FlowElement<string>
        {
            protected override Task<FlowElementResult> OnExecuteAsync(string args, FlowState state)
            {
                throw new Exception();
            }
        }
        
        [Test]
        public async Task ExecutePipeFailShouldReturnError()
        {
            var builder = new FlowBuilder()
                .Add(new FailingFlowElement());

            var pipeline = builder.Build<string>();

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
                        .Read<Person, int>()
                        .Query(args => args.Expression.Where(p => p.Id == args.PipeArgs))
                        .List()
                        .Build()
                ).Build<int>();

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
                        .Read<Person, int>()
                        .Query(args => args.Expression.Where(p => p.Id == args.PipeArgs))
                        .OnEmptyOrNullRaiseError()
                        .List()
                        .Build()
                ).Build<int>();

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
                        .Read<Person, int>()
                        .Query(args => args.Expression.Limit(1))
                        .Single()
                        .Build()
                ).Build<int>();

            var result = await pipeline.RunAsync(2);

            result.Should()
                .BeOfType<FlowSuccessResult>();
            result.Result.Should()
                .BeOfType<Person>();
        }


        class FakeElementReturnA : FlowElement<int>
        {
            protected override async Task<FlowElementResult> OnExecuteAsync(int args, FlowState state)
            {
                return this.Success("A");
            }
        }
        
        class FakeElementReturnB : FlowElement<int>
        {
            protected override async Task<FlowElementResult> OnExecuteAsync(int args, FlowState state)
            {
                return this.Success("B");
            }
        }
        
        
        [Test]
        public async Task FlowShouldReturnA()
        {
            var builder = new FlowBuilder()
                .Add(DecisionBuilder.New<int>().Decision(async value => value == 1)
                    .SetBranches(
                        new List<FlowElement> {new FakeElementReturnA()},
                        new List<FlowElement> {new FakeElementReturnB()}
                    ).Build());

            var pipeline = builder.Build<int>();

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
                .Add(DecisionBuilder.New<int>().Decision(async value => value == 1)
                    .SetBranches(
                        new List<FlowElement>(),
                        new List<FlowElement>()).Build()
                )
                .Add(new FakeElementReturnA())
            ).Message.Should().Be("You can't add another element after a Decision");
        }
        
        [Test]
        public async Task FlowThrowAddingElementAfterDecisionCreateInstance()
        {
            Assert.Throws<ArgumentException>(() => new FlowBuilder()
                .Add(DecisionBuilder.New<int>().Decision(async value => value == 1)
                    .SetBranches(
                        new List<FlowElement>(),
                        new List<FlowElement>()).Build()
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
                        .Delete<Person, int>()
                        .DeleteById(i => i)
                        .Build()
                ).Build<int>();

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
                        .Delete<Person, int[]>()
                        .DeleteByIds(i => i)
                        .Build()
                ).Build<int[]>();

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
                        .Delete<Person, object>()
                        .DeleteAll()
                        .Build()
                ).Build<object>();

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
                        .Delete<Person, int[]>()
                        .DeleteBy(ints => person => ints.Contains(person.Id))
                        .Build()
                ).Build<int[]>();

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
                        .New<int>()
                        .Yes()
                        .OnExecute(async (i, state) =>  i * 2)
                        .Build()
                ).Build<int>();

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
                        .New<int>()
                        .CanExecute(async (i, _) => i == 42)
                        .OnExecute(async (i, _) =>  i * 2)
                        .Build()
                ).Build<int>();

            var flowResult = await flow.RunAsync(4);

            flowResult
                .Should()
                .BeOfType<FlowSuccessResult>()
                .Which.Result
                .Should().Be(4);
        }
    }
}