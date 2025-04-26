@echo off
echo === Проверка зависимостей ===
echo ВАЖНО: Этот проект требует библиотеки ExileCore2 и GameOffsets2
echo Проверьте, что эти библиотеки установлены для успешной сборки

echo === Сборка проекта ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Исправлены критические ошибки совместимости с новой версией ExileCore2:
- Полностью переработан метод GetPlayerPositionForDistance с использованием рефлексии
- Добавлена универсальная обработка компонентов с поддержкой Vector2, Vector2i и Vector3
- Исправлено определение позиции игрока из объекта Entity.Pos
- Исправлены методы DrawWaypoint и DrawWaypointArrow для работы с RectangleF в качестве угла
- Обновлен метод GetMapNameFromDescription для совместимости с новым API
- Исправлены пути к библиотекам ExileCore2 и GameOffsets2 в файле проекта
- Добавлены дополнительные проверки для предотвращения ошибок при отсутствии данных
- Улучшена обработка ошибок в ключевых методах отрисовки"
echo === Pushing to remote repository ===
git push origin local-changes
echo === Done! ===
pause 