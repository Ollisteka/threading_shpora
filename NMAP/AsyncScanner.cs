using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace NMAP
{
	class AsyncScanner : IPScanner
	{
		public virtual ILog log => LogManager.GetLogger(typeof(SequentialScanner));

		public virtual Task Scan(IPAddress[] ipAddrs, int[] ports)
		{
			return Task.WhenAll(ipAddrs.Select(ipAddr => Task.Run(async () =>
			{
				if (await PingAddr(ipAddr) != IPStatus.Success)
					return;

				await Task.WhenAll(ports.Select(port => CheckPort(ipAddr, port))); //пингуем одновременно

				foreach (var port in ports)
					await CheckPort(ipAddr, port); //ждём результатов
			})));
		}

		protected async Task<IPStatus> PingAddr(IPAddress ipAddr, int timeout = 3000)
		{
			log.Info($"Pinging {ipAddr}");
			using (var ping = new Ping())
			{
				var status = await ping.SendPingAsync(ipAddr, timeout);
				log.Info($"Pinged {ipAddr}: {status.Status}");
				return status.Status;
			}
		}

		protected async Task CheckPort(IPAddress ipAddr, int port, int timeout = 3000)
		{
			using (var tcpClient = new TcpClient())
			{
				log.Info($"Checking {ipAddr}:{port}");

				var connectTask = await tcpClient.ConnectAsync(ipAddr, port, timeout);

				PortStatus portStatus;
				switch (connectTask.Status)
				{
					case TaskStatus.RanToCompletion:
						portStatus = PortStatus.OPEN;
						break;
					case TaskStatus.Faulted:
						portStatus = PortStatus.CLOSED;
						break;
					default:
						portStatus = PortStatus.FILTERED;
						break;
				}
				log.Info($"Checked {ipAddr}:{port} - {portStatus}");
			}
		}
	}
}