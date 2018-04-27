using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// Azure Management dependencies
using Microsoft.Rest.Azure.Authentication;
using Microsoft.Rest.Azure.OData;

// These examples correspond to the Monitor .Net SDK versions >= 0.18.0-preview
// Those versions include the multi-dimensional metrics API, which works with the previous single-dimensional metrics API too.
using Microsoft.Azure.Management.Monitor;
using Microsoft.Azure.Management.Monitor.Models;
using System.IO;

namespace AzureMonitor
{
    class Program
    {
        private static MonitorClient readOnlyClient;
        const double mil = 1000000;
        const double gig = 1024000;
        const double freeGB = 400000;
        const double executionsPrice = 0.2;
        const double unitsPrice = 0.000016;


        static void Main(string[] args)
        {        
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            var secret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            if (new List<string> { tenantId, clientId, secret, subscriptionId }.Any(i => String.IsNullOrEmpty(i)))
            {
                Console.WriteLine("Please provide environment variables for AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_CLIENT_SECRET and AZURE_SUBSCRIPTION_ID.");
            }
            else
            {
                readOnlyClient = AuthenticateWithReadOnlyClient(tenantId, clientId, secret, subscriptionId).Result;
                var resourceId = args[0];

                CalculateFunctionAppCost(readOnlyClient, resourceId, @"C:\temp\FunctionCost.txt");                                

                Console.Read();
            }        
        }

        private static void CalculateFunctionAppCost(MonitorClient readOnlyClient, string resourceId, string outputFilename)
        {
            double execUnitsLastHour = ListMetrics(readOnlyClient, resourceId, "FunctionExecutionUnits").Result;
            double execCountLastHour = ListMetrics(readOnlyClient, resourceId, "FunctionExecutionCount").Result;

            // calc pricing - currnetly hard coded (until usage of RateCard API will be possible) 
            // using retail pricing https://azure.microsoft.com/en-us/pricing/details/functions/ 
            // exec count = Total Executions
            // exec units = Execution Time

            double execCountMonth = execCountLastHour * 24 * 30;
            double totalExecs = (execCountMonth > mil) ? ((execCountMonth - mil) / mil) * executionsPrice : 0;

            execUnitsLastHour = execUnitsLastHour / gig; // convert to GB
            double execUnitMonth = execUnitsLastHour * 24 * 30;
            double execTime = (execUnitMonth > freeGB) ? (execUnitMonth - freeGB) * unitsPrice : 0;

            double totalCost = totalExecs + execTime;

            Console.WriteLine("Toal Price: " + totalCost + "\n");
            Console.WriteLine("Metric\tHour\tMonth\tCost");
            Console.WriteLine(string.Format("{0}\t{1}\t{2}\t{3}", "FunctionExecutionUnits", execUnitsLastHour, execUnitMonth, execTime));
            Console.WriteLine(string.Format("{0}\t{1}\t{2}\t{3}", "FunctionExecutionCount", execCountLastHour, execCountMonth, totalExecs));

            string[] lines = {
                    "===============================================================================================",
                    DateTime.Now.ToString(),
                    "Toal Price: " + totalCost,
                    "Metric\tHour\tMonth\tCost",
                    string.Format("{0}\t{1}\t{2}\t{3}", "FunctionExecutionUnits", execUnitsLastHour, execUnitMonth, execTime),
                    string.Format("{0}\t{1}\t{2}\t{3}", "FunctionExecutionCount", execCountLastHour, execCountMonth, totalExecs)
                };
            WriteToFile(outputFilename, lines);
        }

        private static void WriteToFile(string filename, string[] lines)
        {            
            // This text is added only once to the file.
            if (!File.Exists(filename))
            {
                // Create a file to write to.
                using (StreamWriter sw = File.CreateText(filename))
                {
                    foreach (string line in lines)
                    {
                        sw.WriteLine(line);                        
                    }
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filename))
                {
                    foreach (string line in lines)
                    {
                        sw.WriteLine(line);
                    }
                }
            }          
        }

        private static async Task<MonitorClient> AuthenticateWithReadOnlyClient(string tenantId, string clientId, string secret, string subscriptionId)
        {
            // Build the service credentials and Monitor client
            var serviceCreds = await ApplicationTokenProvider.LoginSilentAsync(tenantId, clientId, secret);
            var monitorClient = new MonitorClient(serviceCreds);
            monitorClient.SubscriptionId = subscriptionId;

            return monitorClient;
        }
        
        private static async Task<double> ListMetrics(MonitorClient readOnlyClient, string resourceUri, string metricName)
        {
            // The timespan is the concatenation of the start and end date/times separated by "/"
            string startDate = DateTime.Now.AddHours(-1).ToString("o");
            string endDate = DateTime.Now.ToString("o");
            string timeSpan = startDate + "/" + endDate;

            Response metrics = await readOnlyClient.Metrics.ListAsync(resourceUri: resourceUri, 
                metric: metricName, 
                aggregation: "Total", 
                timespan: timeSpan,
                interval: TimeSpan.FromMinutes(1),
                resultType: ResultType.Data,
                cancellationToken: CancellationToken.None);

            double result = 0;

            foreach (var metric in metrics.Value)
            {
                foreach (TimeSeriesElement element in metric.Timeseries)
                {
                    foreach (MetricValue data in element.Data)
                    {
                        result += (double)data.Total;
                    }
                }
            }

            return result;            
        }             
    }
}
