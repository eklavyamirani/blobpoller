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
    * the data is processed for last N completed partitions and the current partition. 
    * This ensures that we allow enough time for writes to be flushed/committed, but also ensures that
    * the latency of incoming data is reduced to the polling frequency. The tradeoff here is that the reader needs
    * to ensure idempotency. This will guarantee at least once delivery, but messages will be replayed.
    */
    public class OnePlusNWindowsPollingStrategy: IPollingStrategy
    {
        private readonly string blobConnectionString;
        private readonly string containerName;
        private readonly int numberOfTrailingWindows;

        private int counter = 0;

        private HashSet<string> filesRead = new HashSet<string>();

        public OnePlusNWindowsPollingStrategy(string blobConnectionString, string containerName, int numberOfTrailingWindows = 1)
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
            this.numberOfTrailingWindows = numberOfTrailingWindows;
        }

        public async Task CheckForUpdatesAsync(string entity)
        {
            var client = new BlobServiceClient(this.blobConnectionString);
            var containerRef = client.GetBlobContainerClient(this.containerName);

            // TODO: asyncenumerable selectmany to flatten
            var blobSet = GetPartitionsSince(Now().Subtract(TimeSpan.FromMinutes(1 * this.numberOfTrailingWindows)))
                .Select(partition => containerRef.GetBlobsAsync(prefix: $"{entity}/{partition}"));

            foreach (var blobs in blobSet)
            {    
                await foreach (var blob in blobs)
                {
                    var lastModified = blob.Properties.LastModified.GetValueOrDefault();

                    if (!filesRead.Contains(blob.Name))
                    {
                        Interlocked.Increment(ref counter);
                        Console.WriteLine($"{counter}) Blob {blob.Name} updated created {lastModified} now {DateTimeOffset.UtcNow}");
                        filesRead.Add(blob.Name);
                    }
                }
            }
        }

        private bool TryLoadWatermarkFromSQL(string entity, out DateTimeOffset watermark)
        {
            watermark = Now();

            return true;
        }

        private IEnumerable<string> GetPartitionsSince(DateTimeOffset watermark)
        {
            var minutesSinceLastUpdate = Now().Subtract(RemoveSecondAndMillisecondPart(watermark)).Minutes;
            foreach(var minute in Enumerable.Range(0, minutesSinceLastUpdate + 1))
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