using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using mrpaulandrew.azure.procfwk.Helpers;
using mrpaulandrew.azure.procfwk.Services;

namespace mrpaulandrew.azure.procfwk.Functions
{
    public static class DurableExecutePipeline
    {
        private const int internalWaitDuration = 5; //s
        private const string pipelineStartedEventName = "PipelineStartedEvent";

        #region Start

        [FunctionName("DurableExecutePipeline_Orchestrator_Start")]
        public static async Task DurableExecutePipeline_Orchestrator_Start(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger logger)
        {
            var request = context.GetInput<PipelineRequestDTO>();

            var startResponse = await context.CallActivityAsync<PipelineRunStatus>("DurableExecutePipeline_ActivityFunction_Start", request);

            await client.RaiseEventAsync(request.CheckOrchestratorInstanceId, pipelineStartedEventName, startResponse);

            logger.LogInformation("DurableExecutePipeline_Orchestrator_Start - sending event for pipeline '{pipelineName}' to instance '{instanceIdCheck}'", request.PipelineName, request.CheckOrchestratorInstanceId);

            context.SetOutput(startResponse);
        }

        [FunctionName("DurableExecutePipeline_ActivityFunction_Start")]
        public static PipelineRunStatus DurableExecutePipeline_ActivityFunction_Start(
            [ActivityTrigger] PipelineRequest request,
            ILogger logger)
        {
            logger.LogInformation("DurableExecutePipeline_ActivityFunction_Start - starting pipeline '{pipelineName}'", request.PipelineName);

            request.Validate(logger);

            using (var service = PipelineService.GetServiceForRequest(request, logger))
            {
                PipelineRunStatus result = service.StartPipeline(request);

                logger.LogInformation("DurableExecutePipeline_ActivityFunction_Start - started pipeline '{pipelineName}' with run id '{runId}'", request.PipelineName, result.RunId);

                return result;
            }
        }

        #endregion Start

        #region Check

        [FunctionName("DurableExecutePipeline_Orchestrator_Check")]
        public static async Task DurableExecutePipeline_Orchestrator_Check(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            logger.LogInformation("DurableExecutePipeline_Orchestrator_Check function waiting for event...");

            var startResponse = await context.WaitForExternalEvent<PipelineRunStatus>(pipelineStartedEventName);

            logger.LogInformation("DurableExecutePipeline_Orchestrator_Check received event, monitoring pipeline run '{runId}'", startResponse.RunId);

            var request = context.GetInput<PipelineRequest>();

            var runRequest = new PipelineRunRequest(request)
            {
                RunId = startResponse.RunId,
                OrchestratorName = request.OrchestratorName,
                ResourceGroupName = request.ResourceGroupName
            };

            PipelineRunStatus result;
            do
            {
                var nextCheck = context.CurrentUtcDateTime.AddSeconds(internalWaitDuration);
                await context.CreateTimer(nextCheck, CancellationToken.None);

                result = await context.CallActivityAsync<PipelineRunStatus>("DurableExecutePipeline_ActivityFunction_Check", runRequest);

            } while (result.ActualStatus == "InProgress" || result.ActualStatus == "Queued");

            logger.LogInformation("Pipeline run '{runId}' has finished with status '{pipelineStatus}'", startResponse.RunId, result.ActualStatus);

            context.SetOutput(result);
        }

        [FunctionName("DurableExecutePipeline_ActivityFunction_Check")]
        public static PipelineRunStatus DurableExecutePipeline_ActivityFunction_Check(
            [ActivityTrigger] PipelineRunRequest request,
            ILogger logger)
        {
            logger.LogInformation("ActivityFunction - checking on pipeline '{pipelineName}'", request.PipelineName);

            request.Validate(logger);


            PipelineRunStatus result;
            using (var service = PipelineService.GetServiceForRequest(request, logger))
            {
                result = service.GetPipelineRunStatus(request);
            }

            logger.LogInformation("ActivityFunction - done checking on pipeline '{pipelineName}'", request.PipelineName);

            return result;
        }

        #endregion Check

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



            string instanceIdCheck = await starter.StartNewAsync("DurableExecutePipeline_Orchestrator_Check", pipelineRequest);

            logger.LogInformation("Started check orchestrator with ID = '{instanceIdCheck}' for pipeline '{pipelineName}'.", instanceIdCheck, pipelineRequest.PipelineName);



            string instanceIdStart = await starter.StartNewAsync("DurableExecutePipeline_Orchestrator_Start", new PipelineRequestDTO(pipelineRequest, instanceIdCheck));

            logger.LogInformation("Started start orchestrator with ID = '{instanceIdStart}' for pipeline '{pipelineName}'.", instanceIdStart, pipelineRequest.PipelineName);



            return starter.CreateCheckStatusResponse(req, instanceIdCheck);
        }
    }
}
