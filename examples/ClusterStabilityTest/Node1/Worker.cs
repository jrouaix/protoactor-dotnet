using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace TestApp
{
    public class HelloGrain : IHelloGrain
    {
        static int count = 0;

        public Task<HelloResponse> SayHello(HelloRequest request)
        {
            Interlocked.Increment(ref count);
            Console.WriteLine("Hello request received " + new string('.', count % 10 + 1));

            return Task.FromResult(new HelloResponse
            {
                Message = ""
            });
        }
    }

    public static class Worker
    {
        public static void Start(string clusterName)
        {
            Console.WriteLine("Starting worker");
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Grains.HelloGrainFactory(() => new HelloGrain());

            Cluster.Start(clusterName, "127.0.0.1", 0, new ConsulProvider(new ConsulProviderOptions()));

            Console.ReadLine();
            Cluster.Shutdown();
        }
    }
}
