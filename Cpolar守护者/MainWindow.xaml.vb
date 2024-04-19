Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.Win32
Imports System.ServiceProcess
Imports System.Windows.Threading
Class MainWindow
	Shared ReadOnly 服务控制器 As New ServiceController("Cpolar守护服务")
	Shared Function 服务运行中() As Boolean
		Select Case 服务控制器.Status
			Case ServiceControllerStatus.ContinuePending, ServiceControllerStatus.Running, ServiceControllerStatus.StartPending
				Return True
			Case ServiceControllerStatus.Paused, ServiceControllerStatus.PausePending, ServiceControllerStatus.Stopped, ServiceControllerStatus.StopPending
				Return False
		End Select
	End Function
	Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
		Width = 主框架.ActualWidth + 20
		Height = 主框架.ActualHeight + 40
		With Registry.LocalMachine.CreateSubKey("SOFTWARE\埃博拉酱\Cpolar守护服务")
			Email.Text = .GetValue("Email")
			Cpolar密码.Password = 对称解密(.GetValue("Cpolar密码"))
			隧道名称.Text = .GetValue("隧道名称")
			TCP地址.Text = .GetValue("TCP地址")
			状态.Text = .GetValue("状态")
		End With
		切换守护.Content = If(服务运行中(), "停止守护", "开始守护")
	End Sub
	Private Sub 保存设置() Handles Me.Closing
		With Registry.LocalMachine.CreateSubKey("SOFTWARE\埃博拉酱\Cpolar守护服务")
			.SetValue("Email", Email.Text)
			.SetValue("Cpolar密码", 对称加密(Cpolar密码.Password))
			.SetValue("隧道名称", 隧道名称.Text)
			.SetValue("TCP地址", TCP地址.Text)
		End With
	End Sub
	Private Sub 切换守护_Click(sender As Object, e As RoutedEventArgs) Handles 切换守护.Click
		切换守护.IsEnabled = False
		Task.Run(Sub()
					 Try
						 If 服务运行中() Then
							 服务控制器.Stop()
							 Dispatcher.Invoke(Sub() 切换守护.Content = "服务停止中……")
							 服务控制器.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1))
							 Dispatcher.Invoke(Sub() 切换守护.Content = "开始守护")
						 Else
							 服务控制器.Start()
							 Dispatcher.Invoke(Sub() 切换守护.Content = "服务启动中……")
							 服务控制器.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1))
							 Dispatcher.Invoke(Sub() 切换守护.Content = "停止守护")
						 End If
					 Catch ex As Exception
						 Dispatcher.Invoke(Sub() 状态.Text = $"{ex.GetType} {ex.Message}")
					 End Try
					 服务控制器.Refresh()
					 Dispatcher.Invoke(Sub()
										   切换守护.IsEnabled = True
										   切换守护.Content = If(服务运行中(), "停止守护", "开始守护")
									   End Sub)
				 End Sub)
	End Sub
End Class
