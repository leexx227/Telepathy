# Microsoft Telepathy Tutorial – Write your first SOA service and client

This series of tutorial introduces the SOA programming model for Microsoft Telepathy . This is the first tutorial of the series and provides guidance to write your first SOA service and client. 

## Getting started

Let's say we need a service running on the Telepathy cluster to echo the requests. Then we need a Telepathy client program to submit the echo requests to the service and returns the result to the end users. 

Here are the steps to write a SOA application in c#:

- Step 1: Implement the service

- Step 2: Deploy the service

- Step 3: Implement the client

- Step 4: Test the service

### Step 1: Implement the service

### Step 2: Deploy the service

### Step 3: Implement the client
Following code can be found in the Client project in the [sample code](../samples/csharp). 

Now we need a client program to submit the echo request. The client needs to invoke the service we just created remotely, so we need to leverage GRPC to generate the client code for us. Grpc tool can simplify the whole process by compiling specific .proto.

**Important**: Ensure that **echo.proto** is same in both server side and client side. We suggest that both sides refer the .proto link.

The suggested project organization in Visual Studio is to create different projects for the service and client programs, as shown in the following figure:

![image-20191114160851527](soa-tutorial-1-write-your-first-soa-service-and-client.media/add-service-reference.png)

Now we can write the client code:

1. Reference `Grpc.Tools`, `Google.Protobuf`, and `Grpc.Net.Client` Nuget package in the client project.

1. Add reference `ClientAPI.dll` after building [ClientAPI project](../client/ClientAPI) to the client project.

1. Add file `echo.proto` to the project and set its build action as **Protobuf compiler**.

1. Prepare the session info, which includes the frontend address (telepathy system entrance) and the service name. Let’s assume the head node host name is head.contoso.com and we are using the Echo service.

    ```csharp
    SessionStartInfo info = new SessionStartInfo("head.contoso.com:9100", "Echo");
    ```

1. With the SessionStartInfo object we can create a session to connect to the telepathy system.

   ```csharp
   using (Session session = await Session.CreateSessionAsync(info)) {……}
   ```

1. To be able to send tasks and receive results, you need to create a BatchClient object with the method.

   ```csharp
   using (Session session = await Session.CreateSessionAsync(info))
   {
       using (var client = new BatchClient(session, Echo.Descriptor.FindMethodByName("Echo"))){……}
   }
   ```

1. With the BatchClient, you can send tasks and receive results.

   ```csharp
   using (var client = new BatchClient(session, Echo.Descriptor.FindMethodByName("Echo")))
   {
       //send task
       var request = new EchoRequest{ Message = "hello" };
       client.SendTask(request);
       await client.EndTasksAsync();

       //get result
      await foreach (var result in client.GetResultsAsync<EchoReply>())
      {
         Console.WriteLine("Echo hello, and we get {0} from service", result.Message);
      }
   }
   ```
### Step 4: Test the service