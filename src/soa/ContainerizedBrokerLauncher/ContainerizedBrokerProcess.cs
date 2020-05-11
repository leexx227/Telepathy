// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.Net;

namespace Microsoft.Telepathy.Internal.ContainerizedBrokerLauncher
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Text;
    using System.Threading;

    using Microsoft.Hpc.Scheduler.Session;
    using Microsoft.Hpc.Scheduler.Session.Internal;
    using Microsoft.Telepathy.RuntimeTrace;
    using Microsoft.Telepathy.Session.Common;
    using Microsoft.Telepathy.Session.Exceptions;
    using Microsoft.Telepathy.Session.Internal;
    using Microsoft.Win32.SafeHandles;
    using System.Collections.Generic;
    using k8s;
    using k8s.Models;
    using Microsoft.Telepathy.Common.TelepathyContext;
    using Microsoft.Telepathy.Common.TelepathyContext.Extensions;
    using Microsoft.Telepathy.Session;

    /// <summary>
    /// Wrapped native operation to a broker process
    /// </summary>
    [SecurityCritical]
    internal class ContainerizedBrokerProcess : CriticalFinalizerObject, IDisposable
    {
        /// <summary>
        /// Stores the ready timeout
        /// </summary>
        private static readonly TimeSpan readyTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Stores the timeout waiting for process exit event finish
        /// </summary>
        private static readonly TimeSpan processExitEventFinishedWaitTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Stores the broker shim file name
        /// </summary>
        private const string BrokerServiceName = "HpcBrokerWorker.exe";

        /// <summary>
        /// Stores the creation flag
        /// </summary>
        private const uint creationFlags = NativeMethods.CREATE_UNICODE_ENVIRONMENT | NativeMethods.CREATE_SUSPENDED | NativeMethods.CREATE_NO_WINDOW;

        /// <summary>
        /// Stores the startup info
        /// </summary>
        private NativeMethods.STARTUPINFO startupInfo;

        /// <summary>
        /// Stores the process info
        /// </summary>
        private NativeMethods.PROCESS_INFORMATION processInfo;

        /// <summary>
        /// Stores the ready wait handle
        /// </summary>
        private EventWaitHandle readyWaitHandle;

        /// <summary>
        /// Stores the exit wait handle
        /// </summary>
        private ManualResetEvent exitWaitHandle;

        /// <summary>
        /// Stores the wait handle that sets when process is exited and all exited event are finished
        /// </summary>
        private ManualResetEvent processExitAndEventFinishedWaitHandle = new ManualResetEvent(false);

        /// <summary>
        /// Stores the environment handle
        /// </summary>
        private GCHandle environmentHandle;

        /// <summary>
        /// Stores a value indicating whether the instance has been disposed
        /// </summary>
        private bool disposed;

        /// <summary>
        /// It is set to 1 when current object's dispose method is called.
        /// </summary>
        private int disposedFlag;

        /// <summary>
        /// Stores the lock object to protect disposing procedure
        /// </summary>
        private object lockThis = new object();

        /// <summary>
        /// Stores the unique id for the broker worker process
        /// </summary>
        private Guid uniqueId = Guid.NewGuid();

        private ITelepathyContext context;

        private readonly IKubernetes Client;

        private string workerName;

        public string WorkerName
        {
            get => workerName;
        }

        private string sessionId;

        public string SessionId
        {
            set;
            get;
        }


        /// <summary>
        /// Initializes a new instance of the BrokerProcess class
        /// </summary>
        public ContainerizedBrokerProcess(ITelepathyContext context, string sessionId)
        {
            this.context = context;
            this.sessionId = sessionId;
            workerName = "brokerworker" + UniqueId;
            V1StatefulSet statefulSet = new V1StatefulSet()
            {
                ApiVersion = "apps/v1",
                Kind = "StatefulSet",
                Metadata = new V1ObjectMeta()
                {
                    Name = workerName,
                    NamespaceProperty = "telepathy",
                    Labels = new Dictionary<string, string>
                    {
                        {"app", "brokerworker"},
                        {"run", workerName}
                    }
                },
                Spec = new V1StatefulSetSpec
                {
                    ServiceName = workerName,
                    Replicas = 1,
                    Selector = new V1LabelSelector()
                    {
                        MatchLabels = new Dictionary<string, string>
                        {
                            {"app", "brokerworker"},
                            {"run", workerName}
                        }
                    },
                    Template = new V1PodTemplateSpec()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            CreationTimestamp = null,
                            Labels = new Dictionary<string, string>
                            {
                                {"app", "brokerworker"},
                                {"run", workerName}
                            }
                        },
                        Spec = new V1PodSpec
                        {
                            NodeSelector = new Dictionary<string, string>
                            {
                                { "kubernetes.io/os", "windows" }
                            },
                            Containers = new List<V1Container>()
                            {
                                new V1Container()
                                {
                                    Name = "brokerworker",
                                    Image = "telepathy.azurecr.io/telepathy/brokerworker:latest",
                                    ImagePullPolicy = "Always",

                                    Ports = new List<V1ContainerPort> { new V1ContainerPort(9091), new V1ContainerPort(9093) },
                                    Env = new List<V1EnvVar>
                                    {
                                       new V1EnvVar("REDIS_HOST", "redis-master"),
                                       new V1EnvVar("REDIS_PORT", "6379"),
                                    }
                                }
                            }
                        }
                    }
                }
            };
            V1Service service = new V1Service()
            {
                ApiVersion = "v1",
                Kind = "Service",
                Metadata = new V1ObjectMeta()
                {
                    Name = workerName,
                    NamespaceProperty = "telepathy",
                    Labels = new Dictionary<string, string>
                    {
                        {"app", "brokerworker"},
                        {"run", workerName}
                    }
                },
                Spec = new V1ServiceSpec
                {
                    Type = "LoadBalancer",
                    Selector = new Dictionary<string, string>
                    {
                       {"app", "brokerworker"},
                       {"run", workerName}
                    },
                    Ports = new List<V1ServicePort> { new V1ServicePort() { Name = "worker", Port = 9091, TargetPort = 9091 }, new V1ServicePort() { Name = "management", Port = 9093, TargetPort = 9093 } }
                }
            };
            try
            {
                Client = context.GetClusterClientAsync<IKubernetes>().Result;
                Client.CreateNamespacedStatefulSet(statefulSet, "telepathy");
                Client.CreateNamespacedService(service, "telepathy");
            }
            catch (Microsoft.Rest.HttpOperationException httpOperationException)
            {
                var content = httpOperationException.Response.Content;
                TraceHelper.TraceEvent(TraceEventType.Error, "[BrokerProcess] Error happens when create broker workers.");
                throw;
            }

            try
            {
                TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerProcess] Build unique waithandler for brokerworker {0}.", workerName);
                string uniqueWaitHandleName = BuildUniqueWaitHandle(-1, out this.readyWaitHandle, workerName);

                WaitOrTimerCallback brokerProcessReadyCallback = new ThreadHelper<object>(new WaitOrTimerCallback(this.BrokerProcessReadyCallback)).WaitOrTimerCallbackRoot;
                WaitOrTimerCallback processExitCallback = new ThreadHelper<object>(new WaitOrTimerCallback(this.ProcessExitCallback)).WaitOrTimerCallbackRoot;

                this.exitWaitHandle = new ManualResetEvent(false);

                // Register broker process exit callback
                ThreadPool.RegisterWaitForSingleObject(this.exitWaitHandle, processExitCallback, null, -1, true);

                // Register callback to be raised when broker process opened service host and is ready to initialize.
                ThreadPool.RegisterWaitForSingleObject(this.readyWaitHandle, brokerProcessReadyCallback, null, readyTimeout, true);
            }
            catch (Exception e)
            {
                TraceHelper.TraceEvent(TraceEventType.Error, "[BrokerProcess] New containerized brokerprocess failed.");
                TraceHelper.TraceEvent(TraceEventType.Error, "[BrokerProce] {0}", e.ToString());
            }
            TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerProcess] Initialize brokerworker {0} finished.", workerName);
        }

        /// <summary>
        /// Finalizes an instance of the BrokerProcess class
        /// </summary>
        ~ContainerizedBrokerProcess()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Gets the broker process exited event
        /// </summary>
        public event EventHandler Exited;

        /// <summary>
        /// Gets the broker process ready event
        /// </summary>
        public event EventHandler<BrokerProcessReadyEventArgs> Ready;

        /// <summary>
        /// Gets the unique id of the broker worker process
        /// </summary>
        public Guid UniqueId
        {
            get { return this.uniqueId; }
        }

        /// <summary>
        /// Gets the exit code
        /// </summary>
        public int ExitCode
        {
            get
            {
                uint exitCode;
                NativeMethods.GetExitCodeProcess(new SafeProcessHandle(this.processInfo.hProcess, false), out exitCode);
                return (int)exitCode;
            }
        }

        /// <summary>
        /// Gets the process id of this broker process
        /// </summary>
        public int Id
        { 
            get { return this.processInfo.dwProcessId; }
        }

        /// <summary>
        /// Start the broker process by resuming the thread
        /// </summary>
        public void Start()
        {
            TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerProcess] Try to get brokerworker {0} from AKS to confirm resource has been created in AKS.", workerName);
            while (true)
            {
                try
                {
                    V1Service svc = Client.ReadNamespacedService(WorkerName, "telepathy");
                    V1StatefulSet statefulSet = Client.ReadNamespacedStatefulSet(WorkerName, "telepathy");
                    if (svc.Status.LoadBalancer.Ingress == null)
                    {
                        continue;
                    }
                    break;
                }
                catch (AggregateException ex)
                {
                    foreach (var innerEx in ex.InnerExceptions)
                    {
                        if (innerEx is Microsoft.Rest.HttpOperationException)
                        {
                            var code = ((Microsoft.Rest.HttpOperationException)innerEx).Response.StatusCode;
                            if (code == HttpStatusCode.NotFound)
                            {
                                TraceHelper.TraceEvent(TraceEventType.Error, "[BrokerProcess] Related resource pods hasn't been created in AKS.");
                                continue;
                            }
                            else
                            {
                                TraceHelper.TraceEvent(TraceEventType.Error, "[BrokerProcess] Error occures when read resource from AKS: {0}.", innerEx.ToString());
                                throw ex;
                            }
                        }
                    }
                }
            }

            string workerExternalIp = Client.ReadNamespacedService(WorkerName, "telepathy").Status.LoadBalancer.Ingress[0].Ip;
            // set worker external ip address in Redis for quering by FrontEndBuilder
            context.Registry.SetValueAsync<string>(null, "broker" + sessionId, workerExternalIp, new CancellationToken()).GetAwaiter().GetResult();
            TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerProcess] Successfully set brokermanagementservice cluster external address in Redis:  : {0}.", workerExternalIp);

            context.Registry.SetValueAsync<string>(null, "brokername" + sessionId, WorkerName, new CancellationToken()).GetAwaiter().GetResult();
            TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerProcess] Successfully store brokername of AKS for future use like delete broker workers.");

            readyWaitHandle.Set();
        }

        /// <summary>
        /// Wait for the broker process ready
        /// </summary>
        public void WaitForReady()
        {
            lock (this.lockThis)
            {
                if (this.disposed)
                {
                    return;
                }

                int signal = WaitHandle.WaitAny(new WaitHandle[] { this.readyWaitHandle, this.exitWaitHandle }, readyTimeout, false);
                switch (signal)
                {
                    case WaitHandle.WaitTimeout:
                        // Timeout for ready, Kill process
                        try
                        {
                            this.KillBrokerProcess();
                        }
                        catch (Exception)
                        {
                            // Swallow the exception if failed to kill the custom broker process
                        }

                        ThrowHelper.ThrowSessionFault(SOAFaultCode.Broker_CustomBrokerReadyTimeout, SR.CustomBrokerReadyTimeout, readyTimeout.ToString());
                        break;
                    case 0:
                        // ReadyWaitHandle triggered, exit
                        break;
                    case 1:
                        // ExitWaitHandle triggered, throw exception
                        ThrowHelper.ThrowSessionFault(SOAFaultCode.Broker_CustomBrokerExitBeforeReady, SR.CustomBrokerExitBeforeReady, this.ExitCode.ToString());
                        break;
                    default:
                        Debug.Fail(String.Format("[BrokerProcess] Invalid signal from WaitHandle.WaitAny: {0}", signal));
                        break;
                }
            }
        }

        /// <summary>
        /// Wait until process is exited and all process exit callback is finished
        /// </summary>
        /// <remarks>
        /// This method won't be called concurrently
        /// </remarks>
        public void WaitForExit(TimeSpan timeoutToKillProcess)
        {
            lock (this.lockThis)
            {
                if (this.disposed)
                {
                    // Return immediately if the broker process has already exited and disposed
                    return;
                }

                if (!this.exitWaitHandle.WaitOne(timeoutToKillProcess, false))
                {
                    // Timeout , Kill process
                    try
                    {
                        this.KillBrokerProcess();
                    }
                    catch (Exception)
                    {
                        // Swallow the exception if failed to kill the custom broker process
                    }
                }

                // Still needs to wait until all event are finished
                if (!this.processExitAndEventFinishedWaitHandle.WaitOne(processExitEventFinishedWaitTimeout, false))
                {
                    TraceHelper.TraceError("0", "[BrokerProcess] Timeout waiting for process exit event finish.");
                }
            }
        }

        /// <summary>
        /// Kill broker process
        /// </summary>
        public void KillBrokerProcess()
        {
            TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerProcess] Kill broker process, delete service and statefulset in K8s.");
            Client.DeleteNamespacedStatefulSet(WorkerName, "telepathy");
            Client.DeleteNamespacedService(WorkerName, "telepathy");
        }

        /// <summary>
        /// Close the broker process, terminate the broker process
        /// </summary>
        public void Close()
        {
            try
            {
                this.KillBrokerProcess();
            }
            catch (Exception ex)
            {
                // Swallow exception
                TraceHelper.TraceEvent(TraceEventType.Warning, "[BrokerProcess].Close: Exception {0}", ex);
            }

            // Do not call dispose here as dispose will be called when the process exit callback triggered
        }

        /// <summary>
        /// Build a unique wait hanlde
        /// </summary>
        /// <param name="id">indicating the id</param>
        /// <param name="readyWaitHandle">output the wait handle</param>
        /// <returns>returns the name of this handle</returns>
        private static string BuildUniqueWaitHandle(int id, out EventWaitHandle readyWaitHandle, string name = null)
        {
            string handleName;
            if (id == -1)
            {
                handleName = name;
            }
            else
            {
                handleName = String.Format(Constant.InitializationWaitHandleNameFormat, id);
            }
            bool createdNew;
            readyWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset, handleName, out createdNew);
            if (!createdNew)
            {
                TraceHelper.RuntimeTrace.LogBrokerWorkerUnexpectedlyExit(id, String.Format("[BrokerProcess] Event {0} was already created by someone else.", handleName));
                if (!readyWaitHandle.Reset())
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    TraceHelper.RuntimeTrace.LogBrokerWorkerUnexpectedlyExit(id, String.Format("[BrokerProcess] Failed to reset handle {0}, Win32 Error Code = {1}.", handleName, errorCode));
                    throw new Win32Exception(errorCode);
                }
            }
            return handleName;
        }

        /// <summary>
        /// Encode environment to byte array
        /// </summary>
        /// <param name="sd">indicating the string dictionary of environments</param>
        /// <returns>returns the byte array</returns>
        public static byte[] ToByteArray(NameValueConfigurationCollection sd)
        {
            IDictionary envs = Environment.GetEnvironmentVariables();
            foreach (NameValueConfigurationElement pair in sd)
            {
                envs.Add(pair.Name, pair.Value);
            }

            string[] keys = new string[envs.Count];
            string[] values = new string[envs.Count];
            envs.Keys.CopyTo(keys, 0);
            envs.Values.CopyTo(values, 0);

            Array.Sort(keys, values, OrdinalCaseInsensitiveComparer.Default);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < envs.Count; i++)
            {

                builder.Append(keys[i]);
                builder.Append('=');
                builder.Append(values[i]);
                builder.Append('\0');
            }

            builder.Append('\0');
            return Encoding.Unicode.GetBytes(builder.ToString());
        }

        /// <summary>
        /// Callback when process exit
        /// </summary>
        /// <param name="state">indicating the state</param>
        /// <param name="timedOut">indicating whether the callback is triggered because of timeout</param>
        private void ProcessExitCallback(object state, bool timedOut)
        {
            TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerProcess] Process exit callback occured, TimedOut = {0}, PID = {1}", timedOut, this.Id);

            try
            {
                if (this.Exited != null)
                {
                    this.Exited(this, EventArgs.Empty);
                }
            }
            finally
            {
                // Dispose this object and clean up all handles
                ((IDisposable)this).Dispose();
            }
        }

        /// <summary>
        /// Callback raised when broker process is ready to initialize or time out triggered
        /// </summary>
        /// <param name="state">indicating the state</param>
        /// <param name="timedOut">indicating a value whether the callback is raised because of timeout</param>
        private void BrokerProcessReadyCallback(object state, bool timedOut)
        {
            if (this.disposed)
            {
                return;
            }

            TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerProcess] Broker process ready event triggered: Timeout = {0}, PID = {1}", timedOut, this.Id);

            if (this.Ready != null)
            {
                this.Ready(this, new BrokerProcessReadyEventArgs(timedOut));
            }
        }

        /// <summary>
        /// Dispose the object
        /// </summary>
        void IDisposable.Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose the object
        /// </summary>
        /// <param name="disposing">indicating whether it is disposing</param>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        private void Dispose(bool disposing)
        {
            // Only dispose the object once.
            // #20745, HpcBroker.exe AppVerifier stops, incorrect object type for handle.
            if (Interlocked.CompareExchange(ref this.disposedFlag, 1, 0) != 0)
            {
                return;
            }

            if (disposing)
            {
                // Set all events as we are going to dispose all of them
                try
                {
                    // #20745, HpcBroker.exe AppVerifier stops, incorrect object type for handle.
                    // Try best effort to check the handle, but can't avoid race
                    // condition, because the broker worker process may exit after
                    // the check.
                    if (this.exitWaitHandle.SafeWaitHandle.IsClosed || this.exitWaitHandle.SafeWaitHandle.IsInvalid)
                    {
                        TraceHelper.TraceEvent(
                            TraceEventType.Warning,
                            "[BrokerProcess].Dispose: Broker process handle is already invalid or closed.");
                    }
                    else
                    {
                        this.exitWaitHandle.Set();
                    }
                }
                catch (Exception ex)
                {
                    TraceHelper.TraceEvent(TraceEventType.Warning, "[BrokerProcess].Dispose: Exception while set exitWaitHandle {0}, isDisposing = true", ex);
                }

                try
                {
                    this.processExitAndEventFinishedWaitHandle.Set();
                }
                catch (Exception ex)
                {
                    TraceHelper.TraceEvent(TraceEventType.Warning, "[BrokerProcess].Dispose: Exception while set processExitAndEventFinishedWaitHandle {0}, isDisposing = true", ex);
                }
            }

            lock (this.lockThis)
            {
                if (this.disposed)
                {
                    return;
                }

                if (disposing)
                {
                    try
                    {
                        this.readyWaitHandle.Close();
                    }
                    catch (Exception ex)
                    {
                        // Swallow all exceptions here
                        TraceHelper.TraceEvent(TraceEventType.Warning, "[BrokerProcess].Dispose: Exception {0}", ex);
                    }

                    try
                    {
                        // #20745, HpcBroker.exe AppVerifier stops, incorrect object type for handle.
                        // Try best effort to check the handle, but can't avoid race
                        // condition, because the broker worker process may exit after
                        // the check.
                        if (this.exitWaitHandle.SafeWaitHandle.IsClosed || this.exitWaitHandle.SafeWaitHandle.IsInvalid)
                        {
                            TraceHelper.TraceEvent(
                                TraceEventType.Warning,
                                "[BrokerProcess].Dispose: Broker process handle is already invalid or closed.");
                        }
                        else
                        {
                            this.exitWaitHandle.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Swallow all exceptions here
                        TraceHelper.TraceEvent(TraceEventType.Warning, "[BrokerProcess].Dispose: Exception {0}", ex);
                    }

                    try
                    {
                        this.processExitAndEventFinishedWaitHandle.Close();
                    }
                    catch (Exception ex)
                    {
                        // Swallow all exceptions here
                        TraceHelper.TraceEvent(TraceEventType.Warning, "[BrokerProcess].Dispose: Exception {0}", ex);
                    }
                }

                try
                {
                    if (this.environmentHandle.IsAllocated)
                    {
                        this.environmentHandle.Free();
                    }
                }
                catch (Exception ex)
                {
                    // Swallow all exceptions here
                    TraceHelper.TraceEvent(TraceEventType.Warning, "[BrokerProcess].Dispose: Exception {0}", ex);
                }

                NativeMethods.SafeCloseValidHandle(new HandleRef(this.startupInfo, this.startupInfo.hStdError));
                NativeMethods.SafeCloseValidHandle(new HandleRef(this.startupInfo, this.startupInfo.hStdInput));
                NativeMethods.SafeCloseValidHandle(new HandleRef(this.startupInfo, this.startupInfo.hStdOutput));
                NativeMethods.SafeCloseValidHandle(new HandleRef(this.processInfo, this.processInfo.hProcess));
                NativeMethods.SafeCloseValidHandle(new HandleRef(this.processInfo, this.processInfo.hThread));

                this.disposed = true;
            }
        }

        /// <summary>
        /// Provide ordinal case insensitive string comparer
        /// </summary>
        private class OrdinalCaseInsensitiveComparer : IComparer
        {
            /// <summary>
            /// Gets the comparer
            /// </summary>
            internal static readonly OrdinalCaseInsensitiveComparer Default = new OrdinalCaseInsensitiveComparer();

            /// <summary>
            /// Compare two strings
            /// </summary>
            /// <param name="a">indicating one string</param>
            /// <param name="b">indicating another string</param>
            /// <returns>a integer indicating the result of comparation</returns>
            public int Compare(object a, object b)
            {
                string str = a as string;
                string str2 = b as string;
                if ((str != null) && (str2 != null))
                {
                    return String.CompareOrdinal(str.ToUpperInvariant(), str2.ToUpperInvariant());
                }

                return Comparer.Default.Compare(a, b);
            }
        }
    }
}
