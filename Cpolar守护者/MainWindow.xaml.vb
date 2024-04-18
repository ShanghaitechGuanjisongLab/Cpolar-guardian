Imports Microsoft.Win32.TaskScheduler
Imports System.Runtime.InteropServices
Imports System.Threading
Imports log4net.Appender
Imports System.ComponentModel
Class MainWindow
	Shared ReadOnly Current As Application = System.Windows.Application.Current
	Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
		Width = 主框架.ActualWidth + 20
		Height = 主框架.ActualHeight + 40
		With My.Settings
			Email.Text = .Email
			Cpolar密码.Password = 对称解密(.Cpolar密码)
			隧道名称.Text = .隧道名称
			TCP地址.Text = .TCP地址
			切换守护.Content = If(.守护中, "停止守护", "开始守护")
		End With
		状态.Text = Current.上次日志
	End Sub
	Private Sub 保存设置() Handles Me.Closing
		With My.Settings
			.Email = Email.Text
			.Cpolar密码 = 对称加密(Cpolar密码.Password)
			.隧道名称 = 隧道名称.Text
			.TCP地址 = TCP地址.Text
		End With
		My.Settings.Save()
	End Sub

	Const 任务名称 As String = "Cpolar守护任务"
	Shared 计划任务 As Task = TaskService.Instance.GetTask(任务名称)

	Private Async Sub 切换守护_Click(sender As Object, e As RoutedEventArgs) Handles 切换守护.Click
		Try
			If My.Settings.守护中 Then
				Current.定时器.Change(Timeout.Infinite, Timeout.Infinite)
				If 计划任务 IsNot Nothing Then
					计划任务.Enabled = False
				End If
				My.Settings.守护中 = False
				切换守护.Content = "开始守护"
			Else
				保存设置()
				Await Current.守护检查()
				If 计划任务 Is Nothing Then
					'触发器是一次性的，必须先克隆再使用
					计划任务 = TaskService.Instance.AddTask(任务名称, New BootTrigger, New ExecAction(Environment.ProcessPath, "自启动"))
					With 计划任务.Definition.Settings
						.StartWhenAvailable = True
						.DisallowStartIfOnBatteries = False
						.StopIfGoingOnBatteries = False
						.IdleSettings.StopOnIdleEnd = False
						.ExecutionTimeLimit = TimeSpan.Zero
						.RestartInterval = TimeSpan.FromMinutes(5)
						.RestartCount = 300
					End With
					With 计划任务.Definition.Principal
						.RunLevel = TaskRunLevel.Highest
						.UserId = "SYSTEM"
					End With
					计划任务.RegisterChanges()
				End If
				计划任务.Enabled = True
				My.Settings.守护中 = True
				切换守护.Content = "停止守护"
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
