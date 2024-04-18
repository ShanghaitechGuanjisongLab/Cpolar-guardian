using Microsoft.Win32;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;

namespace Cpolar守护服务
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;

		static readonly RegistryKey 注册表键 = Registry.LocalMachine.CreateSubKey("SOFTWARE\\埃博拉酱\\Cpolar守护服务",true);
		static readonly TimeSpan 最小周期 = TimeSpan.FromSeconds(30);
		static readonly ServiceController 服务控制器 = new("cpolar");
		static readonly HttpClient HTTP客户端 = new();
		static readonly Aes AES算法 = ((Func<Aes>)(() => {
			Aes 返回值 = Aes.Create();
			返回值.Key = SHA256.HashData(Encoding.UTF8.GetBytes("Cpolar守护者"));
			return 返回值;
		}))();
		static string 对称解密(string 密文)
		{
			try
			{
				MemoryStream 内存流 = new(Convert.FromBase64String(密文), false);
				byte[] IV = AES算法.IV;
				内存流.Read(IV);
				return new StreamReader(new CryptoStream(内存流, AES算法.CreateDecryptor(AES算法.Key, IV), CryptoStreamMode.Read)).ReadToEnd();
			}
			catch
			{
				return "";
			}
		}
		struct 登录数据
		{
			public string token;
		}
		struct 登录内容
		{
			public 登录数据 data;
		}
		struct 数据条目
		{
			public string id;
			public string name;
			public string status;
		}
		struct 隧道数据
		{
			public 数据条目[] items;
		}
		struct 隧道获取内容
		{
			public 隧道数据 data;
		}
		bool 上次是starting;
		public Worker(ILogger<Worker> logger)
		{
			_logger = logger;
			定时器 = new Timer((state) => _ = 后台守护());
		}
		readonly Timer 定时器;
		struct 隧道发送内容(string name, string remote_addr)
		{
			public string name = name;
			public readonly string proto = "tcp";
			public readonly string addr = "3389";
			public readonly string subdomain = "";
			public readonly string hostname = "";
			public readonly string auth = "";
			public readonly string inspect = "false";
			public readonly string host_header = "";
			public readonly string bind_tls = "both";
			public string remote_addr = remote_addr;
			public readonly string region = "cn_top";
			public readonly string disable_keep_alives = "false";
			public readonly string redirect_https = "false";
			public readonly string start_type = "enable";
			public readonly bool permanent = true;
			public readonly string crt = "";
			public readonly string key = "";
			public readonly string client_cas = "";
		}
		class Cpolar异常(string 消息, Exception? 内部异常 = null) : Exception(消息, 内部异常);
		struct Cpolar状态
		{
			public ushort code;
			public string message;
			public readonly void 断言()
			{
				if (code != 20000)
					throw new Cpolar异常(message);
			}
		}
		TimeSpan 上次定时 = 最小周期;
		async Task 守护检查()
		{
			string TCP地址 = (string)注册表键.GetValue("TCP地址");
			if (string.IsNullOrEmpty(TCP地址))
				throw new Cpolar异常("TCP地址为空");
			try
			{
				服务控制器.Refresh();
				switch (服务控制器.Status)
				{
					case ServiceControllerStatus.Paused:
					case ServiceControllerStatus.PausePending:
						服务控制器.Continue();
						_logger.LogInformation("检测到Cpolar服务未运行，尝试启动……");
						break;
					case ServiceControllerStatus.Stopped:
					case ServiceControllerStatus.StopPending:
						服务控制器.Start();
						_logger.LogInformation("检测到Cpolar服务未运行，尝试启动……");
						break;
				}
				服务控制器.WaitForStatus(ServiceControllerStatus.Running, 最小周期);
				AuthenticationHeaderValue 授权 = new("Bearer", (await (await HTTP客户端.PostAsJsonAsync("http://localhost:9200/api/v1/user/login", new { email = 注册表键.GetValue("Email"), password = 对称解密((string?)注册表键.GetValue("Cpolar密码")) })).Content.ReadFromJsonAsync<登录内容>()).data.token);
				HttpRequestMessage 隧道获取 = new(HttpMethod.Get, "http://localhost:9200/api/v1/tunnels");
				隧道获取.Headers.Authorization = 授权;
				上次是starting = false;
				数据条目[] 所有隧道 = (await HTTP客户端.Send(隧道获取).Content.ReadFromJsonAsync<隧道获取内容>()).data.items;
				string 隧道名称 = (string)注册表键.GetValue("隧道名称");
				if (所有隧道 is not null)
					foreach (数据条目 item in 所有隧道)
						if (item.name == 隧道名称)
						{
							switch(item.status)
							{
								case "active":
									上次定时 *= 2;
									_logger.LogInformation("例行检查无异常");
									break;
								case "starting":
									if(上次是starting)
									{
										服务控制器.Stop();
										服务控制器.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1));
										服务控制器.Start();
										服务控制器.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1));
										HttpRequestMessage 隧道启动 = new(HttpMethod.Post, $"http://localhost:9200/api/v1/tunnels/{item.id}/start");
										隧道启动.Headers.Authorization = 授权;
										HTTP客户端.Send(隧道启动);
										_logger.LogInformation("上次启动失败，尝试重启Cpolar服务……");
									}
									上次是starting = true;
									上次定时 = 最小周期;
									break;
								default:
									{
										HttpRequestMessage 隧道启动 = new(HttpMethod.Post, $"http://localhost:9200/api/v1/tunnels/{item.id}/start");
										隧道启动.Headers.Authorization = 授权;
										HTTP客户端.Send(隧道启动);
									}
									上次定时 = 最小周期;
									_logger.LogInformation("发现隧道异常，尝试重启……");
									break;
							}
							goto 布置下次任务;
						}
				HttpRequestMessage 隧道发送请求 = new(HttpMethod.Post, "http://localhost:9200/api/v1/tunnels") { Content = JsonContent.Create(new 隧道发送内容(隧道名称, TCP地址)) };
				隧道发送请求.Headers.Authorization = 授权;
				(await HTTP客户端.Send(隧道发送请求).Content.ReadFromJsonAsync<Cpolar状态>()).断言();
				上次定时 = 最小周期;
				_logger.LogInformation("没有找到指定名称的隧道，尝试新建……");
			布置下次任务:
				定时器.Change(上次定时, 上次定时);
			}
			catch (JsonException ex)
			{
				throw new Cpolar异常("登录失败，请检查网络连接、Email和Cpolar密码", ex);
			}
		}
		async Task 后台守护()
		{
			try
			{
				await 守护检查();
			}
			catch(Exception ex)
			{
				_logger.LogError(ex, null);
			}
		}
		protected override Task ExecuteAsync(CancellationToken stoppingToken)
		{
			return 后台守护();
		}
	}
}
