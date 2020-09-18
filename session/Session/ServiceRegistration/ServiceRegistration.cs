// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session.ServiceRegistration
{
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    ///   <para>Represents the service registration information.</para>
    /// </summary>
    public sealed class ServiceRegistration
    {
        public int ServiceTimeout { set; get; }

        public int ServiceConcurrency { set; get; }

        public int PrefetchCount { set; get; }

        public string ServiceFullPath { set; get; }

        public int ServiceInitializeTimeout { set; get; }

        public string ServiceLanguage { set; get; }

        public Dictionary<string, string> EnvironmentVariables { set; get; }

        public static ServiceRegistration GetServiceConfigurations(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                var serviceConfigurations = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<ServiceRegistration>(serviceConfigurations);
            }
        }
    }
}
