using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Telepathy.HostAgent.Core
{
    public class EnvironmentParser
    {
        public EnvironmentParser()
        {

        }

        public void TryGetEnvironmentVariable<T>(string variable, ref T result)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    Type type = typeof(T);
                    result = (T)Convert.ChangeType(value, type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) ? Nullable.GetUnderlyingType(typeof(T)) : typeof(T));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Input Error]: Environment variable: {variable} value: {value} cannot be parse.");
                    Trace.TraceError($"[Input Error]: Environment variable: {variable} value: {value} cannot be parse.");
                }

            }
        }
    }
}
