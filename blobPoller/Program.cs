using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.FileExtensions;
using Microsoft.Extensions.Configuration.Json;

namespace blobPoller
{
    class Program
    {
        static async Task Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var blobConnectionString = config["blobConnectionString"];
            var blobContainer = config["blobContainer"];
            var lastModifiedTimeOptimizedPollingStrategy = new LastModifiedTimeOptimizedPollingStrategy(blobConnectionString, blobContainer);
            var delayedSlidingWindowsPollingStrategy = new DelayedSlidingWindowPollingStrategy(blobConnectionString, blobContainer);
            var onePlusNWindowsPollingStrategy = new OnePlusNWindowsPollingStrategy(blobConnectionString, blobContainer);
            var blobPoller = new BlobPoller(delayedSlidingWindowsPollingStrategy);

            blobPoller.Add("Product");
            var cancellationTokenSource = new CancellationTokenSource();
            var pollingTask = blobPoller.StartAsync(cancellationTokenSource.Token, TimeSpan.FromSeconds(1));
            Console.ReadLine();
            cancellationTokenSource.Cancel();
            await pollingTask;
        }
    }
}
