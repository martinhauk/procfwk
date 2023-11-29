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
    public class CancelPipeline
    {
        private readonly ILogger _logger;

        public CancelPipeline(ILogger<CancelPipeline> logger)
        {
            _logger = logger;
        }

        [Function("CancelPipeline")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest httpRequest)
        {
            _logger.LogInformation("CancelPipeline Function triggered by HTTP request.");
            _logger.LogInformation("Parsing body from request.");
            PipelineRunRequest request = await new BodyReader(httpRequest).GetRunRequestBodyAsync();
            request.Validate(_logger);

            using (var service = PipelineService.GetServiceForRequest(request, _logger))
            {
                PipelineRunStatus result = service.CancelPipeline(request);
                _logger.LogInformation("CancelPipeline Function complete.");
                return new OkObjectResult(JsonConvert.SerializeObject(result));
            }
        }
    }
}
