# Azure Functions Consumption Plan Cost Billing Estimate

This code sample implements the Azure functions [Consumption Plan Cost Billing FAQ](https://github.com/Azure/Azure-Functions/wiki/Consumption-Plan-Cost-Billing-FAQ).

## How do we estimate?
The code uses the [Azure Monitor API](https://docs.microsoft.com/en-us/rest/api/monitor) to collect metrics for the last hour, multiply to estimate a full month and calculate the price using [retail price](https://azure.microsoft.com/en-us/pricing/details/functions).
The assumption is that the functions load in that hour will keep the same trend throughout the month. 

## What do we need?
1. Function App resoure id
2. Tenant ID
3. Client ID (service principle ID)
4. Service Principle secret
5. Subscription ID
