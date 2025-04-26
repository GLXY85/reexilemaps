@echo off
echo === Updating Repository ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Улучшен расчет расстояния для поиска карт:
- Улучшена логика определения точки отсчета для расстояния
- Добавлена дополнительная обработка ошибок и проверки на null
- Улучшено логирование для отладки с добавлением координат точки отсчета
- Оптимизирована проверка видимости элементов интерфейса"
echo === Pushing to remote repository ===
git push origin local-changes
echo === Done! ===
pause 