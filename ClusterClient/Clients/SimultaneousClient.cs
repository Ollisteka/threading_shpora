using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace ClusterClient.Clients
{
	public class SimultaneousClient : ClusterClientBase
	{
		private readonly Random random = new Random();

		public SimultaneousClient(string[] replicaAddresses) : base(replicaAddresses)
		{
		}

		public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
		{
			var a = await Task.WhenAny(
				ReplicaAddresses.Select(replica => Task.Run(async () =>
				{
					var webRequest = CreateRequest(replica + "?query=" + query);
					Log.InfoFormat("Processing {0}", webRequest.RequestUri);
					var resultTask = ProcessRequestAsync(webRequest);
					await Task.WhenAny(resultTask, Task.Delay(timeout));
					if (!resultTask.IsCompleted)
						throw new TimeoutException();
					return resultTask.Result;
				})));
			return a.Result;
		}

		protected override ILog Log => LogManager.GetLogger(typeof(SimultaneousClient));
	}
}