Imports Microsoft.Win32
Class MainWindow
	ReadOnly Current As Application = System.Windows.Application.Current
	Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
		Width = 主框架.ActualWidth + 20
		Height = 主框架.ActualHeight + 40
		Email.Text = My.Settings.Email
		密码.Password = My.Settings.密码
		隧道名称.Text = My.Settings.隧道名称
		TCP地址.Text = My.Settings.TCP地址
		切换守护.Content = If(My.Settings.守护中, "停止守护", "开始守护")
		状态.Text = Current.状态
	End Sub

	Private Sub 切换守护_Click(sender As Object, e As RoutedEventArgs) Handles 切换守护.Click
		Try
			If My.Settings.守护中 Then
			Else
				Current.守护检查()
				Static 任务服务 As TaskScheduler.TaskService = TaskScheduler.TaskService.Instance
				Static 启动触发器 As New TaskScheduler.BootTrigger
				Const 任务名称 As String = "Cpolar守护任务"
				Static 计划任务 As TaskScheduler.Task = 任务服务.GetTask(任务名称)
				Static 执行动作 As New TaskScheduler.ExecAction(Process.GetCurrentProcess().MainModule.FileName, "自启动")
				If 计划任务 Is Nothing Then
					'触发器是一次性的，必须先克隆再使用
					计划任务 = 任务服务.AddTask(任务名称, 启动触发器.Clone, 执行动作)
					With 计划任务.Definition.Settings
						.StartWhenAvailable = True
						.DisallowStartIfOnBatteries = False
						.StopIfGoingOnBatteries = False
						.IdleSettings.StopOnIdleEnd = False
						.AllowDemandStart = True
						.ExecutionTimeLimit = TimeSpan.Zero
						.RunOnlyIfLoggedOn = False

					End With
				Else
					计划任务.Definition.Triggers.Item(0) = 启动触发器.Clone
				End If
			End If
		Catch ex As Exception
			Current.日志异常(ex)
		End Try
	End Sub
End Class
