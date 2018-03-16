using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
	class SmartClient : ClusterClientBase
	{
		public SmartClient(string[] replicaAddresses) : base(replicaAddresses)
		{
		}

		public IEnumerable<string> RandomizeReplicas()
		{
			var replicasToList = ReplicaAddresses.ToList();
			while (replicasToList.Count > 0)
			{
				var uri = replicasToList[new Random().Next(replicasToList.Count)];
				replicasToList.Remove(uri);
				yield return uri;
			}
		}

		public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
		{
			var newTimeout = TimeSpan.FromMilliseconds((double)timeout.TotalMilliseconds / ReplicaAddresses.Length);
			return (await WaitForAnyNonFaultedTaskAsync(
				ReplicaAddresses.Select(replica => Task.Run(async () =>
					{
						var webRequest = CreateRequest(replica + "?query=" + query);
						Log.InfoFormat("Processing {0}", webRequest.RequestUri);
						var resultTask = ProcessRequestAsync(webRequest);
						await Task.WhenAny(resultTask, Task.Delay(newTimeout));
						//Console.WriteLine("1" + resultTask.IsCompleted);
						if (resultTask.IsCompleted)
							return resultTask.Result;
						//Console.WriteLine("2" + resultTask.IsCompleted);
						await Task.WhenAny(resultTask);
						//Console.WriteLine("3" + resultTask.IsCompleted);
						return resultTask.Result;
					})).ToArray()))?.Result;
			var bigTask = await WaitForAnyNonFaultedTaskAsync(Task.Run(async () =>
			{

				foreach (var replica in RandomizeReplicas())
				{
					var webRequest = CreateRequest(replica + "?query=" + query);
					Log.InfoFormat("Processing {0}", webRequest.RequestUri);
					var resultTask = ProcessRequestAsync(webRequest);
					await Task.WhenAny(resultTask, Task.Delay(newTimeout));

					if (!resultTask.IsFaulted && resultTask.IsCompleted)
					{
						return resultTask.Result;
					}
					
				}
				throw new Exception("No Replica Answered");
			}));

			if (bigTask == null || bigTask.IsFaulted)
				return null;
			return bigTask.Result;

		}


		protected override ILog Log => LogManager.GetLogger(typeof(RoundRobinClient));
	}
}