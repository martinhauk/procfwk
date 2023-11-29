using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using mrpaulandrew.azure.procfwk.Helpers;
using mrpaulandrew.azure.procfwk.Services;
using Newtonsoft.Json;

namespace mrpaulandrew.azure.procfwk
{
    public class GetActivityErrors
    {
        private readonly ILogger _logger;

        public GetActivityErrors(ILogger<GetActivityErrors> logger)
        {
            _logger = logger;
        }

        [Function("GetActivityErrors")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest httpRequest)
        {
            _logger.LogInformation("GetActivityErrors Function triggered by HTTP request.");
            _logger.LogInformation("Parsing body from request.");
            PipelineRunRequest request = await new BodyReader(httpRequest).GetRunRequestBodyAsync();
            request.Validate(_logger);

            using (var service = PipelineService.GetServiceForRequest(request, _logger))
            {
                PipelineErrorDetail result = service.GetPipelineRunActivityErrors(request);
                _logger.LogInformation("GetActivityErrors Function complete.");
                return new OkObjectResult(JsonConvert.SerializeObject(result));
            }
        }
    }
}
