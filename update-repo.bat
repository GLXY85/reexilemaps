@echo off
echo === Updating Repository ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Улучшения интерфейса поиска карт:
- Сортировка карт по расстоянию от позиции игрока, ближайшие карты первыми
- Улучшенное отображение расстояния с единицами измерения
- Переименование колонки на 'Расстояние' для лучшего понимания
- Исправления ошибок при вычислении расстояния
- Добавлен класс ColorHelper для удобного управления цветами в интерфейсе
- Улучшена обработка ошибок и защита от null-ссылок"
echo === Pushing to remote repository ===
git push origin local-changes
echo === Done! ===
pause 