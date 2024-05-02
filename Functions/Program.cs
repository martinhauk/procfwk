using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace mrpaulandrew.azure.procfwk
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .ConfigureServices(services =>
                {
                    services.Configure<KestrelServerOptions>(options =>
                    {
                        options.AllowSynchronousIO = true;
                    });
                })
                //.ConfigureLogging((ILoggingBuilder b) => { b.AddConsole(); })
                .Build();

            host.Run();
        }
    }
}