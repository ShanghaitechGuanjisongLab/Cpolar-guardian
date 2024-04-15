Imports System.IO
Imports System.IO.Pipes
Imports System.Net.Http

Class Application

	' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
	' can be handled in this file.
	Friend 状态 As String = "正常"
	Friend WithEvents 当前窗口 As MainWindow
	Private 命名管道服务器流 As NamedPipeServerStream
	Sub 守护检查()
		Static HTTP客户端 As New HttpClient
		System.ServiceProcess.
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

	Friend ReadOnly 日志流 As log4net.ILog = (Function() As log4net.ILog
											   log4net.Config.XmlConfigurator.Configure()
											   Return log4net.LogManager.GetLogger("Application")
										   End Function)()
	Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
		If Command() = "自启动" Then
			自启动()
		Else
			Call (New MainWindow).Show()
		End If
	End Sub
End Class
