# Pipeline Already Running

___
[<< Contents](/procfwk/contents) 

___

![this.running](/procfwk/this-running.png){:style="float: right;margin-left: 15px;margin-bottom: 10px;"}To improve the overall resilience of the processing framework a Data Factory check is done for each new execution to establish if the parent [pipeline](/procfwk/pipelines) is already running.

This check is important to avoid any confusion in the metadata current execution [table](/procfwk/tables) at runtime if a parent has accidently been triggered twice, or to prevent an unwanted [previous run clean up](/procfwk/prevruncleanup) cycle occurring incorrectly.

The check is done as a utility [pipeline](/procfwk/pipelines) that accepts the following pipeline parameters:

- **Pipeline Name** - the pipeline being checked if running, provided as the system variable from the parent.
- **Batch Name** - the name of the current execution batch, applicable if this feature is enabled. More details on this below.

Using the Azure Management API in conjunction with several database [property](/procfwk/properties) values the following Activities inspect the framework Data Factory pipeline runs to establish if an execution is already running (in progress/queued).

[ ![](/procfwk/activitychain-checkingforrunning.png) ](/procfwk/activitychain-checkingforrunning.png){:target="_blank"}

If an instance of the provided pipeline/batch name is already running an exception will be thrown, stopping the new instance and forcing a failure status.

The expressions and filtering take into account the new Run Id then apply filtering to the API pipeline runs results to finally assert if (using a count variable) if the target pipeline is running.

## Data Factory Permissions

For the Azure Management API calls a set of web activities are used (show above) that authenticate using Data Factory's own MSI to perform the various GET requests.

For the pipeline runs request the default permissions granted to the Data Factory MSI are not sufficient. **<span style="color:red">Data Factory must explicitly be granted access to itself in order to query its own pipeline runs.</span>** Details of the request below on the Microsoft docs page.

[https://docs.microsoft.com/en-us/rest/api/datafactory/pipelineruns/get](https://docs.microsoft.com/en-us/rest/api/datafactory/pipelineruns/get)

Hopefully, this is a short-term permissions quirk and bug that Microsoft will correct. However, currently this permissions update will need to be an additional step in deployments of the framework Data Factory.

## Batch Execution Support

The [batch execution](/procfwk/executionbatches) concept supports the running on multiple parent pipelines concurrently, therefore the above filtering had to be extended to take this into account.

Using the central property table value to establish if the batch execution feature is enabled. The filtering then uses the batch name parameter passed to the pipeline to further reduce the results before doing the final assertion. Logically the following can be stated:

- If batch execution handling is disabled.
  - Get parent pipeline runs.
    - If a parent is running. Throw exception.

- If batch execution handling is enabled.
  - Get parent pipeline runs.
    - If a parent is running **and** the batch names are the same. Throw exception.