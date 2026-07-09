pushd %~dp0
rem VS 18's vcvarsall.bat calls vswhere.exe without a full path, which breaks the AOT linker probe unless the Installer dir is on PATH.
set PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%
dotnet publish TheVersionator.csproj --nologo -c Release -r win-x64 -o build\win-x64
if errorlevel 1 (
	echo Error: dotnet publish failed.  Not building.
	popd
	exit /b 1
)
set VERSION=
for /f "tokens=*" %%i in ('build\win-x64\TheVersionator.exe -r https://dev.reachablegames.com -c userpass.txt -i theversionator --patch') do set VERSION=%%i
if not defined VERSION (
	echo Error: TheVersionator failed to produce a version number.  Not building.
	popd
	exit /b 1
)
docker build --progress=plain . -t dev.reachablegames.com/theversionator:%VERSION% -t dev.reachablegames.com/theversionator:latest
docker push dev.reachablegames.com/theversionator:%VERSION%
docker push dev.reachablegames.com/theversionator:latest
pause
