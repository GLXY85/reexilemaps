@echo off
echo === Checking dependencies ===
echo IMPORTANT: This project requires ExileCore2 and GameOffsets2 libraries
echo Make sure these libraries are installed for successful build

echo === Building project ===
dotnet build --configuration Release

git add .
git status
echo === Committing changes ===
git commit -m "Major updates to ExileMaps plugin:
- Fixed critical compatibility issues with ExileCore2
- Simplified player position detection method using direct access to Entity.Pos
- Added robust error handling and additional logging
- Fixed DrawWaypoint and DrawWaypointArrow methods to work with RectangleF as angle
- Updated GetMapNameFromDescription method for compatibility with new API
- Fixed paths to ExileCore2 and GameOffsets2 libraries in project file
- Translated UI interface from Russian to English
- Added comprehensive bilingual (English/Russian) README with plugin features
- Added detailed installation instructions and feature descriptions
- Improved error handling in key rendering methods
- Fixed file encoding issues to UTF-8 for proper display of Russian characters
- Fixed arrow texture loading in Initialise method for waypoint indicators
- Added fallback rendering mechanism for waypoint arrows
- Extended DrawWaypointArrow with additional error handling"
echo === Pushing to remote repository ===
git push origin local-changes
echo === Done! ===
pause 