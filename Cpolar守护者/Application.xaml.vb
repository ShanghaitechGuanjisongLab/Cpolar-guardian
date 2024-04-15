Imports System.IO
Imports System.IO.Pipes
Imports System.Net.Http
Imports System.ServiceProcess
Imports System.Windows.Threading
Imports Windows.Devices.Gpio

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
	Sub 守护检查()
		Static 服务控制器 As New ServiceController("cpolar")
		Try
			服务控制器.Start()
			服务控制器.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1))
		Catch ex As Exception
			日志异常(ex)
			Return
		End Try
	End Sub
	Sub 自启动()

	End Sub
	Private Sub 管道回调()
		If New BinaryReader(命名管道服务器流).ReadBoolean Then
			自启动()
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
			自启动()
		Else
			Call (New MainWindow).Show()
		End If
	End Sub
End Class
