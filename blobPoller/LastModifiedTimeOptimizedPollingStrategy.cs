using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace blobPoller
{
    /*
    * This strategy uses the last modifed timestamp of the blob
    * to keep track of data that is already read and only forward reads.
    * This has an edge case. There are scenarios where last modified time
    * and the partitions fall into different windows. It looks like the enumerator
    * is lazy and gets files by name and not by last modified. This causes the files 
    * with a higher LastModified that occur later in the alphabetical sequence to show up
    * and the ones that appear early in the alphabetical sequence to show up.
    * This is solved by first materializing the list and then processing it.
    */
    public class LastModifiedTimeOptimizedPollingStrategy: IPollingStrategy
    {
        private readonly string blobConnectionString;
        private readonly string containerName;
        private ConcurrentDictionary<string, DateTimeOffset> lastModifiedWatermark = new ConcurrentDictionary<string, DateTimeOffset>();
        private readonly TimeSpan artificialLag = TimeSpan.FromSeconds(5);

        #if DEBUG
        private int counter = 0;
        private HashSet<string> filesRead = new HashSet<string>();
        #endif

        public LastModifiedTimeOptimizedPollingStrategy(string blobConnectionString, string containerName)
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
            var blobMetadata = new List<(string Name, DateTimeOffset LastModified)>();
            foreach (var blobs in blobSet)
            {    
                await foreach (var blob in blobs)
                {
                    blobMetadata.Add((blob.Name, blob.Properties.LastModified.GetValueOrDefault()));
                }

            }

            foreach(var blob in blobMetadata)
            {
                var now = Now();
                var lastModified = blob.LastModified;
                if (lastModified > watermark && now - lastModified > this.artificialLag)
                {
                    if (lastModified > newWatermark)
                    {
                        newWatermark = lastModified;
                    }

#if DEBUG
                    Interlocked.Increment(ref counter);
                    filesRead.Add(blob.Name);
#endif
                    Console.WriteLine($"{counter}) Blob {blob.Name} updated created {lastModified} now {DateTimeOffset.UtcNow}");
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
            var minutesSinceLastUpdate = RemoveSecondAndMillisecondPart(Now()).Subtract(RemoveSecondAndMillisecondPart(watermark)).Minutes;
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

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private DateTimeOffset Now()
        {
            return DateTimeOffset.UtcNow;
        }

        private DateTimeOffset RemoveSecondAndMillisecondPart(DateTimeOffset dateTime)
        {
            // add a minute to ceiling to create a full window
            return dateTime.UtcDateTime.Date + new TimeSpan(dateTime.Hour, (dateTime.Minute), seconds: 0);
        }
    }
}