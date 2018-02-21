using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
	class RoundRobinClient : ClusterClientBase
	{
		public RoundRobinClient(string[] replicaAddresses) : base(replicaAddresses)
		{
		}


		public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
		{
			var newTimeout = TimeSpan.FromMilliseconds((double) timeout.Milliseconds / ReplicaAddresses.Length);
			return (Task.Run(async () =>
			{
				var result = new List<string>();
				foreach (var replica in RandomizeReplicas())
				{
					var webRequest = CreateRequest(replica + "?query=" + query);
					Log.InfoFormat("Processing {0}", webRequest.RequestUri);
					var resultTask = ProcessRequestAsync(webRequest);
					await Task.WhenAny(resultTask, Task.Delay(newTimeout));
					if (!resultTask.IsCompleted)
						continue;
					return resultTask.Result;
				}
			}));
		}

		private IEnumerable<string> RandomizeReplicas()
		{
			var rnd = new Random();
			return ReplicaAddresses.OrderBy(x => rnd.Next());
		}

		protected override ILog Log => LogManager.GetLogger(typeof(RoundRobinClient));
	}
}