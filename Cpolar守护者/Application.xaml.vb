Imports System.IO
Imports System.IO.Pipes
Imports System.Net.Http
Imports System.Net.Http.Json
Imports System.ServiceProcess
Imports System.Threading
Imports System.Windows.Threading

Class Application

	' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
	' can be handled in this file.
	Friend 状态 As String = "正常"
	Friend WithEvents 当前窗口 As MainWindow
	Private 命名管道服务器流 As NamedPipeServerStream

	Friend ReadOnly 日志流 As log4net.ILog = (Function() As log4net.ILog
											   log4net.Config.XmlConfigurator.Configure()
											   Return log4net.LogManager.GetLogger("Application")
										   End Function)()
	Sub 日志消息(消息 As String)
		If 当前窗口 IsNot Nothing Then
			Dispatcher.Invoke(Sub() 当前窗口.状态.Text = 消息)
		End If
		日志流.Info(消息)
	End Sub
	Sub 日志异常(异常 As Exception)
		If 当前窗口 IsNot Nothing Then
			Dispatcher.Invoke(Sub() 当前窗口.状态.Text = $"{异常.GetType} {异常.Message}")
		End If
		日志流.Error(Nothing, 异常)
	End Sub

	Shared ReadOnly 服务控制器 As New ServiceController("cpolar")
	Shared ReadOnly HTTP客户端 As New HttpClient
	ReadOnly 隧道获取 As New HttpRequestMessage(HttpMethod.Get, "http://localhost:9200/api/v1/tunnels")
	ReadOnly 隧道发送请求 As New HttpRequestMessage(HttpMethod.Post, "http://localhost:9200/api/v1/tunnels")
	Friend ReadOnly 定时器 As New Timer(AddressOf 守护检查)
	Property 上次是starting As Boolean
	Property 上次定时 As TimeSpan = TimeSpan.FromMinutes(5)
	Structure 登录数据
		Property token As String
	End Structure
	Structure 登录内容
		Property data As 登录数据
	End Structure
	Structure 数据条目
		Property id As String
		Property name As String
		Property status As String
	End Structure
	Structure 隧道数据
		Property items As 数据条目()
	End Structure
	Structure 隧道获取内容
		Property data As 隧道数据

	End Structure
	Class 隧道发送内容
		Property name As String
		ReadOnly Property proto As String = "tcp"
		ReadOnly Property addr As String = "3389"
		ReadOnly Property subdomain As String = ""
		ReadOnly Property hostname As String = ""
		ReadOnly Property auth As String = ""
		ReadOnly Property inspect As String = "false"
		ReadOnly Property host_header As String = ""
		ReadOnly Property bind_tls As String = "both"
		Property remote_addr As String
		ReadOnly Property region As String = "cn_top"
		ReadOnly Property disable_keep_alives As String = "false"
		ReadOnly Property redirect_https As String = "false"
		ReadOnly Property start_type As String = "enable"
		ReadOnly Property permanent As Boolean = True
		ReadOnly Property crt As String = ""
		ReadOnly Property key As String = ""
		ReadOnly Property client_cas As String = ""
	End Class
	Async Sub 守护检查()
		Try
			Select Case 服务控制器.Status
				Case ServiceControllerStatus.Paused, ServiceControllerStatus.PausePending
					服务控制器.Continue()
				Case ServiceControllerStatus.Stopped, ServiceControllerStatus.StopPending
					服务控制器.Start()
			End Select
			服务控制器.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1))
			Dim 授权 As New Headers.AuthenticationHeaderValue("Bearer", (Await (Await HTTP客户端.PostAsJsonAsync("http://localhost:9200/api/v1/user/login", New With {My.Settings.Email, My.Settings.Cpolar密码})).Content.ReadFromJsonAsync(Of 登录内容)).data.token)
			隧道获取.Headers.Authorization = 授权
			上次是starting = False
			For Each item In (Await HTTP客户端.Send(隧道获取).Content.ReadFromJsonAsync(Of 隧道获取内容)).data.items
				If item.name = My.Settings.隧道名称 Then
					Select Case item.status
						Case "active"
							上次定时 *= 2
						Case "starting"
							上次是starting = True
							If 上次是starting Then
								服务控制器.Stop()
								服务控制器.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1))
								服务控制器.Start()
								服务控制器.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1))
								Dim 隧道启动 As New HttpRequestMessage(HttpMethod.Post, $"http://localhost:9200/api/v1/tunnels/{item.id}/start")
								隧道启动.Headers.Authorization = 授权
								HTTP客户端.Send(隧道启动)
							End If
							上次定时 = TimeSpan.FromMinutes(5)
						Case Else
							Dim 隧道启动 As New HttpRequestMessage(HttpMethod.Post, $"http://localhost:9200/api/v1/tunnels/{item.id}/start")
							隧道启动.Headers.Authorization = 授权
							HTTP客户端.Send(隧道启动)
							上次定时 = TimeSpan.FromMinutes(5)
					End Select
					GoTo 布置下次任务
				End If
			Next
			隧道发送请求.Headers.Authorization = 授权
			隧道发送请求.Content = JsonContent.Create(New 隧道发送内容 With {.name = My.Settings.隧道名称, .remote_addr = My.Settings.TCP地址})
			HTTP客户端.Send(隧道发送请求)
			上次定时 = TimeSpan.FromMinutes(5)
布置下次任务:
			定时器.Change(上次定时, 上次定时)
		Catch ex As Exception
			日志异常(ex)
		End Try
	End Sub
	Private Sub 管道回调()
		If New BinaryReader(命名管道服务器流).ReadBoolean Then
			守护检查()
		Else
			If 当前窗口 Is Nothing Then
				Dispatcher.Invoke(Sub() Call (New MainWindow).Show())
			Else
				Dispatcher.Invoke(AddressOf 当前窗口.Activate)
			End If
		End If
		命名管道服务器流.Disconnect()
		命名管道服务器流.BeginWaitForConnection(AddressOf 管道回调, Nothing)
	End Sub

	Sub New()
		Try
			命名管道服务器流 = New NamedPipeServerStream("Cpolar守护者", PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous Or PipeOptions.CurrentUserOnly)
		Catch ex As IOException
			Dim 命名管道客户端流 As New NamedPipeClientStream(".", "Cpolar守护者", PipeDirection.Out)
			命名管道客户端流.Connect(1000)
			Call New BinaryWriter(命名管道客户端流).Write(Command() = "自启动")
			Shutdown()
			Exit Sub
		End Try
		命名管道服务器流.BeginWaitForConnection(AddressOf 管道回调, Nothing)
	End Sub

	Private Sub 当前窗口_Closed(sender As Object, e As EventArgs) Handles 当前窗口.Closed
		当前窗口 = Nothing
		If Not My.Settings.守护中 Then
			Shutdown()
		End If
	End Sub
	Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
		If Command() = "自启动" Then
			守护检查()
		Else
			当前窗口 = New MainWindow
			当前窗口.Show()
		End If
	End Sub
End Class
