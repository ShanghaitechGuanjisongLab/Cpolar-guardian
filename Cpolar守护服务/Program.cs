using Cpolar守护服务;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using System.Security.Cryptography;
using System.Text;
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
	options.ServiceName = "Cpolar守护服务";
});
LoggerProviderOptions.RegisterProviderOptions<
	EventLogSettings, EventLogLoggerProvider>(builder.Services);
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();