using Microsoft.Extensions.Hosting;

namespace mrpaulandrew.azure.procfwk
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .Build();

            host.Run();
        }
    }
}