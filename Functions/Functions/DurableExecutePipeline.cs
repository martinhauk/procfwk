using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using mrpaulandrew.azure.procfwk.Helpers;
using mrpaulandrew.azure.procfwk.Services;
using Newtonsoft.Json;

namespace mrpaulandrew.azure.procfwk.Functions
{
  public static class DurableExecutePipeline
  {
    [FunctionName("DurableExecutePipeline")]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger logger)
    {
      logger.LogInformation("Parsing body from request.");

      var request = context.GetInput<PipelineRequest>();

      request.Validate(logger);

      using (var service = PipelineService.GetServiceForRequest(request, logger))
      {
        PipelineRunStatus result = service.ExecutePipeline(request);
        logger.LogInformation("ExecutePipeline Function complete.");
        context.SetOutput(result);
      }
    }

    [FunctionName("DurableExecutePipeline_HttpStart")]
    public static async Task<HttpResponseMessage> HttpStart(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger logger)
    {
      logger.LogInformation("DurableExecutePipeline_HttpStart Function triggered by HTTP request.");

      logger.LogInformation("Parsing body from request.");

      string requestBodyAsString = await req.Content.ReadAsStringAsync();
      PipelineRequest pipelineRequest = await new BodyReader(requestBodyAsString).GetRequestBodyAsync();

      logger.LogInformation("Handing over request to orchestrator DurableExecutePipeline.");

      string instanceId = await starter.StartNewAsync("DurableExecutePipeline", pipelineRequest);

      logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");

      return starter.CreateCheckStatusResponse(req, instanceId);
    }
  }
}
