using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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

        private Channel svcChannel;

        private EnvironmentInfo environmentInfo;

        private TimeSpan svcTimeout;

        private TimeSpan dispatcherTimeout = TimeSpan.FromMilliseconds(5000);

        private TimeSpan defaultRetryInterval = TimeSpan.FromMilliseconds(1000);

        private TimeSpan checkQueueLengthInterval = TimeSpan.FromMilliseconds(2000);

        private TimeSpan checkQueueEmptyInterval = TimeSpan.FromMilliseconds(1000);

        private int svcConcurrency;

        private int maxRetries = 5;

        private ConcurrentQueue<InnerRequest> requestQueue = new ConcurrentQueue<InnerRequest>();

        private int prefetchCount;

        public HostAgent(EnvironmentInfo environmentInfo)
        {
            this.environmentInfo = environmentInfo;
            this.svcTimeout = environmentInfo.SvcTimeout;
            this.svcConcurrency = environmentInfo.SvcConcurrency;
            this.prefetchCount = environmentInfo.PrefetchCount;

            var svcHostName = environmentInfo.SvcHostName;
            var svcPort = environmentInfo.SvcPort;
            this.svcChannel = new Channel(svcHostName + svcPort, ChannelCredentials.Insecure);

            var dispatcherIp = environmentInfo.DispatcherIp;
            var dispatcherPort = environmentInfo.DispatcherPort;
            var dispatcherChannel = new Channel(dispatcherIp + dispatcherPort, ChannelCredentials.Insecure);
            this.dispatcherClient = new Dispatcher.DispatcherClient(dispatcherChannel);
        }

        public async Task StartAsync()
        {
            this.GetRequestAsync();
        }

        public async Task GetRequestAsync()
        {
            var getEmptyQueueCount = 0;
            var currentRetryCount = 0;
            var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.dispatcherTimeout.Milliseconds));

            while (true)
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
                        Trace.TraceError($"[GetRequestAsync] catch exception: {e.Message.ToString()}");
                        getEmptyQueueCount++;
                        await Task.Delay(TimeSpan.FromMilliseconds(this.defaultRetryInterval.Milliseconds * currentRetryCount));
                    }
                    catch (Exception e)
                    {
                        if (currentRetryCount < this.maxRetries)
                        {
                            Trace.TraceError($"[GetRequestAsync] catch exception: {e.Message.ToString()}, retry count: {currentRetryCount}");
                            currentRetryCount++;
                            await Task.Delay(this.defaultRetryInterval);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    Trace.TraceInformation($"[GetRequestAsync] prefetch config: {this.prefetchCount}, request queue length: {this.requestQueue.Count}, prefetch request enough.");
                    await Task.Delay(this.checkQueueLengthInterval);
                }
            }
        }
    }
}
