﻿Imports Microsoft.Win32
Imports System.ServiceProcess
Imports System.Windows.Threading
Class MainWindow
	Shared ReadOnly 服务控制器 As New ServiceController("Cpolar守护服务")
	Shared ReadOnly 注册表键 As RegistryKey = Registry.LocalMachine.CreateSubKey("SOFTWARE\埃博拉酱\Cpolar守护服务", True)
	Shared ReadOnly 服务键 As RegistryKey = Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Services\Cpolar守护服务", True)
	WithEvents 事件日志 As New EventLog("Application", ".", "Cpolar守护服务") With {.EnableRaisingEvents = True}
	Shared Function 服务运行中() As Boolean
		Select Case 服务控制器.Status
			Case ServiceControllerStatus.ContinuePending, ServiceControllerStatus.Running, ServiceControllerStatus.StartPending
				Return True
			Case ServiceControllerStatus.Paused, ServiceControllerStatus.PausePending, ServiceControllerStatus.Stopped, ServiceControllerStatus.StopPending
				Return False
		End Select
	End Function
	Private Async Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
		Width = 主框架.ActualWidth + 20
		Height = 主框架.ActualHeight + 40
		With 注册表键
			Email.Text = .GetValue("Email")
			Try
				Cpolar密码.Password = 对称解密(.GetValue("Cpolar密码"))
			Catch ex As InvalidCastException
				Cpolar密码.Password = ""
			End Try
			隧道名称.Text = .GetValue("隧道名称")
			TCP地址.Text = .GetValue("TCP地址")
		End With
		停止守护.IsEnabled = 服务运行中()
		Dim 状态字符串 As String = Await Task.Run(Function() (From 事件 As EventLogEntry In 事件日志.Entries Where 事件.InstanceId = 1 AndAlso 事件.Source = "Cpolar守护服务" Order By 事件.TimeGenerated Descending Select 事件.TimeGenerated & vbCrLf & 事件.Message).FirstOrDefault(""))
		状态.Text = 状态字符串
	End Sub
	Private Sub 保存设置() Handles Me.Closing
		With 注册表键
			.SetValue("Email", Email.Text)
			.SetValue("Cpolar密码", 对称加密(Cpolar密码.Password))
			.SetValue("隧道名称", 隧道名称.Text)
			.SetValue("TCP地址", TCP地址.Text)
		End With
	End Sub

	Private Sub 事件日志_EntryWritten(sender As Object, e As EntryWrittenEventArgs) Handles 事件日志.EntryWritten
		If e.Entry.InstanceId = 1 Then
			Dispatcher.Invoke(Sub() 状态.Text = e.Entry.TimeGenerated & vbCrLf & e.Entry.Message)
		End If
	End Sub

	Private Sub 开始守护_Click(sender As Object, e As RoutedEventArgs) Handles 开始守护.Click
		保存设置()
		Try
			服务控制器.Stop()
		Catch
		End Try
		Try
			服务控制器.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1))
		Catch ex As Exception
			Dispatcher.Invoke(Sub() 状态.Text = $"{ex.GetType} {ex.Message}")
		End Try
		'不能用CUInt，会被注册表实现为REG_SZ
		服务键.SetValue("Start", CInt(ServiceStartMode.Automatic))
		Try
			服务控制器.Start()
		Catch
		End Try
		Try
			服务控制器.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1))
		Catch ex As Exception
			Dispatcher.Invoke(Sub() 状态.Text = $"{ex.GetType} {ex.Message}")
		End Try
		服务控制器.Refresh()
		停止守护.IsEnabled = 服务运行中()
	End Sub

	Private Sub 停止守护_Click(sender As Object, e As RoutedEventArgs) Handles 停止守护.Click
		Try
			服务键.SetValue("Start", CInt(ServiceStartMode.Manual))
			服务控制器.Stop()
			服务控制器.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1))
		Catch ex As Exception
			Dispatcher.Invoke(Sub() 状态.Text = $"{ex.GetType} {ex.Message}")
		End Try
		服务控制器.Refresh()
		停止守护.IsEnabled = 服务运行中()
	End Sub
End Class
