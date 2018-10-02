using System;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class FutureTests
    {
        private static readonly RootContext Context = new RootContext();

        [Fact]
        public void Given_Actor_When_AwaitRequestAsync_Should_ReturnReply()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            }));

            var reply = Context.RequestAsync<object>(pid, "hello").Result;

            Assert.Equal("hey", reply);
        }

        [Fact]
        public void Given_Actor_When_AwaitContextRequestAsync_Should_GetReply()
        {
            var pid1 = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            }));
            var pid2 = Context.Spawn(Props.FromFunc(async ctx =>
            {
                if (ctx.Message is string)
                {
                    var reply1 = await ctx.RequestAsync<string>(pid1, "");
                    ctx.Respond(ctx.Message + reply1);
                }
            }));


            var reply2 = Context.RequestAsync<string>(pid2, "hello").Result;

            Assert.Equal("hellohey", reply2);
        }

        [Fact]
        public void Given_Actor_When_ReplyIsNull_Should_Return()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond(null);
                }
                return Actor.Done;
            }));

            var reply = Context.RequestAsync<object>(pid, "hello", TimeSpan.FromSeconds(1)).Result;

            Assert.Null(reply);
        }



        [Fact]
        public async Task Test()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond(null);
                }
                return Actor.Done;
            }));

            {
                var reply = await Context.RequestAsync<object>(pid, "hello", TimeSpan.FromSeconds(1));
                Assert.Null(reply);
            }
            {
                var reply = Context.RequestAsync<object>(pid, "hello", TimeSpan.FromSeconds(1)).Result;
                Assert.Null(reply);
            }
        }

        [Fact]
        public async Task TestSpawnNamed()
        {
            var pid = Context.SpawnNamed(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond(null);
                }
                return Actor.Done;
            }), "test");

            {
                var reply = await Context.RequestAsync<object>(pid, "hello", TimeSpan.FromSeconds(1));
                Assert.Null(reply);
            }
            {
                var reply = Context.RequestAsync<object>(pid, "hello", TimeSpan.FromSeconds(1)).Result;
                Assert.Null(reply);
            }
        }

        [Fact]
        public void TestInATask() // This one will break
        {
            Task.Run(async () =>
            {
                var pid = Context.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Message is string)
                    {
                        ctx.Respond(null);
                    }
                    return Actor.Done;
                }));

                await Context.RequestAsync<object>(pid, "hello", TimeSpan.FromSeconds(1));
                Context.RequestAsync<object>(pid, "hello", TimeSpan.FromSeconds(1)).Wait();
                await Context.RequestAsync<object>(pid, "hello");
                Context.RequestAsync<object>(pid, "hello").Wait();

            }).Wait();
        }

        [Fact]
        public async Task TestAwaitTheWrongType()
        {
            Task.Run(async () =>
            {
                var pid = Context.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Message is string)
                    {
                        ctx.Respond(42);
                    }
                    return Actor.Done;
                }));

                var ex = await Assert.ThrowsAsync<AggregateException>(() => Context.RequestAsync<string>(pid, "hello"));
                Assert.Single(ex.InnerExceptions);
                Assert.Equal("Unexpected message. Was type System.Int32 but expected System.String", ex.InnerExceptions[0].Message);
            }).Wait();
        }
    }
}
