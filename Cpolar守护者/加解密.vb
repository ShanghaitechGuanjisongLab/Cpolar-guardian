Imports System.IO
Imports System.Text
Imports System.Security.Cryptography
Imports Windows.ApplicationModel.Activation

Module 加解密

    ReadOnly AES As Aes = Function() As Aes
                              Dim AES As Aes = Aes.Create
                              AES.Key = SHA256.HashData(Encoding.UTF8.GetBytes("Cpolar守护"))
                              Return AES
                          End Function()

    Function 对称加密(明文 As String) As Byte()
        Dim 内存流 As New MemoryStream
        内存流.Write(AES.IV)
        With New StreamWriter(New CryptoStream(内存流, AES.CreateEncryptor(), CryptoStreamMode.Write), Encoding.UTF8)
            .Write(明文)
            .Dispose() '必须手动释放，否则不会实际写入到到底层内存流
        End With
        Return 内存流.ToArray
    End Function

    Function 对称解密(密文 As Byte()) As String
        Try
            Dim 内存流 As New MemoryStream(密文, False)
            'AES.IV是临时生成的返回值，不能直接写入
            Dim IV As Byte() = AES.IV
            内存流.Read(IV)
            Return New StreamReader(New CryptoStream(内存流, AES.CreateDecryptor(AES.Key, IV), CryptoStreamMode.Read), Encoding.UTF8).ReadToEnd
        Catch ex As Exception
            Return ""
        End Try
    End Function
End Module
