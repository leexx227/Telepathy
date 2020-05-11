// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Internal.ContainerizedBrokerLauncher
{
    using Microsoft.Telepathy.Session;
    using Microsoft.Telepathy.Session.Interface;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceModel;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Telepathy.Common.TelepathyContext;
    using System.Diagnostics;
    using System.Security.Permissions;
    using System.Threading;
    using Microsoft.Telepathy.RuntimeTrace;
    using Microsoft.Telepathy.Session.Common;
    using Microsoft.Telepathy.Session.Exceptions;
    using Microsoft.Telepathy.Session.Internal;
    using Microsoft.Telepathy.ServiceBroker;
    using k8s;
    using System.Net;

    /// <summary>
    /// Implementation of containerized broker launcher
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, IncludeExceptionDetailInFaults = true,
        Name = "BrokerLauncher", Namespace = "http://hpc.microsoft.com/brokerlauncher/")]
    internal class ContainerizedBrokerLauncher : IBrokerLauncher, IDisposable
    {
        /// <summary>
        /// Stores the broker manager
        /// </summary>
        private ContainerizedBrokerManager brokerManager;

        /// <summary>
        /// the flag to handle online/offline
        /// </summary>
        private bool AllowNewSession = true;

        /// <summary>
        /// How often to check whether graceful offline is complete
        /// </summary>
        const int gracefulOfflineCheckInterval = 5000;

        /// <summary>
        /// Lock for instance member vars
        /// </summary>
        private object instanceLock = new object();

        /// <summary>
        /// Stores the job object
        /// </summary>
        private JobObject job;

        /// <summary>
        /// Stores the fabric client;
        /// </summary>
        private ITelepathyContext context;

        /// <summary>
        /// Initializes a new instance of the BrokerLauncher class
        /// </summary>
        [SecurityPermission(SecurityAction.Demand)]
        public ContainerizedBrokerLauncher(bool managementOperationsOnly, ITelepathyContext context)
        {
            this.context = context;

            // Initializes the job object
            this.job = new JobObject();

            // Assign broker launcher process to the job object
            this.job.Assign(Process.GetCurrentProcess());

            // If this instance of HpcBroker service should handle mgmt and app operations
            if (!managementOperationsOnly)
            {
                // Force the broker mananger to intialize when the service is online
                this.brokerManager = new ContainerizedBrokerManager(this.IsOnline, context);
            }
        }

        public BrokerInitializationResult Attach(string sessionId)
        {
            throw new NotImplementedException();
        }

        public void Close(string sessionId)
        {
            try
            {
                bool? isAadOrLocalUser = this.brokerManager.IfSessionCreatedByAadOrLocalUser(sessionId);
                string brokerName = null;
                if (!isAadOrLocalUser.HasValue)
                {
                    TraceHelper.TraceEvent(sessionId, System.Diagnostics.TraceEventType.Warning, "[BrokerLauncher] Info not found: SessionId = {0}", sessionId);
                    TraceHelper.TraceEvent(sessionId, System.Diagnostics.TraceEventType.Warning, "[BrokerLauncher] Try to find brokerworker name from Redis: SessionId = {0},", sessionId);
                    brokerName = context.Registry.GetValueAsync<string>(null, "brokername" + sessionId, new CancellationToken()).Result;
                    TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerLauncher] The unique brokername in Redis is {0}.", brokerName);
                    try
                    {
                        IKubernetes client = context.ClusterContext.GetClusterClient<IKubernetes>().Result;
                        client.ReadNamespacedStatefulSet(brokerName, "telepathy");
                    }
                    catch (Microsoft.Rest.HttpOperationException httpOperationException)
                    {
                        var code = httpOperationException.Response.StatusCode;
                        if (code == HttpStatusCode.NotFound)
                        {
                            // Only the broker worker pods have been deleted to return
                            TraceHelper.TraceEvent(TraceEventType.Information, "[BrokerLauncher] {0} can't be found in AKS, it has been deleted before.", brokerName);
                            return;
                        }
                    }
                }
                this.CheckAccess(sessionId);
                TraceHelper.TraceEvent(sessionId, System.Diagnostics.TraceEventType.Information, "[BrokerLauncher] Close: SessionId = {0}", sessionId);
                this.brokerManager.CloseBrokerDomain(sessionId, brokerName).GetAwaiter().GetResult();

                #region Debug Failure Test
                SimulateFailure.FailOperation(1);
                #endregion

                TraceHelper.TraceEvent(sessionId, System.Diagnostics.TraceEventType.Information, "[BrokerLauncher] Close Broker {0} Succeeded.", sessionId);
            }
            catch (Exception e)
            {
                // Bug 14019: Swallow the exception for close as the broker node is already taken offline
                if (!this.AllowNewSession)
                {
                    return;
                }

                TraceHelper.TraceEvent(sessionId, System.Diagnostics.TraceEventType.Error, "[BrokerLauncher] Close Broker {0} failed: {1}", sessionId, e);
                throw ExceptionHelper.ConvertExceptionToFaultException(e);
            }
        }

        public void Close()
        {
            try
            {
                this.Dispose();
            }
            catch (Exception e)
            {
                TraceHelper.TraceEvent(TraceEventType.Critical, "[BrokerLauncher] Dispose failed: {0}", e);
            }
        }

        public BrokerInitializationResult Create(SessionStartInfoContract info, string sessionId)
        {
            if ((!this.AllowNewSession) || (!this.IsOnline && String.IsNullOrEmpty(info.DiagnosticBrokerNode)))
            {
                ThrowHelper.ThrowSessionFault(SOAFaultCode.Broker_BrokerIsOffline, SR.BrokerIsOffline);
            }

            // Handle invalid input parameters
            try
            {
                ParamCheckUtility.ThrowIfNull(info, "info");
                if (!BrokerLauncherEnvironment.Standalone)
                {
                    this.CheckAccess(sessionId);
                }

                TraceHelper.TraceEvent(sessionId, System.Diagnostics.TraceEventType.Information, "[BrokerLauncher] Create: SessionId = {0}", sessionId);
                //TODO: make it async
                BrokerInitializationResult returnValue = this.brokerManager.CreateNewBrokerDomain(info, sessionId, false).GetAwaiter().GetResult();

                #region Debug Failure Test
                SimulateFailure.FailOperation(1);
                #endregion

                TraceHelper.RuntimeTrace.LogSessionCreated(sessionId);
                TraceHelper.TraceEvent(sessionId, System.Diagnostics.TraceEventType.Information, "[BrokerLauncher] Create Broker {0} Succeeded.", sessionId);
                return returnValue;
            }
            catch (Exception e)
            {
                TraceHelper.RuntimeTrace.LogFailedToCreateSession(sessionId);

                // Bug 10614: Throw a proper exception when the broker node is being taken offline
                if ((!this.AllowNewSession) || (!this.IsOnline && String.IsNullOrEmpty(info.DiagnosticBrokerNode)))
                {
                    ThrowHelper.ThrowSessionFault(SOAFaultCode.Broker_BrokerIsOffline, SR.BrokerIsOffline);
                }

                TraceHelper.TraceEvent(sessionId, System.Diagnostics.TraceEventType.Error, "[BrokerLauncher] Create Broker {0} failed: {1}", sessionId, e.ToString());
                throw ExceptionHelper.ConvertExceptionToFaultException(e);
            }
        }

        public BrokerInitializationResult CreateDurable(SessionStartInfoContract info, string sessionId)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void CheckAccess(string sessionId)
        {
            // TODO: implement authentication logic
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.brokerManager != null)
                {
                    // BrokerManager.Dispose() will not throw exceptions
                    this.brokerManager.Close();
                    this.brokerManager = null;
                }

                if (this.CheckOfflineState != null)
                {
                    this.CheckOfflineState.Dispose();
                    this.CheckOfflineState = null;
                }
            }
        }

        public string[] GetActiveBrokerIdList()
        {
            throw new NotImplementedException();
        }

        public bool PingBroker(string sessionID)
        {
            throw new NotImplementedException();
        }

        public string PingBroker2(string sessionID)
        {
            throw new NotImplementedException();
        }

        #region Online/Offline

        /// <summary>
        /// Timer to monitor graceful offlone
        /// </summary>
        private Timer CheckOfflineState;

        /// <summary>
        /// Whether the broker is online or offline
        /// </summary>
        /// TODO: Should this be init from reg?
        private bool isOnline = true;

        /// <summary>
        /// Whether the broker is online or offline
        /// </summary>
        internal bool IsOnline
        {
            get
            {
                return this.isOnline;
            }
        }

        /// <summary>
        /// Bring broker online
        /// </summary>
        internal void Online()
        {
            // Check if management command should be redirected to another HpcBroker on the same node (needed
            // for failover BN)
            if (!ShouldRedirectManagementCommand())
            {
                if (!this.IsOnline)
                {
                    this.brokerManager.Close();
                    // Force the broker mananger to intialize when the service is online
                    this.brokerManager = new ContainerizedBrokerManager(true, this.context);
                }

                // If not handle command in this instance
                // Allow new sessions 
                this.AllowNewSession = true;

                // Cleanup up previous timer. Need lock in case user cancels offline timer callback is in progress
                lock (this.instanceLock)
                {
                    if (this.CheckOfflineState != null)
                    {
                        this.CheckOfflineState.Dispose();
                        this.CheckOfflineState = null;
                    }
                }
            }
            else
            {
                // If command should be redirected, redirect online synchronously
                RedirectManagementCommand(delegate (BrokerManagementClient client) { client.Online(); });
            }

            this.isOnline = true;
        }

        /// <summary>
        /// Bring broker offline gracefully
        /// </summary>
        /// <returns>WaitHandle signalled when offline completes</returns>
        internal EventWaitHandle StartOffline(bool force)
        {
            AutoResetEvent finish = new AutoResetEvent(false);

            // Check if management command should be redirected to another HpcBroker on the same node (needed
            // for failover BN)
            if (!ShouldRedirectManagementCommand())
            {
                // If not handle command in this instance
                // Prevent new sessions
                this.AllowNewSession = false;

                if (force)
                {
                    // Dispose all the broker node
                    this.brokerManager.Close();

                    // Signal event now since we are now offline
                    this.isOnline = false;

                    finish.Set();
                }
                else
                {
                    // Watch for existing sessions to complete
                    this.CheckOfflineState = new Timer(new TimerCallback(this.CheckOfflineStateCallback), finish, 0, gracefulOfflineCheckInterval);
                }
            }
            else
            {
                // If command should be redirected, redirect online 
                if (!RedirectManagementCommand(delegate (BrokerManagementClient client) { client.StartOffline(force); }))
                {
                    // If connection to another local HpcBroker instance failed, assume its offline on this node
                    this.isOnline = false;
                    finish.Set();
                }
                else
                {
                    // wait for offline to complete
                    this.CheckOfflineState = new Timer(new TimerCallback(this.CheckOfflineStateCallback), finish, 0, gracefulOfflineCheckInterval);
                }
            }

            return finish;
        }

        /// <summary>
        /// Timer callback for local graceful offline check
        /// </summary>
        /// <param name="finishEvent"></param>
        private void CheckOfflineStateCallback(object finishEvent)
        {
            lock (this.instanceLock)
            {
                if (this.CheckOfflineState != null)
                {
                    bool isOffline = false;

                    if (!ShouldRedirectManagementCommand())
                    {
                        isOffline = this.brokerManager.BrokerCount == 0;

                        if (isOffline)
                        {
                            this.brokerManager.Close();
                            this.brokerManager = new ContainerizedBrokerManager(false, this.context);
                        }
                    }
                    else
                    {
                        if (!RedirectManagementCommand(delegate (BrokerManagementClient client) { isOffline = client.IsOffline(); }))
                        {
                            // If connection to another local HpcBroker instance failed, assume its offline on this node
                            isOffline = true;
                        }
                    }

                    if (isOffline)
                    {
                        // Close timer once all brokers ended
                        this.CheckOfflineState.Dispose();
                        this.CheckOfflineState = null;

                        // Signal that offline complete
                        this.isOnline = false;

                        ((AutoResetEvent)finishEvent).Set();
                    }
                }
            }
        }

        /// <summary>
        /// Whether to redirect the management command to an instance of HpcBroker running in a FC resource group
        /// TODO: Consider caching 
        /// </summary>
        /// <returns></returns>
        private static bool ShouldRedirectManagementCommand()
        {
            // If this HpcBroker instance is running as a Windows service and on a failover BN, redirect
            return !Hpc.Scheduler.Session.Internal.LauncherHostService.LauncherHostService.IsConsoleApplication
                        && Win32API.IsFailoverBrokerNode();
        }

        /// <summary>
        /// Redirects management commands t broker management service
        /// </summary>
        /// <param name="managementCommand">management command to redirect</param>
        /// <param name="managementCommandParam">param to management command</param>
        private static bool RedirectManagementCommand(Action<BrokerManagementClient> managementCommand)
        {
            Debug.Assert(OperationContext.Current == null, "BrokerManagement service should not redirect commands");

            bool clientConnectionFailed = false;

            try
            {
                BrokerManagementClient client = null;

                try
                {
                    client = new BrokerManagementClient();

                    // If connection to local BrokerManager service fails, there is very likely no broker
                    // resource group running on this machine (i.e. this machine is a standby). Eat
                    // the exception and allow pause to succeed
                    try
                    {
                        client.Open();
                    }
                    catch (Exception ex)
                    {
                        clientConnectionFailed = true;
                        TraceHelper.TraceEvent(TraceEventType.Warning, "[BrokerLauncher].RedirectManagementCommand: Exception {0}", ex);
                    }

                    if (!clientConnectionFailed)
                    {
                        managementCommand(client);
                    }
                }

                finally
                {
                    if (client != null && client.State != CommunicationState.Faulted)
                    {
                        try
                        {
                            client.Close();
                        }
                        catch (Exception ex)
                        {
                            TraceHelper.TraceEvent(TraceEventType.Warning, "[BrokerLauncher].RedirectManagementCommand: Exception {0}", ex);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // For any other exception, log it and rethrow
                TraceHelper.TraceEvent(TraceEventType.Error, "Redirecting mgmt command to local broker resource group failed  - {0}", e);
                throw;
            }

            // Return whether client connected to another local HpcBroker instance
            return !clientConnectionFailed;
        }

        #endregion
    }
}
