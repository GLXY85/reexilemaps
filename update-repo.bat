@echo off
echo === Updating Repository ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Оптимизация производительности и исправления ошибок:
- Добавлено кэширование данных поиска и фильтрации карт
- Оптимизирован рендеринг окон поиска и вэйпоинтов для улучшения FPS
- Добавлена задержка обновления данных для снижения нагрузки на CPU
- Добавлен метод GetMapNameFromDescription для исправления ошибок компиляции
- Исправлены ссылки на несуществующее свойство Text у AtlasNodeDescription
- Удален параллельный вызов ToDictionary для предотвращения ошибок
- Обновлен файл content.json с новыми типами контента для соответствия новому патчу игры
- Общие улучшения стабильности и производительности"
echo === Pushing to remote repository ===
git push origin local-changes
echo === Done! ===
pause 