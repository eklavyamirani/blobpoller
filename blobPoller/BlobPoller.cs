using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace blobPoller
{
    internal class BlobPoller
    {
        private ConcurrentDictionary<string, DateTimeOffset> lastModifiedWatermark = new ConcurrentDictionary<string, DateTimeOffset>();
        private readonly IProducerConsumerCollection<string> entities = new ConcurrentBag<string>();
        private readonly IPollingStrategy pollingStrategy;

        public BlobPoller(IPollingStrategy pollingStrategy)
        {
            if (pollingStrategy is null)
            {
                throw new ArgumentNullException(nameof(pollingStrategy));
            }

            this.pollingStrategy = pollingStrategy;
        }

        public void Add(string entity)
        {
            if (!this.entities.TryAdd(entity))
            {
                Console.Error.WriteLine("Can't add entity");
            }
        }

        public async Task StartAsync(CancellationToken token, TimeSpan frequency)
        {
            while (!token.IsCancellationRequested)
            {
                // TODO: check for cancel
                await Task.WhenAll(this.entities.Select(entity => this.pollingStrategy.CheckForUpdatesAsync(entity)));
                try
                {
                    await Task.Delay(frequency, token);
                }
                catch (TaskCanceledException) { }
            }
        }
    }
}