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

        #region Start

        [FunctionName("DurableExecutePipeline_Orchestrator_Start")]
        public static async Task<string> DurableExecutePipeline_Orchestrator_Start(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger logger)
        {
            var request = context.GetInput<PipelineRequest>();

            logger.LogInformation("DurableExecutePipeline_Orchestrator_Start - calling Activity to start pipeline '{pipelineName}'", request.PipelineName);

            var startResponse = await context.CallActivityAsync<PipelineRunStatus>("DurableExecutePipeline_ActivityFunction_Start", request);

            logger.LogInformation("DurableExecutePipeline_Orchestrator_Start - started pipeline '{pipelineName}' with run id '{runId}'", request.PipelineName, startResponse.RunId);

            return startResponse.RunId;
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
            var request = context.GetInput<PipelineRunRequest>();

            logger.LogInformation("DurableExecutePipeline_Orchestrator_Check - monitoring pipeline '{pipelineName}' with run id '{runId}'", request.PipelineName, request.RunId);

            PipelineRunStatus result;
            do
            {
                var nextCheck = context.CurrentUtcDateTime.AddSeconds(internalWaitDuration);
                await context.CreateTimer(nextCheck, CancellationToken.None);

                result = await context.CallActivityAsync<PipelineRunStatus>("DurableExecutePipeline_ActivityFunction_Check", request);

            } while (result.ActualStatus == "InProgress" || result.ActualStatus == "Queued");

            logger.LogInformation("DurableExecutePipeline_Orchestrator_Check - Pipeline '{pipelineName}' with run id '{runId}' has finished with status '{pipelineStatus}'", request.PipelineName, request.RunId, result.ActualStatus);

            context.SetOutput(result);
        }

        [FunctionName("DurableExecutePipeline_ActivityFunction_Check")]
        public static PipelineRunStatus DurableExecutePipeline_ActivityFunction_Check(
            [ActivityTrigger] PipelineRunRequest request,
            ILogger logger)
        {
            logger.LogInformation("DurableExecutePipeline_ActivityFunction_Check - checking on pipeline '{pipelineName}' with run id '{runId}'", request.PipelineName, request.RunId);

            request.Validate(logger);

            PipelineRunStatus result;
            using (var service = PipelineService.GetServiceForRequest(request, logger))
            {
                result = service.GetPipelineRunStatus(request);
            }

            logger.LogInformation("DurableExecutePipeline_ActivityFunction_Check - checked pipeline '{pipelineName}' with run id '{runId}'. Status: '{status}'", request.PipelineName, request.RunId, result.ActualStatus);

            return result;
        }

        #endregion Check

        #region Main Orchestrator

        [FunctionName("DurableExecutePipeline_MainOrchestrator")]
        public static async Task DurableExecutePipeline_MainOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            var pipelineRequest = context.GetInput<PipelineRequest>();

            logger.LogInformation("DurableExecutePipeline_MainOrchestrator - starting orchestrator for start of pipeline '{pipelineName}'", pipelineRequest.PipelineName);

            string runId = await context.CallSubOrchestratorAsync<string>("DurableExecutePipeline_Orchestrator_Start", pipelineRequest);

            logger.LogInformation("DurableExecutePipeline_MainOrchestrator - pipeline '{pipelineName}' and run id '{runId}' started. Starting orchestrator for cyclic check...", pipelineRequest.PipelineName, runId);

            PipelineRunRequest pipelineRunRequest = new PipelineRunRequest(pipelineRequest) { RunId = runId };
            
            var retryCount = 5;
            PipelineRunStatus status = null;
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    status = await context.CallSubOrchestratorAsync<PipelineRunStatus>("DurableExecutePipeline_Orchestrator_Check", pipelineRunRequest);
                }
                catch (HttpRequestException e)
                {
                    logger.LogError(e, "DurableExecutePipeline_MainOrchestrator - Failed check on pipeline run {runId}. Retrying...", runId);
                    await Task.Delay(10000);  
                }             

                if (i == retryCount)
                {
                    var ex = new System.Exception($"DurableExecutePipeline_MainOrchestrator - Max retries exceeded for pipeline '{pipelineRequest.PipelineName}' with run id '{runId}'");
                    logger.LogError(ex, "DurableExecutePipeline_MainOrchestrator - Max retries exceeded for pipeline '{pipelineRequest.PipelineName}' with run id '{runId}'", pipelineRequest.PipelineName,  runId);
                    throw ex;
                }
            }

            logger.LogInformation("DurableExecutePipeline_MainOrchestrator - finished for pipeline'{pipelineName}' with run id '{runId}'", pipelineRequest.PipelineName, runId);

            context.SetOutput(status);
        }

        #endregion Main Orchestrator

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

            logger.LogInformation("DurableExecutePipeline_HttpStart - Handing over request for pipeline {pipelineName} to main orchestrator 'DurableExecutePipeline_MainOrchestrator'", pipelineRequest.PipelineName);

            string instanceId = await starter.StartNewAsync("DurableExecutePipeline_MainOrchestrator", pipelineRequest);

            logger.LogInformation("DurableExecutePipeline_HttpStart - Started main orchestrator 'DurableExecutePipeline_MainOrchestrator' with ID = '{instanceId}' for pipeline '{pipelineName}'.", instanceId, pipelineRequest.PipelineName);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
