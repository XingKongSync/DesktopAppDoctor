@echo off

for /f "delims=" %%x in ('GetVersion.exe "DesktopAppDoctor\bin\Release\DesktopAppDoctor.exe"') do (
    set ClientVersion=%%x
)


call "clean_release.bat"
makensis.exe /DClientVersion=%ClientVersion% DesktopAppDoctor.nsi

