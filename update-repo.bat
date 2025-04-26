@echo off
echo === Проверка зависимостей ===
echo ВАЖНО: Этот проект требует библиотеки ExileCore2 и GameOffsets2
echo Проверьте, что эти библиотеки установлены для успешной сборки

echo === Сборка проекта ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Устранены критические ошибки совместимости с ExileCore2:
- Максимально упрощен метод получения позиции игрока через прямой доступ к Entity.Pos
- Добавлена надежная обработка ошибок и дополнительное логирование
- Исправлены методы DrawWaypoint и DrawWaypointArrow для работы с RectangleF в качестве угла
- Обновлен метод GetMapNameFromDescription для совместимости с новым API
- Исправлены пути к библиотекам ExileCore2 и GameOffsets2 в файле проекта
- Добавлены дополнительные проверки для предотвращения ошибок при отсутствии данных
- Улучшена обработка ошибок в ключевых методах отрисовки"
echo === Pushing to remote repository ===
git push origin local-changes
echo === Done! ===
pause 