@echo off
echo === Fixing file encoding to UTF-8 for all .cs files ===
for %%f in (*.cs) do (
    echo Processing: %%f
    powershell -Command "$content = Get-Content -Path '%%f' -Raw; $bytes = [System.Text.Encoding]::Default.GetBytes($content); $content = [System.Text.Encoding]::UTF8.GetString($bytes); Set-Content -Path '%%f' -Value $content -Encoding UTF8"
)

echo === Processing files in Classes directory ===
if exist Classes (
    for %%f in (Classes\*.cs) do (
        echo Processing: %%f
        powershell -Command "$content = Get-Content -Path '%%f' -Raw; $bytes = [System.Text.Encoding]::Default.GetBytes($content); $content = [System.Text.Encoding]::UTF8.GetString($bytes); Set-Content -Path '%%f' -Value $content -Encoding UTF8"
    )
)

echo === Encoding fixed ===
pause 