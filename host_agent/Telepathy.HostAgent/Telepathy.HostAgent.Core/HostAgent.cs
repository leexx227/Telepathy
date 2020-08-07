using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        private int dispatcherTimeoutMs = 5000;

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
            this.svcChannel = new Channel(svcHostName + svcPort, ChannelCredentials.Insecure);

            var dispatcherIp = environmentInfo.DispatcherIp;
            var dispatcherPort = environmentInfo.DispatcherPort;
            var dispatcherChannel = new Channel(dispatcherIp + dispatcherPort, ChannelCredentials.Insecure);
            this.dispatcherClient = new Dispatcher.DispatcherClient(dispatcherChannel);

            this.concurrentSvcTask = new Task[this.svcConcurrency];

            Console.WriteLine($"[Init]svc host name: {svcHostName}, svcport: {svcPort}, dispatcher ip: {dispatcherIp}, dispatcher port: {dispatcherPort}, svc concurrency: {this.svcConcurrency}, prefetch: {this.prefetchCount}");
        }

        public async Task StartAsync()
        {
            this.GetRequestAsync();
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
            var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.dispatcherTimeoutMs));

            while (!this.sessionFinished)
            {
                if (this.requestQueue.Count < this.prefetchCount)
                {
                    try
                    {
                        var request = await this.dispatcherClient.GetRequestAsync(new Empty(), callOptions);
                        this.requestQueue.Enqueue(request);
                        getEmptyQueueCount = 0;
                        currentRetryCount = 0;
                    }
                    catch (RpcException e)
                    {
                        // if e.Status.StatusCode==xxx, this.sessionFinished = true
                        Trace.TraceError($"[GetRequestAsync] Catch RPC exception: {e.Message}");
                        getEmptyQueueCount++;
                        await Task.Delay(this.defaultRetryIntervalMs * getEmptyQueueCount);
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
            while (true)
            {
                if (this.sessionFinished)
                {
                    break;
                }
                else
                {
                    if (!this.requestQueue.IsEmpty)
                    {
                        InnerRequest request;
                        if (this.requestQueue.TryDequeue(out request))
                        {
                            var response = await this.CallMethodWrapperAsync(request);
                            SendResponseAsync(response);
                        }
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
            var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.dispatcherTimeoutMs));

            await retry.RetryOperationAsync<object>(
                async () =>
                {
                    await this.dispatcherClient.SendResponseAsync(response, callOptions);
                    return null;
                },
                (e) =>
                {
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
