

namespace mrpaulandrew.azure.procfwk.Helpers
{
    public class PipelineRequestDTO : PipelineRequest
    {
        public string CheckOrchestratorInstanceId { get; set; }

        public PipelineRequestDTO(PipelineRequest request, string checkOrchestratorInstanceId)
        {
            TenantId = request?.TenantId;
            ApplicationId = request?.ApplicationId;
            AuthenticationKey = request?.AuthenticationKey;
            SubscriptionId = request?.SubscriptionId;
            ResourceGroupName = request?.ResourceGroupName;
            OrchestratorType = request?.OrchestratorType;
            OrchestratorName = request?.OrchestratorName;
            PipelineName = request?.PipelineName;
            OrchestratorType = request?.OrchestratorType;
            PipelineParameters = request?.PipelineParameters;
            CheckOrchestratorInstanceId = checkOrchestratorInstanceId;
        }
    }
}