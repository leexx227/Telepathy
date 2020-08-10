using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Telepathy.HostAgent.Common;
using Microsoft.Telepathy.HostAgent.Interface;
using Microsoft.Telepathy.ProtoBuf;

using Google.Protobuf.WellKnownTypes;
using Helloworld;

namespace Microsoft.Telepathy.HostAgent.Core
{
    public class HostAgent : IHostAgent
    {
        private Dispatcher.DispatcherClient dispatcherClient;

        private Channel svcChannel;

        private EnvironmentInfo environmentInfo;

        private int svcTimeoutMs;

        private int dispatcherTimeoutMs = 3000;

        private int defaultRetryIntervalMs = 1000;

        private int checkQueueLengthIntervalMs = 2000;

        private int checkQueueEmptyIntervalMs = 1000;

        private int svcConcurrency;

        private int maxRetries = 5;

        private ConcurrentQueue<InnerRequest> requestQueue = new ConcurrentQueue<InnerRequest>();

        private int prefetchCount;

        private bool sessionFinished = false;

        private Task[] concurrentSvcTask;

        public HostAgent(EnvironmentInfo environmentInfo)
        {
            this.environmentInfo = environmentInfo;
            this.svcTimeoutMs = environmentInfo.SvcTimeoutMs;
            this.svcConcurrency = environmentInfo.SvcConcurrency;
            this.prefetchCount = environmentInfo.PrefetchCount;

            var svcHostName = environmentInfo.SvcHostName;
            var svcPort = environmentInfo.SvcPort;
            var svcTarget = svcHostName + ":" + svcPort;
            this.svcChannel = new Channel(svcTarget, ChannelCredentials.Insecure);

            var dispatcherIp = environmentInfo.DispatcherIp;
            var dispatcherPort = environmentInfo.DispatcherPort;
            var dispatcherTarget = dispatcherIp + ":" + dispatcherPort;
            var dispatcherChannel = new Channel(dispatcherTarget, ChannelCredentials.Insecure);
            this.dispatcherClient = new Dispatcher.DispatcherClient(dispatcherChannel);

            this.concurrentSvcTask = new Task[this.svcConcurrency];

            if (!this.ParameterValid)
            {
                Trace.TraceError(
                    $"Host agent initialization failed. Parameter invalid. Svc host name: {this.environmentInfo.SvcHostName}, svc port: {this.environmentInfo.SvcPort}, " +
                    $"dispatcher ip: {this.environmentInfo.DispatcherIp}, dispatcher port: {this.environmentInfo.DispatcherPort}, svc timeout: {this.svcTimeoutMs}ms");
                Console.WriteLine($"Host agent initialization failed. Parameter invalid. Svc host name: {this.environmentInfo.SvcHostName}, svc port: {this.environmentInfo.SvcPort}, " +
                                  $"dispatcher ip: {this.environmentInfo.DispatcherIp}, dispatcher port: {this.environmentInfo.DispatcherPort}, svc timeout: {this.svcTimeoutMs}ms");
                throw new InvalidOperationException("Host agent initialization failed. Parameter invalid.");
            }

            Trace.TraceInformation($"[Host agent init] Svc host name: {svcHostName}, svc port: {svcPort}, dispatcher ip: {dispatcherIp}, " +
                                   $"dispatcher port: {dispatcherPort}, svc concurrency: {this.svcConcurrency}, prefetch: {this.prefetchCount}, svc timeout: {this.svcTimeoutMs}ms");

            Console.WriteLine($"[Init] Svc host name: {svcHostName}, svc port: {svcPort}, dispatcher ip: {dispatcherIp}, " +
                              $"dispatcher port: {dispatcherPort}, svc concurrency: {this.svcConcurrency}, prefetch: {this.prefetchCount}, svc timeout: {this.svcTimeoutMs}ms");
        }

        private bool ParameterValid => this.SvcTargetValid && this.DispatcherTargetValid &&
                                       this.svcTimeoutMs > 0 && this.prefetchCount > 0 && this.svcConcurrency > 0;

        private bool SvcTargetValid => !string.IsNullOrEmpty(this.environmentInfo.SvcHostName) &&
                                       (this.environmentInfo.SvcPort > 0);

        private bool DispatcherTargetValid => !string.IsNullOrEmpty(this.environmentInfo.DispatcherIp) &&
                                              (this.environmentInfo.DispatcherPort >= 0);

        public async Task StartAsync()
        {
            this.GetRequestAsync();

            ///////////////// test
            //HelloRequest helloRequest = new HelloRequest { Name = "xiang" };
            //var md = Greeter.Descriptor.FindMethodByName("SayHello");
            //InnerRequest innerRequest = new InnerRequest { ServiceName = md.Service.FullName, MethodName = md.Name, MethodType = MethodEnum.Unary, Msg = helloRequest.ToByteString()};
            //for (int i = 0; i < this.prefetchCount; i++)
            //{
            //    this.requestQueue.Enqueue(innerRequest);
            //}
            ////////////////
            for (var i = 0; i < this.svcConcurrency; i++)
            {
                this.concurrentSvcTask[i] = this.SendRequestToSvcAsync();
            }

            await Task.WhenAll(this.concurrentSvcTask);
        }

        /// <summary>
        /// Get inner request from dispatcher and save the request into the queue until meet the prefetch count or session closed.
        /// </summary>
        /// <returns></returns>
        public async Task GetRequestAsync()
        {
            var getEmptyQueueCount = 0;
            var currentRetryCount = 0;

            while (!this.sessionFinished)
            {
                if (this.requestQueue.Count < this.prefetchCount)
                {
                    try
                    {
                        var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.dispatcherTimeoutMs));
                        var request = await this.dispatcherClient.GetRequestAsync(new Empty(), callOptions);
                        if (request.StateCode == NewRequestSateCode.Empty)
                        {
                            Console.WriteLine($"Find request empty");
                            getEmptyQueueCount++;
                            await Task.Delay(this.defaultRetryIntervalMs * getEmptyQueueCount);
                        }

                        if (request.StateCode == NewRequestSateCode.Finish)
                        {
                            Console.WriteLine("Session end of request.");
                            this.sessionFinished = true;
                        }
                        else
                        {
                            Console.WriteLine("Get healthy request.");
                            this.requestQueue.Enqueue(request.Request);
                            getEmptyQueueCount = 0;
                            currentRetryCount = 0;
                        }
                    }
                    catch (Exception e)
                    {
                        if (currentRetryCount < this.maxRetries)
                        {
                            Trace.TraceError($"[GetRequestAsync] Error occured when getting request from dispatcher: {e.Message}, retry count: {currentRetryCount}");
                            currentRetryCount++;
                            await Task.Delay(this.defaultRetryIntervalMs);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    Trace.TraceInformation($"[GetRequestAsync] Prefetch request enough. Expected prefetch count: {this.prefetchCount}, current request queue length: {this.requestQueue.Count}.");
                    await Task.Delay(this.checkQueueLengthIntervalMs);
                }
            }
        }

        /// <summary>
        /// Send the request to the svc host and get the response.
        /// </summary>
        /// <returns></returns>
        public async Task SendRequestToSvcAsync()
        {
            var gui = Guid.NewGuid().ToString();
            Console.WriteLine($"enter sendrequest, thread: {Thread.CurrentThread.ManagedThreadId}, guid: {gui}");
            
            while (true)
            {
                if (!this.requestQueue.IsEmpty)
                {
                    InnerRequest request;
                    if (this.requestQueue.TryDequeue(out request))
                    {
                        var response = await this.CallMethodWrapperAsync(request);
                        //////// test
                        //var r = HelloReply.Parser.ParseFrom(response.Msg);
                        Console.WriteLine($"thread: {Thread.CurrentThread.ManagedThreadId}, guid:{gui}, get reply");
                        ///////
                        await SendResponseAsync(response);
                        await Task.Delay(2000);
                    }
                }
                else
                {
                    if (this.sessionFinished)
                    {
                        break;
                    }
                    else
                    {
                        Trace.TraceInformation($"[SendRequestToSvc] Request queue is empty.");
                        await Task.Delay(this.checkQueueEmptyIntervalMs);
                    }
                }
            }
        }

        /// <summary>
        /// Call svc host method using CallInvoker and build the inner response.
        /// </summary>
        /// <param name="innerRequest"></param>
        /// <returns>InnerResponse, which could be sent back to dispatcher.</returns>
        public async Task<InnerResponse> CallMethodWrapperAsync(InnerRequest innerRequest)
        {
            var callInvoker = this.svcChannel.CreateCallInvoker();
            MessageWrapper result;
            try
            {
                switch (innerRequest.MethodType)
                {
                    case MethodEnum.Unary:
                        result = await this.HandleUnaryCall(callInvoker, innerRequest);
                        break;
                    case MethodEnum.ClientStream:
                        result = await this.HandleClientStreamingCall(callInvoker, innerRequest);
                        break;
                    case MethodEnum.ServerStream:
                        result = await this.HandleServerStreamingCall(callInvoker, innerRequest);
                        break;
                    case MethodEnum.DuplexStream:
                        result = await this.HandleDuplexStreamingCall(callInvoker, innerRequest);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"[CallMethodWrapperAsync] Method type invalid: {innerRequest.MethodType}");
                }
            }
            catch (Exception e)
            {
                Trace.TraceError($"[CallMethodWrapperAsync] Error occured when handling svc host method call: {e.Message}");
                throw;
            }

            var response = new InnerResponse
            {
                Msg = ByteString.CopyFrom(result.Msg),
                SessionId = innerRequest.SessionId,
                ClientId = innerRequest.ClientId,
                MessageId = innerRequest.MessageId
            };
            return response;
        }

        /// <summary>
        /// Call Unary method.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerRequest"></param>
        /// <returns>MessageWrapper from svc host.</returns>
        public async Task<MessageWrapper> HandleUnaryCall(CallInvoker callInvoker, InnerRequest innerRequest)
        {
            var retry = new RetryManager(this.defaultRetryIntervalMs, this.maxRetries);
            var request = this.GetRequestWrapper(innerRequest);
            var method = new MethodWrapper(innerRequest.ServiceName, innerRequest.MethodName, MethodType.Unary);

            var result = await retry.RetryOperationAsync<MessageWrapper>(
                async () =>
                {
                    var callOptions = new CallOptions(deadline:DateTime.UtcNow.AddMilliseconds(this.svcTimeoutMs));
                    return await callInvoker.AsyncUnaryCall(method.Method, null, callOptions, request);
                },
                (e) =>
                {
                    Trace.TraceError($"[HandleUnaryCall] Error occured when calling AsyncUnaryCall: {e.Message}, retry count: {retry.RetryCount}");
                    Console.WriteLine($"[HandleUnaryCall] Error occured when calling AsyncUnaryCall: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                });
            
            return result;
        }

        /// <summary>
        /// Call ClientStreaming method. Not currently supported.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerRequest"></param>
        /// <returns></returns>
        public async Task<MessageWrapper> HandleClientStreamingCall(CallInvoker callInvoker, InnerRequest innerRequest)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Call ServerStreaming method. Not currently supported.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerRequest"></param>
        /// <returns></returns>
        public async Task<MessageWrapper> HandleServerStreamingCall(CallInvoker callInvoker, InnerRequest innerRequest)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Call DuplexStreaming method. The svc host side service implementation must return one and exact one result in the response stream, or this function will throw exception.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerRequest"></param>
        /// <returns>MessageWrapper from svc host.</returns>
        public async Task<MessageWrapper> HandleDuplexStreamingCall(CallInvoker callInvoker, InnerRequest innerRequest)
        {
            var retry = new RetryManager(this.defaultRetryIntervalMs, this.maxRetries);
            var request = this.GetRequestWrapper(innerRequest);
            var method = new MethodWrapper(innerRequest.ServiceName, innerRequest.MethodName, MethodType.DuplexStreaming);

            var result = await retry.RetryOperationAsync<MessageWrapper>(
                async () =>
                {
                    var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.svcTimeoutMs));
                    var call = callInvoker.AsyncDuplexStreamingCall(method.Method, null, callOptions);

                    await call.RequestStream.WriteAsync(request);
                    await call.RequestStream.CompleteAsync();
                    var responseStream = call.ResponseStream;

                    var responseList = new List<MessageWrapper>();

                    while (await responseStream.MoveNext())
                    {
                        responseList.Add(responseStream.Current);
                    }

                    if (responseList.Count == 0)
                    {
                        throw new InvalidOperationException("[HandleDuplexStreamingCall] Get no response from response stream.");
                    }

                    if (responseList.Count > 1)
                    {
                        throw new InvalidOperationException($"[HandleDuplexStreamingCall] Response stream returns more than one response corresponding to one request. Gets {responseList.Count} responses.");
                    }

                    return responseList[0];
                },
                (e) =>
                {
                    Trace.TraceError($"[HandleDuplexStreamingCall] Error occured when calling HandleDuplexStreamingCall: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                });

            return result;
        }

        /// <summary>
        /// Send the inner response to dispatcher.
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        public async Task SendResponseAsync(InnerResponse response)
        {
            var retry = new RetryManager(this.defaultRetryIntervalMs, this.maxRetries);
            
            await retry.RetryOperationAsync<object>(
                async () =>
                {
                    var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.dispatcherTimeoutMs));
                    await this.dispatcherClient.SendResponseAsync(response, callOptions);
                    return null;
                },
                (e) =>
                {
                    Console.WriteLine($"[SendResponseAsync] Error occured when sending response to dispatcher: {e.Message}, retry count: {retry.RetryCount}");
                    Trace.TraceError($"[SendResponseAsync] Error occured when sending response to dispatcher: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                });
        }

        public MessageWrapper GetRequestWrapper(InnerRequest innerRequest)
        {
            if (innerRequest != null)
            {
                return new MessageWrapper(innerRequest.Msg);
            }
            else
            {
                throw new ArgumentNullException(nameof(innerRequest));
            }
        }
    }
}
