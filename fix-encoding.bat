@echo off
echo === Fixing file encoding to UTF-8 ===
powershell -Command "$content = Get-Content -Path 'ReExileMaps.cs' -Raw; $bytes = [System.Text.Encoding]::Default.GetBytes($content); $content = [System.Text.Encoding]::UTF8.GetString($bytes); Set-Content -Path 'ReExileMaps.cs' -Value $content -Encoding UTF8"
echo === Encoding fixed ===
pause 