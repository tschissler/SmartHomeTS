@echo off
REM Load environment variables from .env file (Windows batch script)
REM This script reads the .env file and sets environment variables

echo Loading environment variables from .env file...

if not exist ".env" (
    echo Error: .env file not found!
    echo Please copy .env.example to .env and fill in your credentials.
    exit /b 1
)

for /f "usebackq tokens=1,* delims==" %%a in (".env") do (
    REM Skip comments and empty lines
    echo %%a | findstr /r "^#" >nul
    if errorlevel 1 (
        if not "%%a"=="" (
            if not "%%b"=="" (
                set "%%a=%%b"
                echo Set %%a
            )
        )
    )
)

echo.
echo Environment variables loaded successfully!
echo.
echo You can now run the migration:
echo   python migrate.py --config config.yaml
echo.
