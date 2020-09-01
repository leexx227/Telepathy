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

        private int checkSvcAvailableIntervalMs = 2000;

        private int waitForSvcAvailableIntervalMs = 2000;

        private int svcConcurrency;

        private int maxRetries = 3;

        private ConcurrentQueue<WrappedTask> taskQueue = new ConcurrentQueue<WrappedTask>();

        private int prefetchCount;

        private bool isTaskEnd = false;

        private Task[] concurrentSvcTask;

        public string SessionId { get; }

        private SvcLoader svcLoader;

        private Process svcProcess;

        private bool isSvcAvailable = false;

        private int svcInitTimeoutMs;

        private Stopwatch svcInitSw = new Stopwatch();

        private int consecutiveFailedTask = 0;

        private int maxConsecutiveFailedTask = 5;

        public HostAgent(EnvironmentInfo environmentInfo)
        {
            this.environmentInfo = environmentInfo;
            this.svcTimeoutMs = environmentInfo.SvcTimeoutMs;
            this.svcInitTimeoutMs = environmentInfo.SvcInitTimeoutMs;
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
                Trace.TraceError(
                    $"Host agent initialization failed. Parameter invalid. Session id: {this.SessionId}, svc host name: {this.environmentInfo.SvcHostName}," +
                    $"dispatcher ip: {this.environmentInfo.DispatcherIp}, dispatcher port: {this.environmentInfo.DispatcherPort}, svc timeout: {this.svcTimeoutMs}ms, svc init timeout: {this.svcInitTimeoutMs}ms");
                Console.WriteLine(
                    $"Host agent initialization failed. Parameter invalid. Session id: {this.SessionId}, svc host name: {this.environmentInfo.SvcHostName}," +
                    $"dispatcher ip: {this.environmentInfo.DispatcherIp}, dispatcher port: {this.environmentInfo.DispatcherPort}, svc timeout: {this.svcTimeoutMs}ms, svc init timeout: {this.svcInitTimeoutMs}ms");
                throw new InvalidOperationException("Host agent initialization failed. Parameter invalid.");
            }

            this.PrintInfo();
        }

        private bool ParameterValid => this.SvcTargetValid && this.DispatcherTargetValid && this.SessionIdValid &&
                                        this.TimeoutValid && this.prefetchCount >= 0 && this.svcConcurrency > 0;

        private bool SvcTargetValid => !string.IsNullOrEmpty(this.environmentInfo.SvcHostName);

        private bool DispatcherTargetValid => !string.IsNullOrEmpty(this.environmentInfo.DispatcherIp) &&
                                              (this.environmentInfo.DispatcherPort >= 0);

        private bool SessionIdValid => !string.IsNullOrEmpty(this.SessionId);

        private bool TimeoutValid => this.svcTimeoutMs > 0 && this.svcInitTimeoutMs >= 0;

        private void PrintInfo()
        {
            Console.WriteLine(
                $"[Host agent info] Session id: {this.SessionId}, svc host name: {this.environmentInfo.SvcHostName}, dispatcher ip: {this.environmentInfo.DispatcherIp}, " +
                $"dispatcher port: {this.environmentInfo.DispatcherPort}, svc concurrency: {this.svcConcurrency}, svc prefetch count: {this.prefetchCount}, svc timeout: {this.svcTimeoutMs}ms svc init timeout: {this.svcInitTimeoutMs}ms");
            Trace.TraceInformation(
                $"[Host agent info] Session id: {this.SessionId}, svc host name: {this.environmentInfo.SvcHostName}, dispatcher ip: {this.environmentInfo.DispatcherIp}, " +
                $"dispatcher port: {this.environmentInfo.DispatcherPort}, svc concurrency: {this.svcConcurrency}, svc prefetch count: {this.prefetchCount}, svc timeout: {this.svcTimeoutMs}ms svc init timeout: {this.svcInitTimeoutMs}ms");
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
        /// Get task wrapper from dispatcher and save the task wrapper into the queue until meet the prefetch count or task end.
        /// </summary>
        /// <returns></returns>
        public async Task GetTaskAsync()
        {
            var getEmptyQueueCount = 0;
            var currentRetryCount = 0;

            while (!this.isTaskEnd)
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
                    Trace.TraceInformation($"[GetTaskAsync] Prefetch task enough. Expected prefetch count: {this.prefetchCount}, current task queue length: {this.taskQueue.Count}.");
                    await Task.Delay(this.checkQueueLengthIntervalMs);
                }
            }
        }

        /// <summary>
        /// Send the task to the svc host and get the result.
        /// </summary>
        /// <returns></returns>
        public async Task SendTaskToSvcAsync()
        {
            var gui = Guid.NewGuid().ToString();
            Console.WriteLine($"enter sendtask, thread: {Thread.CurrentThread.ManagedThreadId}, guid: {gui}");
            
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
                            Console.WriteLine($"thread: {Thread.CurrentThread.ManagedThreadId}, guid:{gui}, get reply");
                            await SendResultAsync(result);
                            await Task.Delay(2000);
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
        public async Task<SendResultRequest> CallMethodWrapperAsync(WrappedTask wrappedTask)
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
                    SerializedInnerResult = failedInnerResult.ToByteString()
                };

                if (e is RpcException)
                {
                    var ex = e as RpcException;
                    if (ex.StatusCode == StatusCode.Unknown)
                    {
                        failedResult.TaskState = TaskStateEnum.Finished;
                        this.consecutiveFailedTask = 0;
                    }
                    else
                    {
                        failedResult.TaskState = TaskStateEnum.Requeue;
                        Interlocked.Increment(ref this.consecutiveFailedTask);
                    }
                }
                else
                {
                    failedResult.TaskState = TaskStateEnum.Requeue;
                    Interlocked.Increment(ref this.consecutiveFailedTask);
                }

                if (this.consecutiveFailedTask > this.maxConsecutiveFailedTask)
                {
                    throw new Exception($"[Host agent] Service consecutively failed. ConsecutiveFailedTask:{this.consecutiveFailedTask}.");
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
                TaskState = TaskStateEnum.Finished,
                SerializedInnerResult = innerResult.ToByteString()
            };
            this.consecutiveFailedTask = 0;

            return result;
        }

        /// <summary>
        /// Call Unary method.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerTask"></param>
        /// <returns>MessageWrapper from service.</returns>
        public async Task<MessageWrapper> HandleUnaryCall(CallInvoker callInvoker, InnerTask innerTask)
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
                        this.HandlePortBindError((RpcException)e);
                    }
                    Trace.TraceError($"[HandleUnaryCall] Error occured when calling AsyncUnaryCall: {e.Message}, retry count: {retry.RetryCount}");
                    Console.WriteLine($"[HandleUnaryCall] Error occured when calling AsyncUnaryCall: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                },
                ()=>(this.IsSvcInitTimeout() && this.isSvcAvailable));
            
            return result;
        }

        /// <summary>
        /// Call ClientStreaming method. Not currently supported.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerTask"></param>
        /// <returns></returns>
        public async Task<MessageWrapper> HandleClientStreamingCall(CallInvoker callInvoker, InnerTask innerTask)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Call ServerStreaming method. Not currently supported.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerTask"></param>
        /// <returns></returns>
        public async Task<MessageWrapper> HandleServerStreamingCall(CallInvoker callInvoker, InnerTask innerTask)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Call DuplexStreaming method. The svc host side service implementation must return one and exact one result in the response stream, or this function will throw exception.
        /// </summary>
        /// <param name="callInvoker"></param>
        /// <param name="innerTask"></param>
        /// <returns>MessageWrapper from service.</returns>
        public async Task<MessageWrapper> HandleDuplexStreamingCall(CallInvoker callInvoker, InnerTask innerTask)
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
                        this.HandlePortBindError((RpcException)e);
                    }
                    Trace.TraceError($"[HandleDuplexStreamingCall] Error occured when calling HandleDuplexStreamingCall: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                },
                () => (this.IsSvcInitTimeout() && this.isSvcAvailable));

            return result;
        }

        /// <summary>
        /// Send the result to dispatcher.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public async Task SendResultAsync(SendResultRequest result)
        {
            var retry = new RetryManager(this.defaultRetryIntervalMs, this.maxRetries);
            
            await retry.RetryOperationAsync<object>(
                async () =>
                {
                    var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.dispatcherTimeoutMs));
                    await this.dispatcherClient.SendResultAsync(result, callOptions);
                    return null;
                },
                (e) =>
                {
                    Console.WriteLine($"[SendResultAsync] Error occured when sending result to dispatcher: {e.Message}, retry count: {retry.RetryCount}");
                    Trace.TraceError($"[SendResultAsync] Error occured when sending result to dispatcher: {e.Message}, retry count: {retry.RetryCount}");
                    return Task.CompletedTask;
                });
        }

        public MessageWrapper GetMessageWrapper(InnerTask innerTask)
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

        private void HandlePortBindError(RpcException e)
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
                        this.svcInitSw.Start();
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
        private bool IsSvcInitTimeout()
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
    }
}
