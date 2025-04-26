@echo off
echo === Updating Repository ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Улучшен расчет расстояния для поиска карт:
- Изменен метод расчета расстояния для использования только позиции игрока
- Удален неиспользуемый метод GetReferencePositionForDistance
- Улучшено логирование позиции для расчета расстояния
- Упрощен код определения точки отсчета"
echo === Pushing to remote repository ===
git push origin local-changes
echo === Done! ===
pause 