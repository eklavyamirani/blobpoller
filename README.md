This prototype is to experiment different ways to poll a blob to test file read guarantees.

To run, add blob details to appsettings.json and run both projects. Use the postman collection to simulate pushing data to a blob. It will create a new file and return back the name of the file.

e.g. PostRows response: Product/2020.11.06.12.24/8ef0b534-6a78-44aa-8003-3bd00e7e70e9

In the blob poller you will see this file show up. The latency and the guarantees depend on the polling strategies used.
