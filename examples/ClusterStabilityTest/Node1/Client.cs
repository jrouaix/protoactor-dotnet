using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using Process = System.Diagnostics.Process;
using ProtosReflection = Messages.ProtosReflection;

namespace TestApp
{
    public static class Client
    {
        public static void Start()
        {
            var clusterName = "cluster" + DateTime.Now.Ticks;
            var consul = StartConsulDevMode();
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Cluster.Start(clusterName, "127.0.0.1", 0, new ConsulProvider(new ConsulProviderOptions()));

            const int SERVERS_COUNT = 5;
            var serverNumber = SERVERS_COUNT;
            var servers = Enumerable.Range(0, SERVERS_COUNT).Select(i => RunServer(clusterName, i)).ToList();

            var cts = new CancellationTokenSource();
            var randy = new Random(42);
            var childrenShepherd = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    if (servers.Any(s => s.HasExited))
                    {
                        Console.WriteLine();
                        Console.WriteLine(
                            "Some servers dead : |" +
                            string.Join("|", servers.Select(s => s.HasExited ? "X" : " ")) +
                            "| (X=Dead)"
                            );
                    }

                    // Kill a node and enter a new one (same as a kubernetes update would do)
                    //var position = randy.Next(SERVERS_COUNT);
                    //var newServer = RunServer(clusterName, serverNumber++);
                    //servers[position].Kill();
                    //servers[position] = newServer;
                }
            });

            EventStream.Instance.Subscribe<ClusterTopologyEvent>(e =>
            {
                Console.Write("T");
            });

            var options = new GrainCallOptions()
            {
                RetryCount = 10,
                RetryAction = async i =>
                {
                    Console.Write("!");
                    i++;
                    await Task.Delay(i * i * 50);
                },
            };

            var tasks = new List<Task>();
            const int COUNT = 20000;
            int successes = 0;
            for (int i = 0; i < COUNT; i++)
            {
                var client = Grains.HelloGrain("name" + i % 200);
                var task = client
                    .SayHello(new HelloRequest(), CancellationToken.None, options)
                    .ContinueWith(t =>
                    {
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            Interlocked.Increment(ref successes);
                            Console.Write(".");
                        }
                        else
                        {
                            Console.Write("#");
                        }
                    });
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());

            foreach (var serv in servers) serv.Kill();
            Cluster.Shutdown();
            cts.Cancel();
            childrenShepherd.Wait();
            consul.Kill();

            Console.WriteLine();
            Console.WriteLine($"{successes} / {COUNT} succeeded.");
            Console.WriteLine("Done!");

            Console.ReadLine();
        }


        private static Process RunServer(string clusterName, int i)
        {
            var psi = new ProcessStartInfo("dotnet", "TestApp.dll " + clusterName)
            {
                UseShellExecute = true,
            };

            Console.WriteLine($"Starting server process {i}.");
            var process = Process.Start(psi);
            process.Exited += (sender, args) => { Console.Write($"Server {i} dead"); };
            return process;
        }

        private static Process StartConsulDevMode()
        {
            Console.WriteLine("Consul - Starting");
            ProcessStartInfo psi =
                new ProcessStartInfo(@"..\..\..\..\..\..\dependencies\consul",
                    "agent -server -bootstrap -data-dir /tmp/consul -bind=127.0.0.1 -ui")
                {
                    //CreateNoWindow = true, 
                    UseShellExecute = true, // better to see it, to be able to close it
                };
            var process = Process.Start(psi);
            Console.WriteLine("Consul - Started");
            return process;
        }
    }
}
