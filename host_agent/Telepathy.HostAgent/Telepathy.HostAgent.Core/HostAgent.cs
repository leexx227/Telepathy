using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Telepathy.HostAgent.Common;
using Microsoft.Telepathy.HostAgent.Interface;
using Microsoft.Telepathy.ProtoBuf;

using Google.Protobuf.WellKnownTypes;

namespace Microsoft.Telepathy.HostAgent.Core
{
    public class HostAgent : IHostAgent
    {
        private Dispatcher.DispatcherClient dispatcherClient;

        private int svcPort;

        private Channel svcChannel;

        private EnvironmentInfo environmentInfo;

        private int svcTimeoutMs;

        private int dispatcherTimeoutMs = 3000;

        private int defaultRetryIntervalMs = 1000;

        private int checkQueueLengthIntervalMs = 2000;

        private int checkQueueEmptyIntervalMs = 1000;

        private int checkServiceAvailable = 2000;

        private int waitForSvcAvailable = 2000;

        private int svcConcurrency;

        private int maxRetries = 3;

        private ConcurrentQueue<InnerRequest> requestQueue = new ConcurrentQueue<InnerRequest>();

        private int prefetchCount;

        private bool sessionFinished = false;

        private Task[] concurrentSvcTask;

        public string SessionId { get; }

        private SvcLoader svcLoader;

        private Process svcProcess;

        private bool isSvcAvailable = false;

        public HostAgent(EnvironmentInfo environmentInfo)
        {
            this.environmentInfo = environmentInfo;
            this.svcTimeoutMs = environmentInfo.SvcTimeoutMs;
            this.svcConcurrency = environmentInfo.SvcConcurrency;
            this.prefetchCount = environmentInfo.PrefetchCount;

            var dispatcherIp = environmentInfo.DispatcherIp;
            var dispatcherPort = environmentInfo.DispatcherPort;
            var dispatcherTarget = dispatcherIp + ":" + dispatcherPort;
            var dispatcherChannel = new Channel(dispatcherTarget, ChannelCredentials.Insecure);
            this.dispatcherClient = new Dispatcher.DispatcherClient(dispatcherChannel);

            this.concurrentSvcTask = new Task[this.svcConcurrency];
            this.SessionId = environmentInfo.SessionId;

            this.svcLoader = new SvcLoader(SvcLoader.GetSvcMustVariableList());

            if (!this.ParameterValid)
            {
                Trace.TraceError($"Host agent initialization failed. Parameter invalid. Session id: {this.SessionId}, svc host name: {this.environmentInfo.SvcHostName}," +
                                 $"dispatcher ip: {this.environmentInfo.DispatcherIp}, dispatcher port: {this.environmentInfo.DispatcherPort}, svc timeout: {this.svcTimeoutMs}ms");
                Console.WriteLine($"Host agent initialization failed. Parameter invalid. Session id: {this.SessionId}, svc host name: {this.environmentInfo.SvcHostName}, " +
                                  $"dispatcher ip: {this.environmentInfo.DispatcherIp}, dispatcher port: {this.environmentInfo.DispatcherPort}, svc timeout: {this.svcTimeoutMs}ms");
                throw new InvalidOperationException("Host agent initialization failed. Parameter invalid.");
            }

            this.PrintInfo();
        }

        private bool ParameterValid => this.SvcTargetValid && this.DispatcherTargetValid && this.SessionIdValid &&
                                       this.svcTimeoutMs > 0 && this.prefetchCount >= 0 && this.svcConcurrency > 0;

        private bool SvcTargetValid => !string.IsNullOrEmpty(this.environmentInfo.SvcHostName);

        private bool DispatcherTargetValid => !string.IsNullOrEmpty(this.environmentInfo.DispatcherIp) &&
                                              (this.environmentInfo.DispatcherPort >= 0);

        private bool SessionIdValid => !string.IsNullOrEmpty(this.SessionId);

        private void PrintInfo()
        {
            Console.WriteLine($"[Host agent info] Session id: {this.SessionId}, svc host name: {this.environmentInfo.SvcHostName}, dispatcher ip: {this.environmentInfo.DispatcherIp}, " +
                              $"dispatcher port: {this.environmentInfo.DispatcherPort}, svc concurrency: {this.svcConcurrency}, svc prefetch count: {this.prefetchCount}, svc timeout: {this.svcTimeoutMs}ms.");
            Trace.TraceInformation($"[Host agent info] Session id: {this.SessionId}, svc host name: {this.environmentInfo.SvcHostName}, dispatcher ip: {this.environmentInfo.DispatcherIp}, " +
                                   $"dispatcher port: {this.environmentInfo.DispatcherPort}, svc concurrency: {this.svcConcurrency}, svc prefetch count: {this.prefetchCount}, svc timeout: {this.svcTimeoutMs}ms.");
        }

        /// <summary>
        /// Start host agent service.
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            var taskList = new List<Task>();
            await this.RetryToLoadSvc();

            taskList.Add(this.MonitorSvc());

            taskList.Add(this.GetRequestAsync());

            for (var i = 0; i < this.svcConcurrency; i++)
            {
                this.concurrentSvcTask[i] = this.SendRequestToSvcAsync();
            }

            var svcTask = Task.WhenAll(this.concurrentSvcTask);

            while (true)
            {
                var t = await Task.WhenAny(taskList);
                
                if (t.IsFaulted)
                {
                    Console.WriteLine($"Error occured: {t.Exception.Message}");
                    throw t.Exception;
                }

                taskList.Remove(t);
            }

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
                            Console.WriteLine($"[GetRequestAsync] Error occured when getting request from dispatcher: {e.Message}, retry count: {currentRetryCount}");
                            Trace.TraceError($"[GetRequestAsync] Error occured when getting request from dispatcher: {e.Message}, retry count: {currentRetryCount}");
                            currentRetryCount++;
                            await Task.Delay(this.defaultRetryIntervalMs);
                        }
                        else
                        {
                            Console.WriteLine($"[GetRequestAsync] Retry exhausted. Error occured when getting request from dispatcher: { e.Message}");
                            Trace.TraceError($"[GetRequestAsync] Retry exhausted. Error occured when getting request from dispatcher: { e.Message}");
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
                    if (this.isSvcAvailable)
                    {
                        InnerRequest request;
                        if (this.requestQueue.TryDequeue(out request))
                        {
                            var response = await this.CallMethodWrapperAsync(request);
                            Console.WriteLine($"thread: {Thread.CurrentThread.ManagedThreadId}, guid:{gui}, get reply");
                            await SendResponseAsync(response);
                            await Task.Delay(2000);
                        }
                    }
                    else
                    {
                        await Task.Delay(this.waitForSvcAvailable);
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

        /// <summary>
        /// Find an available port and use that port to start service.
        /// </summary>
        public void LoadSvc()
        {
            var port = Utility.GetAvailableSvcPort();
            try
            {
                while (true)
                {
                    var process = this.svcLoader.LoadSvc(port);
                    if (!process.HasExited)
                    {
                        this.svcProcess = process;
                        this.svcPort = port;
                        var svcTarget = this.environmentInfo.SvcHostName + ":" + this.svcPort;
                        this.svcChannel = new Channel(svcTarget, ChannelCredentials.Insecure);
                        this.isSvcAvailable = true;
                        return;
                    }
                    else
                    {
                        if (!Utility.PortAvailable(port))
                        {
                            Trace.TraceInformation($"Find port: {port} not available. Continue to search available port.");
                            Console.WriteLine($"Find port: {port} not available. Continue to search available port.");
                        }
                        else
                        {
                            Trace.TraceError($"Starting service process failed.");
                            Console.WriteLine($"Starting service process failed.");
                            throw new Exception("Starting service process failed.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError($"Error occured when starting service process: {e.Message}");
                Console.WriteLine($"Error occured when starting service process: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Monitor service available. If service has exited, will restart the service until meet the max retry count.
        /// </summary>
        /// <returns></returns>
        private async Task MonitorSvc()
        {
            while (true)
            {
                if (this.svcProcess.HasExited)
                {
                    this.isSvcAvailable = false;
                    await this.RetryToLoadSvc();
                }

                await Task.Delay(this.checkServiceAvailable);
            }
        }

        private async Task RetryToLoadSvc()
        {
            var retry = new RetryManager(this.defaultRetryIntervalMs, this.maxRetries);
            await retry.RetryOperationAsync<object>(
                () =>
                {
                    this.LoadSvc();
                    return Task.FromResult(new object());
                },
                (e) =>
                {
                    Console.WriteLine(
                        $"[MonitorSvc] Error occured when restarting service: {e.Message}, retry count: {retry.RetryCount}");
                    Trace.TraceError(
                        $"[MonitorSvc] Error occured when restarting service: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                });
        }
    }
}
