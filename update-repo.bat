@echo off
echo === Updating Repository ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Обновления и исправления ошибок компиляции:
- Добавлен метод GetMapNameFromDescription для исправления ошибок компиляции
- Исправлены ссылки на несуществующее свойство Text у AtlasNodeDescription
- Обновлен файл content.json с новыми типами контента: Cleansed, Corrupted, Corrupted Nexus, Unique Map
- Изменены цвета и веса для различных типов контента для соответствия новому патчу игры
- Улучшена стабильность плагина с дополнительными проверками на null"
echo === Pushing to remote repository ===
git push origin local-changes
echo === Done! ===
pause 