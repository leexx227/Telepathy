
namespace SessionManager
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;
    //using Session;

    public class Program
    {
        public static void Main(string[] args)
        {
            // AppContext.SetSwitch(
            //     "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            CreateHostBuilder(args).Build().Run();
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://0.0.0.0:5001/");
                    webBuilder.UseStartup<Startup>();
                });
    }
}
