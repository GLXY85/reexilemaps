# ExileMaps | Карты Изгнания

## English Description

**ExileMaps** is a plugin for Path of Exile that enhances map navigation and provides useful atlas information. The plugin is designed to work with ExileCore2.

### Features

- **Interactive Map Display**: View and interact with the Atlas map in real-time
- **Map Search**: Quickly find maps by name or properties 
- **Distance Calculation**: Calculate and display distances between map nodes
- **Waypoint System**: Set custom waypoints on the Atlas for navigation
- **Map Information**: View detailed information about maps, including mods, effects, and biomes
- **Minimap Display**: Show the Atlas data in a convenient minimap view
- **Custom Filtering**: Filter maps by various criteria to find exactly what you need
- **Visual Indicators**: Colored indicators show map weights, distances, and other properties
- **Hotkey Support**: Customize keyboard shortcuts for quick access to functions
- **Customizable UI**: Adjust colors, sizes, and other display options to your preference

### Installation

1. Make sure ExileCore2 is installed
2. Place the plugin files in the `Plugins/Source/reexilemaps` directory of your ExileCore2 installation
3. Make sure the ExileCore2.dll and GameOffsets2.dll are properly referenced
4. Restart ExileCore2

## Описание на русском

**ExileMaps** - это плагин для Path of Exile, который улучшает навигацию по картам и предоставляет полезную информацию об атласе. Плагин разработан для работы с ExileCore2.

### Функции

- **Интерактивное отображение карты**: Просмотр и взаимодействие с Атласом карт в реальном времени
- **Поиск карт**: Быстрый поиск карт по названию или свойствам
- **Расчет расстояний**: Вычисление и отображение расстояний между узлами карт
- **Система путевых точек**: Установка пользовательских путевых точек на Атласе для навигации
- **Информация о картах**: Просмотр подробной информации о картах, включая модификаторы, эффекты и биомы
- **Отображение миникарты**: Показ данных Атласа в удобном виде миникарты
- **Настраиваемая фильтрация**: Фильтрация карт по различным критериям для поиска именно того, что вам нужно
- **Визуальные индикаторы**: Цветовые индикаторы показывают веса карт, расстояния и другие свойства
- **Поддержка горячих клавиш**: Настройка сочетаний клавиш для быстрого доступа к функциям
- **Настраиваемый интерфейс**: Настройка цветов, размеров и других параметров отображения по вашему вкусу

### Установка

1. Убедитесь, что ExileCore2 установлен
2. Поместите файлы плагина в каталог `Plugins/Source/reexilemaps` вашей установки ExileCore2
3. Убедитесь, что ExileCore2.dll и GameOffsets2.dll правильно подключены
4. Перезапустите ExileCore2

## Description

Re:ExileMaps is a plugin for Path of Exile that enhances the map display in the game, providing additional information and visual elements. This is an updated version of the original ExileMaps plugin, adapted to work with ExileCore2.

### Key Features

- Highlighting maps in the atlas based on their content and modifiers
- Displaying exact map coordinates
- Advanced map search system by name, content, effects, and biomes
- Ability to add waypoints to maps for quick navigation
- Customizable color indication system for various map content

## Installation

1. Clone the repository or download it as a ZIP archive
2. Place the contents in your Path of Exile client's plugins folder
3. **Important**: Make sure to have ExileCore2 and GameOffsets2 referenced in your project, these are required dependencies

## Requirements

- .NET 8.0
- ExileCore2
- GameOffsets2 (containing necessary type definitions)

## Recent Fixes

The recent updates include several important fixes:
- Removed duplicate variable definitions
- Added missing methods for waypoint functionality
- Fixed map search typing issues
- Improved position detection for distance calculations
- Fixed rendering methods for waypoints and markers

## Building

To build the project, you need to have the required libraries referenced in your solution:
```bash
dotnet build --configuration Release
```

If you encounter errors related to missing types (like `AtlasPanel`, `Vector2i`, etc.), check that all required dependencies are properly set up.

## Usage

After installing the plugin, start the game and enable Re:ExileMaps in the plugin settings.

### Map Search

- Press F4 to open the map search panel (the hotkey can be changed in the settings)
- Enter map name or other keywords to search
- Search works for map names, content types, effects, and biomes
- Results can be sorted by name, status, or weight

#### Advanced Search Syntax

You can use property-based search with the following syntax: `property:value`

Available properties:
- `content:value` - Search by content type (e.g., `content:delirium`)
- `effect:value` - Search by map effects (e.g., `effect:coalesced corruption`)
- `biome:value` - Search by biome name (e.g., `biome:forest`)
- `name:value` - Search by map name (e.g., `name:tower`)
- `status:value` - Search by status (e.g., `status:visited`, `status:unlocked`)

## License

[MIT](LICENSE)

---

# Re:ExileMaps

Плагин для отображения карт в Path of Exile.

## Описание

Re:ExileMaps - это плагин для Path of Exile, который улучшает отображение карт в игре, предоставляя дополнительную информацию и визуальные элементы. Это обновленная версия оригинального плагина ExileMaps, адаптированная для работы с ExileCore2.

### Основные функции

- Подсветка карт в атласе на основе их содержимого и модификаторов
- Отображение точных координат карт
- Расширенная система поиска карт по имени, содержимому, эффектам и биомам
- Возможность добавления путевых точек на карты для быстрой навигации
- Настраиваемая система цветовой индикации для различного контента карт

## Установка

1. Клонируйте репозиторий или загрузите как ZIP-архив
2. Разместите содержимое в папке с плагинами вашего клиента Path of Exile
3. **Важно**: Убедитесь, что в вашем проекте есть ExileCore2 и GameOffsets2, это зависимые библиотеки

## Требования

- .NET 8.0
- ExileCore2
- GameOffsets2 (содержащие необходимые определения типов)

## Недавние исправления

Недавние обновления включают несколько важных исправлений:
- Удалены дублирующие определения переменных
- Добавлены отсутствующие методы для функциональности путевых точек
- Исправлены проблемы с вводом поиска карт
- Улучшена обнаружение позиции для вычисления расстояний
- Исправлены методы рендеринга для путевых точек и маркеров

## Сборка

Чтобы собрать проект, вам нужно иметь в решении ссылки на необходимые библиотеки:
```bash
dotnet build --configuration Release
```

Если вы столкнетесь с ошибками, связанными с отсутствием типов (например, `AtlasPanel`, `Vector2i`, и т.д.), проверьте, что все необходимые зависимости настроены правильно.

## Использование

После установки плагина запустите игру и включите Re:ExileMaps в настройках плагинов.

### Поиск карт

- Нажмите F4 для открытия панели поиска карт (горячую клавишу можно изменить в настройках)
- Введите название карты или другие ключевые слова для поиска
- Поиск работает по названиям карт, типам контента, эффектам и биомам
- Результаты можно сортировать по имени, статусу или весу

#### Расширенный синтаксис поиска

Вы можете использовать поиск по свойствам с помощью следующего синтаксиса: `свойство:значение`

Доступные свойства:
- `content:значение` - Поиск по типу контента (например, `content:delirium`)
- `effect:значение` - Поиск по эффектам карты (например, `effect:coalesced corruption`)
- `biome:значение` - Поиск по названию биома (например, `biome:forest`)
- `name:значение` - Поиск по названию карты (например, `name:tower`)
- `status:значение` - Поиск по статусу (например, `status:visited`, `status:unlocked`)

## Лицензия

[MIT](LICENSE) 