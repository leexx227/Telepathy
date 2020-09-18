using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Microsoft.Telepathy.Frontend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppContext.SetSwitch(
                "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    int port = args.Length > 0 ? int.Parse(args[0]) : 5005;
                    webBuilder.ConfigureKestrel(options =>
                    {
                        // Setup a HTTP/2 endpoint without TLS.
                        options.ListenAnyIP(port, o => o.Protocols = 
                            HttpProtocols.Http2);
                    });
                    webBuilder.UseStartup<Startup>();
                   
                    //webBuilder.UseUrls($"http://0.0.0.0:{port}");
                });
    }
}
