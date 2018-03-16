using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
    public abstract class ClusterClientBase
    {
        protected string[] ReplicaAddresses { get; set; }

        protected ClusterClientBase(string[] replicaAddresses)
        {
            ReplicaAddresses = replicaAddresses;
        }

        public abstract Task<string> ProcessRequestAsync(string query, TimeSpan timeout);
        protected abstract ILog Log { get; }

        protected static HttpWebRequest CreateRequest(string uriStr)
        {
            var request = WebRequest.CreateHttp(Uri.EscapeUriString(uriStr));
            request.Proxy = null;
            request.KeepAlive = true;
            request.ServicePoint.UseNagleAlgorithm = false;
            request.ServicePoint.ConnectionLimit = 100500;
            return request;
        }

        protected async Task<string> ProcessRequestAsync(WebRequest request)
        {
            var timer = Stopwatch.StartNew();
            using (var response = await request.GetResponseAsync())
            {
                var result = await new StreamReader(response.GetResponseStream(), Encoding.UTF8).ReadToEndAsync();
                Log.InfoFormat("Response from {0} received in {1} ms", request.RequestUri, timer.ElapsedMilliseconds);
                return result;
            }
        }
        protected static async Task<Task<string>> WaitForAnyNonFaultedTaskAsync(params Task<string>[] tasks)
        {
            IList<Task<string>> customTasks = tasks.ToList();
            Task<string> completedTask;
            do
            {
                completedTask = await Task.WhenAny(customTasks);
                customTasks.Remove(completedTask);
            } while (completedTask.IsFaulted && customTasks.Count > 0);

            return completedTask.IsFaulted ? null : completedTask;
        }
        protected static async Task<Task<string>> WaitForTasksAsync(params Task<string>[] tasks)
        {
            IList<Task<string>> customTasks = tasks.ToList();
            Task<string> completedTask;
            do
            {
                completedTask = await Task.WhenAny(customTasks);
                if (completedTask.IsFaulted)
                    customTasks.Remove(completedTask);
                //Console.WriteLine(completedTask.Status);
            } while (!completedTask.IsCompleted && customTasks.Count > 0);

            return completedTask.IsFaulted ? null : completedTask;
        }
    }
}