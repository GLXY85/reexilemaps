@echo off
echo === Updating Repository ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Исправлены ошибки в ReExileMaps.cs:
- Удалены дублирующиеся определения переменных cachedDistances, mapItems и referencePositionText
- Добавлены недостающие методы для работы с путевыми точками
- Добавлены свойства для работы с поиском карт
- Исправлены пути к зависимостям в файле проекта"
echo === Pushing to remote repository ===
git push origin local-changes
echo === Done! ===
pause 