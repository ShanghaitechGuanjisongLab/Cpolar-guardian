Class MainWindow
	Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
		Width = 主框架.ActualWidth + 20
		Height = 主框架.ActualHeight + 40
		Email.Text = My.Settings.Email
		密码.Password = My.Settings.密码
		隧道名称.Text = My.Settings.隧道名称
		本地端口.Text = My.Settings.本地端口
		TCP地址.Text = My.Settings.TCP地址
		切换守护.Content = If(My.Settings.守护中, "停止守护", "开始守护")
		状态.Text = DirectCast(Application.Current, Application).状态
	End Sub

	Private Sub 切换守护_Click(sender As Object, e As RoutedEventArgs) Handles 切换守护.Click

	End Sub
End Class
