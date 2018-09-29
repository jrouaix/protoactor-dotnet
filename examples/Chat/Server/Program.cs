using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using chat.messages;
using Jaeger;
using Jaeger.Samplers;
using OpenTracing.Util;
using Proto;
using Proto.OpenTracing;
using Proto.Remote;

class Program
{
    static void Main(string[] args)
    {
        var tracer = new Tracer.Builder("Proto.Chat.Server")
            .WithSampler(new ConstSampler(true))
            .Build();
        GlobalTracer.Register(tracer);

        SpanSetup spanSetup = (span, message) => span.Log(message?.ToString());

        var context = new RootContext();
        Serialization.RegisterFileDescriptor(ChatReflection.Descriptor);
        Remote.Start("127.0.0.1", 8000);

        const string SERVER_NAME = "SERVER_NAME";

        var clients = new HashSet<PID>();
        var props = Props.FromFunc(ctx =>
        {
            switch (ctx.Message)
            {
                case Connect connect:
                    Console.WriteLine($"Client {connect.Sender} connected");
                    clients.Add(connect.Sender);
                    ctx.Send(connect.Sender, new Connected { Message = "Welcome!" });
                    break;

                case SayRequest check when check.UserName == SERVER_NAME:
                    ctx.Respond(new SayResponse());
                    break;

                case SayRequest sayRequest:
                    foreach (var client in clients)
                    {
                        ctx.Send(client, new SayResponse
                        {
                            UserName = sayRequest.UserName,
                            Message = sayRequest.Message
                        });
                    }
                    break;

                case NickRequest nickRequest:
                    foreach (var client in clients)
                    {
                        ctx.Send(client, new NickResponse
                        {
                            OldUserName = nickRequest.OldUserName,
                            NewUserName = nickRequest.NewUserName
                        });
                    }
                    break;
            }
            return Actor.Done;
        })
        .WithOpenTracing(spanSetup, spanSetup);

        var serverPid = context.SpawnNamed(props, "chatserver");

        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await context.RequestAsync<SayResponse>(serverPid, new SayRequest { UserName = SERVER_NAME }, TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    context.RequestAsync<SayResponse>(serverPid, new SayRequest { UserName = SERVER_NAME }, TimeSpan.FromSeconds(1)).Wait();
                    Console.WriteLine("Server auto check OK.");
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
            }
        });

        Console.WriteLine("Press [Enter] to leave Server");
        Console.ReadLine();
    }
}