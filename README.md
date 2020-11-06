This prototype is to experiment different ways to poll a blob to test file read guarantees.

To run, add blob details to appsettings.json and run both projects. Use the postman collection to simulate pushing data to a blob. It will create a new file and return back the name of the file.

e.g. PostRows response: 
`Product/2020.11.06.12.24/8ef0b534-6a78-44aa-8003-3bd00e7e70e9`

In the blob poller you will see this file show up. The console output of blob poller looks like:

`5) Blob Product/2020.11.06.12.24/8ef0b534-6a78-44aa-8003-3bd00e7e70e9 updated created 11/6/2020 12:24:25 AM +00:00 now 11/6/2020 12:26:00 AM +00:00`

The latency and the guarantees depend on the polling strategies used.

LastModifiedTimeOptimizedPollingStrategy

This strategy uses the last modifed timestamp of the blob
to keep track of data that is already read and only forward reads.
This has an edge case. There are scenarios where last modified time
and the partitions fall into different windows. It looks like the enumerator
is lazy and gets files by name and not by last modified. This causes the files 
with a higher LastModified that occur later in the alphabetical sequence to show up
and the ones that appear early in the alphabetical sequence to show up.
This is solved by first materializing the list and then processing it.

OnePlusNWindowsPollingStrategy

This strategy processes data by partitions. In this case,
the data is processed for last N completed partitions and the current partition. 
This ensures that we allow enough time for writes to be flushed/committed, but also ensures that
the latency of incoming data is reduced to the polling frequency. The tradeoff here is that the reader needs
to ensure idempotency. This will guarantee at least once delivery, but messages will be replayed.


DelayedSlidingWindowPollingStrategy

This strategy processes data by partitions. In this case,
the data is processed for completed partitions only. This ensures that we allow
the data to be flushed/committed before it is picked up.
For instance, let's assume that the data is partitioned by minute. For this scenario,
in the worst case, a file is allowed upto 1 min to be committed from the time of creation.
This method ensures Once and only Once delivery at the cost of latency.
