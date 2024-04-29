Imports System.Runtime.InteropServices
Module 输入法
    <DllImport("imm32.dll", CharSet:=CharSet.Unicode)>
    Public Function ImmGetContext(hWnd As IntPtr) As IntPtr
    End Function
End Module
