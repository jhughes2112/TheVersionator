pushd %~dp0
for /f "tokens=*" %%i in ('bin\Release\net8.0\TheVersionator.exe -r https://dev.reachablegames.com -c userpass.txt -i theversionator') do set VERSION=%%i
docker build --progress=plain . -t dev.reachablegames.com/theversionator:%VERSION% -t dev.reachablegames.com/theversionator:latest
docker push dev.reachablegames.com/theversionator:%VERSION%
docker push dev.reachablegames.com/theversionator:latest
pause