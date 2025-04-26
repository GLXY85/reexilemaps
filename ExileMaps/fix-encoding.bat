@echo off
echo === Исправление кодировки ===
echo Конвертирование файлов из ANSI в UTF-8...

chcp 65001
echo Установлена кодировка UTF-8

powershell -Command "Get-ChildItem -Path . -Filter *.cs -Recurse | ForEach-Object { $content = Get-Content -Path $_.FullName -Raw -Encoding Default; Set-Content -Path $_.FullName -Value $content -Encoding UTF8 -NoNewline }"

echo === Проверка файлов JSON ===
powershell -Command "Get-ChildItem -Path .\json -Filter *.json -Recurse | ForEach-Object { $content = Get-Content -Path $_.FullName -Raw -Encoding Default; Set-Content -Path $_.FullName -Value $content -Encoding UTF8 -NoNewline }"

echo === Готово! ===
echo Все файлы проекта сконвертированы в UTF-8
pause 