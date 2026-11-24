@echo off
rem Rebuilds Locker.exe from Locker.cs using the C# compiler that ships with Windows
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /target:winexe /out:"%~dp0Locker.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0Locker.cs"
ping 127.0.0.1 -n 1 -w 200 >nul
