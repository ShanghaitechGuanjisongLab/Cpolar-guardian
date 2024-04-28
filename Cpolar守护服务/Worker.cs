using Microsoft.Win32;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Cpolar守护服务
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;

		static readonly RegistryKey 注册表键 = Registry.LocalMachine.CreateSubKey("SYSTEM\\CurrentControlSet\\Services\\Cpolar守护服务", true);
		static readonly TimeSpan 最小周期 = TimeSpan.FromSeconds(30);
		static readonly ServiceController 服务控制器 = new("cpolar");
		static readonly HttpClient HTTP客户端 = new();
		static readonly Aes AES算法 = ((Func<Aes>)(() => {
			Aes 返回值 = Aes.Create();
			返回值.Key = SHA256.HashData(Encoding.UTF8.GetBytes("Cpolar守护"));
			return 返回值;
		}))();
		static readonly MediaTypeHeaderValue JSON类型 = new("application/json");
		static readonly EventId 事件ID = new(1);
		static string 对称解密(byte[] 密文)
		{
			try
			{
				MemoryStream 内存流 = new(密文, false);
				byte[] IV = AES算法.IV;
				内存流.Read(IV);
				return new StreamReader(new CryptoStream(内存流, AES算法.CreateDecryptor(AES算法.Key, IV), CryptoStreamMode.Read), Encoding.UTF8).ReadToEnd();
			}
			catch
			{
				return "";
			}
		}
		bool 上次是starting;
		public Worker(ILogger<Worker> logger)
		{
			_logger = logger;
			定时器 = new Timer((state) => _ = 后台守护());
		}
		readonly Timer 定时器;
		JsonObject 隧道发送内容 = new()
		{
			["proto"] = "tcp",
			["addr"] = "3389",
			["subdomain"] = "",
			["hostname"] = "",
			["auth"] = "",
			["inspect"] = "false",
			["host_header"] = "",
			["bind_tls"] = "both",
			["region"] = "cn_top",
			["disable_keep_alives"] = "false",
			["redirect_https"] = "false",
			["start_type"] = "enable",
			["permanent"] = true,
			["crt"] = "",
			["key"] = "",
			["client_cas"] = ""
		};
		class Cpolar异常(string 消息, Exception? 内部异常 = null) : Exception(消息, 内部异常);
		TimeSpan 上次定时 = 最小周期;
		void 新建隧道(string? 隧道名称, string? TCP地址, AuthenticationHeaderValue 授权)
		{
		}
		async Task 守护检查()
		{
			string? TCP地址 = (string?)注册表键.GetValue("TCP地址");
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
						_logger.LogInformation(事件ID,"检测到Cpolar服务未运行，尝试启动……");
						break;
					case ServiceControllerStatus.Stopped:
					case ServiceControllerStatus.StopPending:
						服务控制器.Start();
						_logger.LogInformation(事件ID, "检测到Cpolar服务未运行，尝试启动……");
						break;
				}
				服务控制器.WaitForStatus(ServiceControllerStatus.Running, 最小周期);
				AuthenticationHeaderValue 授权 = new("Bearer", JsonNode.Parse((await HTTP客户端.PostAsync("http://localhost:9200/api/v1/user/login", new StringContent(new JsonObject
				{
					["email"] = JsonValue.Create((string?)注册表键.GetValue("Email")),
					["password"] = 对称解密((byte[])注册表键.GetValue("Cpolar密码"))
				}.ToJsonString(), JSON类型))).Content.ReadAsStream())["data"]["token"].GetValue<string>());
				HttpRequestMessage 隧道获取 = new(HttpMethod.Get, "http://localhost:9200/api/v1/tunnels");
				隧道获取.Headers.Authorization = 授权;
				上次是starting = false;
				JsonNode? 所有隧道 = JsonNode.Parse(HTTP客户端.Send(隧道获取).Content.ReadAsStream())["data"]["items"];
				string? 隧道名称 = (string?)注册表键.GetValue("隧道名称");
				if (所有隧道 is not null)
					foreach (JsonNode? 隧道 in 所有隧道.AsArray())
						if (隧道["name"].GetValue<string>() == 隧道名称)
						{
							switch (隧道["status"].GetValue<string>())
							{
								case "active":
									上次定时 *= 2;
									_logger.LogInformation(事件ID, "例行检查无异常");
									break;
								case "starting":
									if (上次是starting)
									{
										服务控制器.Stop();
										服务控制器.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1));
										服务控制器.Start();
										服务控制器.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1));
										HttpRequestMessage 隧道启动 = new(HttpMethod.Post, $"http://localhost:9200/api/v1/tunnels/{隧道["id"].GetValue<string>()}/start");
										隧道启动.Headers.Authorization = 授权;
										HTTP客户端.Send(隧道启动);
										_logger.LogInformation(事件ID, "上次启动失败，尝试重启Cpolar服务……");
									}
									上次是starting = true;
									上次定时 = 最小周期;
									break;
								default:
									{
										HttpRequestMessage 隧道启动 = new(HttpMethod.Post, $"http://localhost:9200/api/v1/tunnels/{隧道["id"].GetValue<string>()}/start");
										隧道启动.Headers.Authorization = 授权;
										HTTP客户端.Send(隧道启动);
									}
									上次定时 = 最小周期;
									_logger.LogInformation(事件ID, "发现隧道异常，尝试重启……");
									break;
							}
							goto 跳过新建隧道;
						}
				隧道发送内容["name"] = 隧道名称;
				隧道发送内容["remote_addr"] = TCP地址;
				HttpRequestMessage 隧道发送请求 = new(HttpMethod.Post, "http://localhost:9200/api/v1/tunnels") { Content = new StringContent(隧道发送内容.ToJsonString(), JSON类型) };
				隧道发送请求.Headers.Authorization = 授权;
				JsonNode? 返回 = JsonNode.Parse(HTTP客户端.Send(隧道发送请求).Content.ReadAsStream());
				if (返回["code"].GetValue<ushort>() != 20000)
					throw new Cpolar异常(返回["message"].GetValue<string>());
				上次定时 = 最小周期;
				_logger.LogInformation(事件ID, "没有找到指定名称的隧道，尝试新建……");
			跳过新建隧道:
				定时器.Change(上次定时, 上次定时);
			}
			catch (NullReferenceException ex)
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
				_logger.LogError(事件ID, ex, null);
			}
		}
		protected override Task ExecuteAsync(CancellationToken stoppingToken)
		{
			return 后台守护();
		}
	}
}
