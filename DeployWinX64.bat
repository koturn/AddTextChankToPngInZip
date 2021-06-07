@echo off

set DESTINATION_PARENT_DIR=Artifacts
set DESTINATION_DIR=%DESTINATION_PARENT_DIR%\AddTextChankToPngInZip-winx64
set SOLUTION_FILE=AddTextChankToPngInZip.sln

msbuild /nologo /m /t:restore /p:Configuration=Release;Platform="Any CPU" %SOLUTION_FILE%

msbuild /nologo /m /p:Configuration=Release;Platform="Any CPU" %SOLUTION_FILE%

dotnet publish --nologo  /p:Platform="Any CPU" -c Release -o %DESTINATION_DIR% -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRunShowWarnings=true --self-contained true -r win-x64 %SOLUTION_FILE% || set ERRORLEVEL=0

del %DESTINATION_DIR%\*.pdb

:: cd %DESTINATION_PARENT_DIR%

:: 7z a -mx=9 -r ..\RecompressPng.zip RecompressPng

cd ..
