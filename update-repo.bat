@echo off
echo === Updating Repository ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Улучшения поиска карт:
- Улучшена сортировка карт по расстоянию
- Ближайшие карты отображаются первыми
- Расстояние показывается с единицами измерения
- Добавлен мод 'Coalesced Corruption'
- Улучшена обработка ошибок и стабильность
"
echo === Pushing to remote repository ===
git push origin main
echo === Done! ===
pause 