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
    public class ValidatePipeline
    {
        private readonly ILogger _logger;

        public ValidatePipeline(ILogger<ValidatePipeline> logger)
        {
            _logger = logger;
        }

        [Function("ValidatePipeline")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest httpRequest)
        {
            _logger.LogInformation("ValidatePipeline Function triggered by HTTP request.");
            _logger.LogInformation("Parsing body from request.");
            PipelineRequest request = await new BodyReader(httpRequest).GetRequestBodyAsync();
            request.Validate(_logger);

            using (var service = PipelineService.GetServiceForRequest(request, _logger))
            {
                PipelineDescription result = service.ValidatePipeline(request);
                _logger.LogInformation("ValidatePipeline Function complete.");
                return new OkObjectResult(JsonConvert.SerializeObject(result));
            }
        }
    }
}
