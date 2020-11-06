using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace blobPoller
{
    /*
    * This strategy processes data by partitions. In this case,
    * the data is processed for completed partitions only. This ensures that we allow
    * the data to be flushed/committed before it is picked up.
    * For instance, let's assume that the data is partitioned by minute. For this scenario,
    * in the worst case, a file is allowed upto 1 min to be committed from the time creation.
    * This method ensures Once and only Once delivery at the cost of latency.
    */
    public class DelayedSlidingWindowPollingStrategy: IPollingStrategy
    {
        private readonly string blobConnectionString;
        private readonly string containerName;
        private ConcurrentDictionary<string, DateTimeOffset> lastModifiedWatermark = new ConcurrentDictionary<string, DateTimeOffset>();
        private readonly TimeSpan artificialLag = TimeSpan.FromSeconds(0);

        private int counter = 0;

        private HashSet<string> filesRead = new HashSet<string>();

        public DelayedSlidingWindowPollingStrategy(string blobConnectionString, string containerName)
        {
            if (string.IsNullOrWhiteSpace(blobConnectionString))
            {
                throw new ArgumentException($"'{nameof(blobConnectionString)}' cannot be null or whitespace", nameof(blobConnectionString));
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException($"'{nameof(containerName)}' cannot be null or whitespace", nameof(containerName));
            }

            this.blobConnectionString = blobConnectionString;
            this.containerName = containerName;
        }

        public async Task CheckForUpdatesAsync(string entity)
        {
            var client = new BlobServiceClient(this.blobConnectionString);
            var containerRef = client.GetBlobContainerClient(this.containerName);

            if (!this.lastModifiedWatermark.TryGetValue(entity, out var watermark))
            {
                this.TryLoadWatermarkFromSQL(entity, out watermark);
            }

            // TODO: asyncenumerable selectmany to flatten
            var blobSet = GetPartitionsSince(watermark)
                .Select(partition => containerRef.GetBlobsAsync(prefix: $"{entity}/{partition}"));

            var newWatermark = watermark;
            foreach (var blobs in blobSet)
            {    
                await foreach (var blob in blobs)
                {
                    var lastModified = blob.Properties.LastModified.GetValueOrDefault();
                    if (lastModified > watermark)
                    {
                        if (lastModified > newWatermark)
                        {
                            newWatermark = lastModified;
                        }

                        //if (!filesRead.Contains(blob.Name))
                        {
                            Interlocked.Increment(ref counter);
                            Console.WriteLine($"{counter}) Blob {blob.Name} updated created {lastModified} now {DateTimeOffset.UtcNow}");
                            filesRead.Add(blob.Name);
                        }
                    }
                }

            }

            this.lastModifiedWatermark.AddOrUpdate(entity, (entity) => newWatermark, (entity, oldValue) => newWatermark);
        }

        private bool TryLoadWatermarkFromSQL(string entity, out DateTimeOffset watermark)
        {
            watermark = Now();

            return true;
        }

        private IEnumerable<string> GetPartitionsSince(DateTimeOffset watermark)
        {
            var minutesSinceLastUpdate = Now().Subtract(RemoveSecondAndMillisecondPart(watermark)).Minutes;
            foreach(var minute in Enumerable.Range(-1, minutesSinceLastUpdate))
            {
                yield return FormatAsPartitionString(watermark.AddMinutes(minute));
            }
        }

        private string GetCurrentPartition()
        {
            return FormatAsPartitionString(Now());
        }

        private string FormatAsPartitionString(DateTimeOffset dateTime)
        {
            return dateTime.ToString("yyyy.MM.dd.hh.mm");
        }

        private DateTimeOffset Now()
        {
            return RemoveSecondAndMillisecondPart(DateTimeOffset.UtcNow);
        }

        private DateTimeOffset RemoveSecondAndMillisecondPart(DateTimeOffset dateTime)
        {
            // add a minute to ceiling to create a full window
            return dateTime.UtcDateTime.Date + new TimeSpan(dateTime.Hour, (dateTime.Minute), seconds: 0);
        }
    }
}