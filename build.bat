pushd %~dp0
set VERSION=
for /f "tokens=*" %%i in ('bin\Release\net10.0\TheVersionator.exe -r https://dev.reachablegames.com -c userpass.txt -i theversionator --patch') do set VERSION=%%i
if not defined VERSION (
	echo Error: TheVersionator failed to produce a version number.  Not building.
	popd
	exit /b 1
)
docker build --progress=plain . -t dev.reachablegames.com/theversionator:%VERSION% -t dev.reachablegames.com/theversionator:latest
docker push dev.reachablegames.com/theversionator:%VERSION%
docker push dev.reachablegames.com/theversionator:latest
pause
