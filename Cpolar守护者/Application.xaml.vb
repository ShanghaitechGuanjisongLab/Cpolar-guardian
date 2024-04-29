Imports System.Security
Imports System.Security.Principal
Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.

    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        If Not New WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) Then
            Process.Start(New ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName) With {
            .UseShellExecute = True,
            .Verb = "runas" ' 指定以管理员权限运行
        })
            Shutdown()
        End If
    End Sub
End Class
