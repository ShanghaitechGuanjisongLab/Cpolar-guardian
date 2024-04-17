Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports Microsoft.Win32.TaskScheduler
Imports System.Runtime.InteropServices
Imports System.Threading
Imports log4net.Appender
Class MainWindow
	Shared ReadOnly Current As Application = System.Windows.Application.Current
	Shared ReadOnly AES As Aes = Function() As Aes
									 Dim AES As Aes = Aes.Create
									 AES.Key = SHA256.HashData(Encoding.UTF8.GetBytes("Cpolar守护者"))
									 Return AES
								 End Function()

	Shared Function 对称加密(明文 As String) As String
		AES.GenerateIV()
		Dim 内存流 As New MemoryStream
		内存流.Write(AES.IV, 0, AES.IV.Length)
		Call New StreamWriter(New CryptoStream(内存流, AES.CreateEncryptor(), CryptoStreamMode.Write)).Write(明文)
		Return Convert.ToBase64String(内存流.ToArray)
	End Function

	Shared Function 对称解密(密文 As String) As String
		Static 初始化向量长度 As Integer = AES.BlockSize / 8 - 1
		Dim 密文字节 As Byte() = Convert.FromBase64String(密文)
		Return If(密文字节.Length > 初始化向量长度, New StreamReader(New CryptoStream(New MemoryStream(密文字节, 初始化向量长度, 密文字节.Length - 初始化向量长度, False), AES.CreateDecryptor(AES.Key, 密文字节.Take(初始化向量长度).ToArray), CryptoStreamMode.Read)).ReadToEnd, "")
	End Function
	Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
		Width = 主框架.ActualWidth + 20
		Height = 主框架.ActualHeight + 40
		With My.Settings
			Email.Text = .Email
			Cpolar密码.Password = 对称解密(.Cpolar密码)
			Windows密码.Password = 对称解密(.Windows密码)
			隧道名称.Text = .隧道名称
			TCP地址.Text = .TCP地址
			切换守护.Content = If(.守护中, "停止守护", "开始守护")
		End With
		状态.Text = Current.状态
	End Sub
	Private Sub MainWindow_Closing() Handles Me.Closing
		With My.Settings
			.Email = Email.Text
			.Cpolar密码 = 对称加密(Cpolar密码.Password)
			.Windows密码 = 对称加密(Windows密码.Password)
			.隧道名称 = 隧道名称.Text
			.TCP地址 = TCP地址.Text
		End With
	End Sub
	Private Sub 切换守护_Click(sender As Object, e As RoutedEventArgs) Handles 切换守护.Click
		Try
			Static 任务服务 As TaskService = TaskService.Instance
			Const 任务名称 As String = "Cpolar守护任务"
			Static 计划任务 As Task = 任务服务.GetTask(任务名称)
			If My.Settings.守护中 Then
				Current.定时器.Change(Timeout.Infinite, Timeout.Infinite)
				If 计划任务 IsNot Nothing Then
					计划任务.Enabled = False
				End If
			Else
				Current.守护检查()
				If 计划任务 Is Nothing Then
					'触发器是一次性的，必须先克隆再使用
					计划任务 = 任务服务.AddTask(任务名称, New BootTrigger, New ExecAction(Environment.ProcessPath, "自启动"))
					With 计划任务.Definition.Settings
						.StartWhenAvailable = True
						.DisallowStartIfOnBatteries = False
						.StopIfGoingOnBatteries = False
						.IdleSettings.StopOnIdleEnd = False
						.ExecutionTimeLimit = TimeSpan.Zero
						.RunOnlyIfLoggedOn = False
						.RestartInterval = TimeSpan.FromMinutes(5)
						.RestartCount = 300
					End With
					With 计划任务.Definition.Principal
						.LogonType = TaskLogonType.Password
						.Id = "Author"
						.RunLevel = TaskRunLevel.Highest
						.UserId = Environment.UserName
						Static 任务文件夹 As TaskFolder = 任务服务.GetFolder("\")
						任务文件夹.RegisterTaskDefinition(任务名称, 计划任务.Definition, TaskCreation.CreateOrUpdate, Environment.UserName, Windows密码.Password, TaskLogonType.Password)
					End With
				End If
				计划任务.Enabled = True
				My.Settings.守护中 = True
			End If
		Catch ex As Exception
			Current.日志异常(ex)
		End Try
	End Sub
	<DllImport("shell32.dll", CharSet:=CharSet.Unicode)>
	Private Shared Function ShellExecute(
		hwnd As IntPtr,
		lpOperation As String,
		lpFile As String,
		lpParameters As String,
		lpDirectory As String,
		nShowCmd As Integer) As IntPtr
	End Function
	Private Sub 查看日志_Click(sender As Object, e As RoutedEventArgs) Handles 查看日志.Click
		Static 日志路径 As String = DirectCast(Current.日志流.Logger.Repository.GetAppenders.Single, FileAppender).File
		ShellExecute(IntPtr.Zero, "open", 日志路径, "", "", 1)
	End Sub
End Class
