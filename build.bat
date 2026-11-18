@echo off
rem Compila o Volt usando o compilador C# que ja vem no Windows (.NET Framework 4.x).
rem Nao precisa instalar nada.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Compilador C# nao encontrado. Instale o .NET Framework 4.8.
    exit /b 1
)

cd /d "%~dp0"
set ICO=
if exist Volt.ico set ICO=/win32icon:Volt.ico
"%CSC%" /nologo /target:winexe /optimize /platform:anycpu %ICO% /win32manifest:Volt.manifest /out:Volt.exe Volt.cs Painel.cs Leitura.cs Comum.cs Barra.cs Dica.cs
if errorlevel 1 (
    echo.
    echo Erro ao compilar.
    exit /b 1
)
echo.
echo Pronto: Volt.exe
