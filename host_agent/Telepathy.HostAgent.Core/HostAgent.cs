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

namespace Microsoft.Telepathy.HostAgent.Core
{
    public class HostAgent : IHostAgent, IDisposable
    {
        private Dispatcher.DispatcherClient dispatcherClient;

        private int svcPort;

        private string svcHostName;

        internal Channel svcChannel;

        private int svcTimeoutMs;

        private int dispatcherTimeoutMs = 3000;

        private int defaultRetryIntervalMs = 1000;

        private int checkQueueLengthIntervalMs = 2000;

        private int checkQueueEmptyIntervalMs = 1000;

        private int checkSvcAvailableIntervalMs = 2000;

        private int waitForSvcAvailableIntervalMs = 2000;

        private int svcConcurrency;

        internal int maxRetries = 3;

        internal ConcurrentQueue<WrappedTask> taskQueue = new ConcurrentQueue<WrappedTask>();

        internal int prefetchCount { get; }

        private bool isTaskEnd = false;

        private Task[] concurrentSvcTask;

        public string SessionId { get; }

        private SvcLoader svcLoader;

        private Process svcProcess;

        private bool isSvcAvailable = false;

        internal int svcInitTimeoutMs;

        internal Stopwatch svcInitSw = new Stopwatch();

        private int consecutiveFailedTask = 0;

        private int maxConsecutiveFailedTask = 5;

        private CancellationTokenSource hostAgentCancellationTokenSource { get; } = new CancellationTokenSource();

        public CancellationToken HostAgentCancellationToken => this.hostAgentCancellationTokenSource.Token;

        public HostAgent(EnvironmentInfo environmentInfo)
        {
            if (!this.ParameterValid(environmentInfo))
            {
                Trace.TraceError(
                    $"Host agent initialization failed. Parameter invalid.");
                Console.WriteLine("Host agent initialization failed. Parameter invalid.");
                this.PrintInfo(environmentInfo);
                throw new InvalidOperationException("Host agent initialization failed. Parameter invalid.");
            }
            this.PrintInfo(environmentInfo);

            this.svcHostName = environmentInfo.SvcHostName;
            this.svcTimeoutMs = environmentInfo.SvcTimeoutMs;
            this.svcInitTimeoutMs = environmentInfo.SvcInitTimeoutMs;
            this.svcConcurrency = environmentInfo.SvcConcurrency;
            this.prefetchCount = environmentInfo.PrefetchCount;

            var dispatcherChannel = new Channel(environmentInfo.DispatcherIp, environmentInfo.DispatcherPort, ChannelCredentials.Insecure);
            this.dispatcherClient = new Dispatcher.DispatcherClient(dispatcherChannel);

            this.concurrentSvcTask = new Task[this.svcConcurrency];
            this.SessionId = environmentInfo.SessionId;

            this.svcLoader = new SvcLoader();
        }

        private bool ParameterValid(EnvironmentInfo info) => this.SvcTargetValid(info) && this.DispatcherTargetValid(info) && this.SessionIdValid(info) &&
                                        this.TimeoutValid(info) && info.PrefetchCount >= 0 && info.SvcConcurrency > 0;

        private bool SvcTargetValid(EnvironmentInfo info) => !string.IsNullOrEmpty(info.SvcHostName);

        private bool DispatcherTargetValid(EnvironmentInfo info) => !string.IsNullOrEmpty(info.DispatcherIp) &&
                                              (info.DispatcherPort >= 0);

        private bool SessionIdValid(EnvironmentInfo info) => !string.IsNullOrEmpty(info.SessionId);

        private bool TimeoutValid(EnvironmentInfo info) => info.SvcTimeoutMs > 0 && info.SvcInitTimeoutMs >= 0;

        private void PrintInfo(EnvironmentInfo info)
        {
            Console.WriteLine(
                $"[Host agent info] Session id: {info.SessionId}, svc host name: {info.SvcHostName}, dispatcher ip: {info.DispatcherIp}, dispatcher port: {info.DispatcherPort}, " +
                $"svc concurrency: {info.SvcConcurrency}, svc prefetch count: {info.PrefetchCount}, svc timeout: {info.SvcTimeoutMs}ms, svc init timeout: {info.SvcInitTimeoutMs}ms");
            Trace.TraceInformation(
                $"[Host agent info] Session id: {info.SessionId}, svc host name: {info.SvcHostName}, dispatcher ip: {info.DispatcherIp}, dispatcher port: {info.DispatcherPort}, " +
                $"svc concurrency: {info.SvcConcurrency}, svc prefetch count: {info.PrefetchCount}, svc timeout: {info.SvcTimeoutMs}ms, svc init timeout: {info.SvcInitTimeoutMs}ms");
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

            taskList.Add(this.GetTaskAsync());

            for (var i = 0; i < this.svcConcurrency; i++)
            {
                this.concurrentSvcTask[i] = this.SendTaskToSvcAsync();
            }

            var svcTask = Task.WhenAll(this.concurrentSvcTask);
            taskList.Add(svcTask);

            while (taskList.Count > 0)
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
        /// Get task wrapper from dispatcher and save the task wrapper into the queue until meet the prefetch count or task end.
        /// </summary>
        /// <returns></returns>
        internal async Task GetTaskAsync()
        {
            var getEmptyQueueCount = 0;
            var currentRetryCount = 0;
            var token = this.HostAgentCancellationToken;

            while (!this.isTaskEnd && !token.IsCancellationRequested)
            {
                if (this.taskQueue.Count < this.prefetchCount)
                {
                    try
                    {
                        var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.dispatcherTimeoutMs));
                        var task = new GetTaskRequest(){SessionId = this.SessionId};
                        var wrapperTask = await this.dispatcherClient.GetWrappedTaskAsync(task, callOptions);
                        if (wrapperTask.SessionState == SessionStateEnum.TempNoTask)
                        {
                            Console.WriteLine($"Find task empty");
                            getEmptyQueueCount++;
                            await Task.Delay(this.defaultRetryIntervalMs * getEmptyQueueCount);
                        }

                        if (wrapperTask.SessionState == SessionStateEnum.EndTask)
                        {
                            Console.WriteLine("Task end.");
                            this.isTaskEnd = true;
                        }
                        if(wrapperTask.SessionState == SessionStateEnum.Running)
                        {
                            Console.WriteLine("Get healthy task.");
                            this.taskQueue.Enqueue(wrapperTask);
                            getEmptyQueueCount = 0;
                            currentRetryCount = 0;
                        }
                    }
                    catch (Exception e)
                    {
                        if (currentRetryCount < this.maxRetries)
                        {
                            Console.WriteLine($"[GetTaskAsync] Error occured when getting task from dispatcher: {e.Message}, retry count: {currentRetryCount}");
                            Trace.TraceError($"[GetTaskAsync] Error occured when getting task from dispatcher: {e.Message}, retry count: {currentRetryCount}");
                            currentRetryCount++;
                            await Task.Delay(this.defaultRetryIntervalMs);
                        }
                        else
                        {
                            Console.WriteLine($"[GetTaskAsync] Retry exhausted. Error occured when getting task from dispatcher: { e.Message}");
                            Trace.TraceError($"[GetTaskAsync] Retry exhausted. Error occured when getting task from dispatcher: { e.Message}");
                            throw;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[GetTaskAsync] Prefetch task enough. Expected prefetch count: {this.prefetchCount}, current task queue length: {this.taskQueue.Count}.");
                    Trace.TraceInformation($"[GetTaskAsync] Prefetch task enough. Expected prefetch count: {this.prefetchCount}, current task queue length: {this.taskQueue.Count}.");
                    await Task.Delay(this.checkQueueLengthIntervalMs);
                }
            }
        }

        /// <summary>
        /// Send the task to the svc host and get the result.
        /// </summary>
        /// <returns></returns>
        private async Task SendTaskToSvcAsync()
        {
            while (true)
            {
                if (!this.taskQueue.IsEmpty)
                {
                    if (this.isSvcAvailable)
                    {
                        WrappedTask wrapperTask;
                        if (this.taskQueue.TryDequeue(out wrapperTask))
                        {
                            var result = await this.CallMethodWrapperAsync(wrapperTask);
                            await SendResultAsync(result);
                        }
                    }
                    else
                    {
                        await Task.Delay(this.waitForSvcAvailableIntervalMs);
                    }
                }
                else
                {
                    if (this.isTaskEnd)
                    {
                        break;
                    }
                    else
                    {
                        Trace.TraceInformation($"[SendTaskToSvcAsync] Task queue is empty.");
                        await Task.Delay(this.checkQueueEmptyIntervalMs);
                    }
                }
            }
        }

        /// <summary>
        /// Call svc host method using CallInvoker and build the SendResultRequest to send back to dispatcher.
        /// </summary>
        /// <param name="wrappedTask"></param>
        /// <returns>SendResultRequest which should be send to dispatcher.</returns>
        internal async Task<SendResultRequest> CallMethodWrapperAsync(WrappedTask wrappedTask)
        {
            var innerTask = InnerTask.Parser.ParseFrom(wrappedTask.SerializedInnerTask);
            
            var callInvoker = this.svcChannel.CreateCallInvoker();
            MessageWrapper resultMessage;
            try
            {
                switch (innerTask.MethodType)
                {
                    case MethodEnum.Unary:
                        resultMessage = await this.HandleUnaryCall(callInvoker, innerTask);
                        break;
                    case MethodEnum.ClientStream:
                        resultMessage = await this.HandleClientStreamingCall(callInvoker, innerTask);
                        break;
                    case MethodEnum.ServerStream:
                        resultMessage = await this.HandleServerStreamingCall(callInvoker, innerTask);
                        break;
                    case MethodEnum.DuplexStream:
                        resultMessage = await this.HandleDuplexStreamingCall(callInvoker, innerTask);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"[CallMethodWrapperAsync] Method type invalid: {innerTask.MethodType}");
                }
            }
            catch (Exception e)
            {
                Trace.TraceError($"[CallMethodWrapperAsync] Error occured when handling svc host method call: {e.Message}");
                var failedInnerResult = new InnerResult()
                {
                    StateCode = 1,
                    StateDetail = e.Message,
                    SessionId = innerTask.SessionId,
                    ClientId = innerTask.ClientId,
                    MessageId = innerTask.MessageId
                };
                var failedResult = new SendResultRequest()
                {
                    SessionId = wrappedTask.SessionId,
                    TaskId = wrappedTask.TaskId,
                    ClientId = innerTask.ClientId,
                    SerializedInnerResult = failedInnerResult.ToByteString()
                };

                if (e is RpcException)
                {
                    this.HandleRpcException((RpcException)e, ref failedResult);
                }
                else
                {
                    failedResult.TaskState = TaskStateEnum.Requeue;
                    Interlocked.Increment(ref this.consecutiveFailedTask);
                    Console.WriteLine($"[Host agent] Catch exception and return REQUEUE result to dispatcher. Consecutive failed task: {this.consecutiveFailedTask}.");
                }

                if (this.consecutiveFailedTask > this.maxConsecutiveFailedTask)
                {
                    throw new Exception($"[Host agent] Service consecutively failed. Consecutive failed task: {this.consecutiveFailedTask}.");
                }
                return failedResult;
            }

            var innerResult = new InnerResult()
            {
                Msg = ByteString.CopyFrom(resultMessage.Msg),
                StateCode = 0,
                StateDetail = "Success",
                SessionId = innerTask.SessionId,
                ClientId = innerTask.ClientId,
                MessageId = innerTask.MessageId
            };
            var result = new SendResultRequest()
            {
                SessionId = wrappedTask.SessionId,
                TaskId = wrappedTask.TaskId,
                ClientId = innerTask.ClientId,
                TaskState = TaskStateEnum.Finished,
                SerializedInnerResult = innerResult.ToByteString()
            };
            this.consecutiveFailedTask = 0;
            Console.WriteLine($"[Host agent] Get result successfully and return FINISHED result to dispatcher.");

            return result;
        }

        private void HandleRpcException(RpcException ex, ref SendResultRequest failedResult)
        {
            if (this.IsServiceException(ex))
            {
                failedResult.TaskState = TaskStateEnum.Finished;
                this.consecutiveFailedTask = 0;
                Console.WriteLine($"[Host agent] Catch exception and return FINISHED result to dispatcher.");
            }
            else
            {
                failedResult.TaskState = TaskStateEnum.Requeue;
                Interlocked.Increment(ref this.consecutiveFailedTask);
                Console.WriteLine($"[Host agent] Catch exception and return REQUEUE result to dispatcher. Consecutive failed task: {this.consecutiveFailedTask}.");
            }
        }

        private bool IsServiceException(RpcException ex) =>
            (ex.StatusCode == StatusCode.Internal) || (ex.StatusCode == StatusCode.Unknown) ||
                (ex.StatusCode == StatusCode.Unimplemented) || (ex.StatusCode == StatusCode.Unavailable);

        /// <summary>
        /// Call Unary method.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerTask"></param>
        /// <returns>MessageWrapper from service.</returns>
        internal async Task<MessageWrapper> HandleUnaryCall(CallInvoker callInvoker, InnerTask innerTask)
        {
            var retry = new RetryManager(this.defaultRetryIntervalMs, this.maxRetries);
            var task = this.GetMessageWrapper(innerTask);
            var method = new MethodWrapper(innerTask.ServiceName, innerTask.MethodName, MethodType.Unary);

            var result = await retry.RetryOperationAsync<MessageWrapper>(
                async () =>
                {
                    var callOptions = new CallOptions(deadline:DateTime.UtcNow.AddMilliseconds(this.svcTimeoutMs));
                    return await callInvoker.AsyncUnaryCall(method.Method, null, callOptions, task);
                },
                (e) =>
                {
                    if (e is RpcException)
                    {
                        this.HandleShouldNotRetryError((RpcException)e);
                    }
                    Trace.TraceError($"[HandleUnaryCall] Error occured when calling AsyncUnaryCall: {e.Message}, retry count: {retry.RetryCount}");
                    Console.WriteLine($"[HandleUnaryCall] Error occured when calling AsyncUnaryCall: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                },
                ()=> this.IsSvcInitTimeout());
            
            return result;
        }

        /// <summary>
        /// Call ClientStreaming method. Not currently supported.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerTask"></param>
        /// <returns></returns>
        private async Task<MessageWrapper> HandleClientStreamingCall(CallInvoker callInvoker, InnerTask innerTask)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Call ServerStreaming method. Not currently supported.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerTask"></param>
        /// <returns></returns>
        private async Task<MessageWrapper> HandleServerStreamingCall(CallInvoker callInvoker, InnerTask innerTask)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Call DuplexStreaming method. The svc host side service implementation must return one and exact one result in the response stream, or this function will throw exception.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerTask"></param>
        /// <returns>MessageWrapper from service.</returns>
        private async Task<MessageWrapper> HandleDuplexStreamingCall(CallInvoker callInvoker, InnerTask innerTask)
        {
            var retry = new RetryManager(this.defaultRetryIntervalMs, this.maxRetries);
            var task = this.GetMessageWrapper(innerTask);
            var method = new MethodWrapper(innerTask.ServiceName, innerTask.MethodName, MethodType.DuplexStreaming);

            var result = await retry.RetryOperationAsync<MessageWrapper>(
                async () =>
                {
                    var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.svcTimeoutMs));
                    var call = callInvoker.AsyncDuplexStreamingCall(method.Method, null, callOptions);

                    await call.RequestStream.WriteAsync(task);
                    await call.RequestStream.CompleteAsync();
                    var responseStream = call.ResponseStream;

                    var responseList = new List<MessageWrapper>();

                    while (await responseStream.MoveNext())
                    {
                        responseList.Add(responseStream.Current);
                    }

                    if (responseList.Count == 0)
                    {
                        throw new InvalidOperationException("[HandleDuplexStreamingCall] Get no result from response stream.");
                    }

                    if (responseList.Count > 1)
                    {
                        throw new InvalidOperationException($"[HandleDuplexStreamingCall] Response stream returns more than one result corresponding to one task. Gets {responseList.Count} results.");
                    }

                    return responseList[0];
                },
                (e) =>
                {
                    if (e is RpcException)
                    {
                        this.HandleShouldNotRetryError((RpcException)e);
                    }
                    Trace.TraceError($"[HandleDuplexStreamingCall] Error occured when calling HandleDuplexStreamingCall: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                },
                () => this.IsSvcInitTimeout());

            return result;
        }

        /// <summary>
        /// Send the result to dispatcher.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        internal async Task<IMessage> SendResultAsync(SendResultRequest result)
        {
            var retry = new RetryManager(this.defaultRetryIntervalMs, this.maxRetries);
            
            return await retry.RetryOperationAsync<IMessage>(
                async () =>
                {
                    var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.dispatcherTimeoutMs));
                    return await this.dispatcherClient.SendResultAsync(result, callOptions);
                },
                (e) =>
                {
                    Console.WriteLine($"[SendResultAsync] Error occured when sending result to dispatcher: {e.Message}, retry count: {retry.RetryCount}");
                    Trace.TraceError($"[SendResultAsync] Error occured when sending result to dispatcher: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                });
        }

        private MessageWrapper GetMessageWrapper(InnerTask innerTask)
        {
            if (innerTask != null)
            {
                return new MessageWrapper(innerTask.Msg);
            }
            else
            {
                throw new ArgumentNullException(nameof(innerTask));
            }
        }

        private void HandleShouldNotRetryError(RpcException e)
        {
            if (e.StatusCode == StatusCode.Unavailable)
            {
                Console.WriteLine($"[Host agent] Service port binding error: {e.Message}");
                Trace.TraceError($"[Host agent] Service port binding error: {e.Message}");
                if (this.IsSvcInitTimeout())
                {
                    throw e;
                }
            }
            else if (e.StatusCode == StatusCode.Unimplemented)
            {
                Console.WriteLine($"[Host agent] Service not implemented error: {e.Message}");
                Trace.TraceError($"[Host agent] Service not implemented error: {e.Message}");
                if (this.IsSvcInitTimeout())
                {
                    throw e;
                }
            }
        }

        /// <summary>
        /// Find an available port and use that port to start service.
        /// </summary>
        private void LoadSvc()
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
                        this.svcChannel = new Channel(this.svcHostName, this.svcPort, ChannelCredentials.Insecure);
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
            var token = this.HostAgentCancellationToken;
            while (true && !token.IsCancellationRequested)
            {
                if (this.svcProcess == null || this.svcProcess.HasExited)
                {
                    this.isSvcAvailable = false;
                    this.svcInitSw = new Stopwatch();
                    await this.RetryToLoadSvc();
                }

                await Task.Delay(this.checkSvcAvailableIntervalMs);
            }
        }

        /// <summary>
        /// Retry to load service.
        /// </summary>
        /// <returns></returns>
        private async Task RetryToLoadSvc()
        {
            this.svcInitSw.Start();
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

        /// <summary>
        /// Check if service initialization timeout.
        /// </summary>
        /// <returns>True if service initialization timeout.</returns>
        internal bool IsSvcInitTimeout()
        {
            if (!this.svcInitSw.IsRunning)
            {
                return true;
            }

            if (svcInitSw.ElapsedMilliseconds > this.svcInitTimeoutMs)
            {
                this.svcInitSw.Stop();
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (this.svcProcess != null && !this.svcProcess.HasExited)
            {
                this.svcProcess.Kill();
            }
            this.hostAgentCancellationTokenSource.Cancel();
            this.svcChannel.ShutdownAsync().Wait();
            this.taskQueue.Clear();

            GC.SuppressFinalize(this);
            Trace.TraceInformation("Host agent stopped.");
        }

        public void Stop()
        {
            this.hostAgentCancellationTokenSource.Cancel();
            Trace.TraceInformation("Host agent stopped.");
        }
    }
}
