[English](README.md) | **Русский**

# Каталог Магазина и проверки элементов

Этот документ описывает репозиторий Магазина, который читает GUI, поля JSON каталога и проверки для каждого типа загрузки.
Последовательность установки, очередь и откат описаны в разделе Store документа [CDPIUI.Core/README.ru.md](../README.ru.md#store).
Действия установки (`before_install_actions`, `after_install_actions`) описаны в [LScript/README.ru.md](../LScript/README.ru.md).

Код находится в таких файлах:

| Часть | Файлы |
|---|---|
| Репозиторий и каталог | `Repository/RepositoryLoaderService.cs`, `Repository/*InitializationModel.cs`, `Repository/VersionControlData.cs`, `ViewModels/RepoItemModel.cs`, `ViewModels/ReadyKitModel.cs` |
| Ссылки для загрузки | `OnlineItemsInstallationService.cs`, `Network/APIWorker.cs`, `Network/Models/*.cs` |
| Загрузка и распаковка | `Network/RequestWorker.cs`, `Network/DownloadWorker.cs`, `CDPIUI.Core/System/ZipService.cs`, `CDPIUI.Shared/Basic/Filesystem/ArchivePaths.cs` |
| Типы файлов | `FileExtensionTypes` и `FileSystemService` в `CDPIUI.Shared/Basic/Filesystem/FileSystemService.cs` |
| Подпись каталога | `CDPIUI.Core/Security/CertificateCheck.cs`, `CDPIUI.Shared/Security/CatalogVerifier.cs` |
| Локальные наборы | `LocalItemsInstallerHelper.cs`, `StoreHelper.cs` (`GetReadyLocalItem`, `CopyLocalPack`) |
| Проверки в трее | `CDPIUI.TrayIcon/Helper/MsiInstallerHelper.cs`, `PackageStaging.cs`, `ExecutableTrust.cs`, `TrustedPaths.cs` |

`JSONConvertor` читает все файлы каталога через Newtonsoft.Json с настройками по умолчанию. Поля, которые клиент не знает, игнорируются.

## Источник репозитория

Настройка `STORE/versionControlType` выбирает хост репозитория. Значение по умолчанию — `GitHub`.

| Хост | Репозиторий (`SharedConstants`) | URL архива (`VersionControlData.VersionControlsLinks`) |
|---|---|---|
| `GitHub` | `StoreRepo` = `Storik4pro/CDPIUI-Store` | `https://api.github.com/repos/Storik4pro/CDPIUI-Store/zipball/main` |
| `GitLab` | `GitLabStoreRepo` = `Storik4/CDPIUI-Store` | `https://gitlab.com/Storik4/CDPIUI-Store/-/archive/main/CDPIUI-Store-main.zip` |

Клиент читает только ветку `main`. Он не использует релизы и теги репозитория Магазина.

`LoadAllStoreDatabase` скачивает архив в таких случаях:

- Папки `Store\Cache\Repo` нет.
- Синхронизация принудительная, и с последней синхронизации (`STORE/lastSyncTime`) прошёл один день или больше.

Если загрузка с GitHub завершилась ошибкой, клиент скачивает архив с GitLab.

Архив репозитория проходит такие проверки:

- URL должен быть абсолютным URL HTTPS (`RequestWorker.CreateRequest`).
- Архив должен укладываться в ограничения `ArchivePaths.EnsureWithinLimits` (см. [Ограничения архивов](#archive-limits)).
- Путь каждой записи должен оставаться в целевой папке и не должен проходить через ссылку (`ArchivePaths.GetDestinationOrThrow`, `EnsureNoReparsePoints`).

У репозитория нет подписи. Клиент не проверяет для него каталог.

Клиент удаляет первый сегмент пути каждой записи. Этот сегмент — корневая папка zipball. Запись только с одним сегментом пропускается.
Клиент распаковывает архив в `Repo.new-<GUID>`. Он заменяет `Store\Cache\Repo` только после полной распаковки.

## Структура репозитория

```text
<repository root>/
├── init.json                         # Repository file (RepositoryInitializationModel)
├── <file from localized_strings_directory>   # One localization file for each language
├── <folder from categories_directory>/
│   ├── init.json                     # Category file (RepoCategoryInitializationModel)
│   └── <folder from items_directories>/
│       └── init.json                 # Catalogue item (RepoItemModel)
└── <kits_directory>/
    ├── init.json                     # Kit list (ReadyKitsInitializationModel)
    └── <folder from kits_directories>/
        └── init.json                 # Ready kit (ReadyKitModel)
```

Все пути в этих файлах относительные. Папка категории задаётся относительно корня репозитория. Папка элемента задаётся относительно папки его категории. Папка комплекта задаётся относительно `kits_directory`.
Клиент читает только файлы этого дерева. Другие файлы репозитория он игнорирует.

### Файл репозитория (`init.json`)

| Поле | Тип | Обязательное | Назначение |
|---|---|---|---|
| `categories` | Список строк | Да | Имена категорий в порядке показа. Имя также является ключом локализации заголовка категории. |
| `categories_directory` | Объект: имя → папка | Да | Папка каждой категории. Имя без записи пропускается. |
| `localized_strings_directory` | Объект: код языка → файл | Нет | Файл локализации каждого языка, например `EN` и `RU`. `EN` используется как запасной. Без этого поля все имена показываются как `slocale:<name>`. |
| `kits_directory` | Строка | Нет | Папка готовых комплектов. |

Если `categories` задано, а `categories_directory` нет, клиент не может загрузить каталог. Он сообщает ошибку `Cannot load Items: <message>` (`StoreInternalErrorHappens`).

### Файл локализации

Файл локализации — объект JSON. Каждый ключ — имя из каталога. Каждое значение — переведённый текст.
`GetLocalizedStoreItemName` использует файл языка GUI. Если для языка нет файла, используется файл `EN`.

- Имя, которое содержит пробел, показывается без изменений.
- Имя без перевода показывается как `slocale:<name>`.

### Файл категории (`<category>/init.json`)

| Поле | Тип | Обязательное | Назначение |
|---|---|---|---|
| `store_id` | Строка | Да | Id категории. Каждый элемент категории получает его как `category_id`. |
| `type` | Строка | Да | `basic_category` или `second_category`. Категорию с другим значением клиент пропускает. |
| `items` | Список строк | Да | Имена элементов в порядке показа. |
| `items_directories` | Объект: имя → папка | Да | Папка каждого элемента. Имя без записи пропускается. |

Повреждённый файл категории пропускает только эту категорию. Повреждённый файл элемента пропускает только этот элемент.

### Элемент каталога (`<category>/<item>/init.json`)

Имена полей должны иметь именно такое написание, включая `preffered_to_download_file_name`.

| Поле | Тип | Обязательное | Назначение |
|---|---|---|---|
| `store_id` | Строка | Да | Id элемента. Он должен соответствовать `^[A-Za-z0-9_-]{1,64}$` (`StoreItemIds.IsValid`), иначе установка завершается ошибкой. Это также имя папки элемента. |
| `type` | Строка | Да (не проверяется) | Тип элемента. Код использует `component`, `configlist` и `addon`. Значение записывается в столбец `Type` базы данных элементов. |
| `name` | Строка | Да (не проверяется) | Отображаемое имя. Клиент переводит его через файл локализации. |
| `short_name` | Строка | Нет | Короткое отображаемое имя. |
| `developer` | Строка | Нет | Имя разработчика. |
| `icon` | Строка | Нет | Значок. GUI вычисляет его через LScript, например `$DYNAMICIMAGE(...)`. |
| `background` | Строка | Нет | Цвет фона в шестнадцатеричном виде. |
| `stars` | Строка | Нет | Текст рейтинга. Если поля нет, страница элемента показывает `NaN`. |
| `small_description`, `description` | Строка | Нет | Описания. GUI вычисляет их через LScript для языка Магазина (`EN` или `RU`). |
| `display_warning` | Логическое | Нет | `true` показывает `warning_text` на странице элемента. |
| `warning_text` | Строка | Нет | Текст предупреждения. |
| `links` | Список `{ "name", "url" }` | Нет | Ссылки на странице элемента. |
| `license` | Список `{ "name", "url" }` | Нет | Лицензии. GUI показывает их перед установкой. |
| `dependencies` | Список `[ itemId, version ]` | Нет | Элементы, которые нужны этому элементу. Запись меньше чем из двух значений игнорируется. |
| `target_minversion`, `target_maxversion` | Строка | Нет | Диапазон версий приложения, которые поддерживают элемент (`VersionHelper.IsVersionInRange`). |
| `version_control` | Строка | Да | Как клиент находит загрузку. См. [Источники загрузки](#download-sources). |
| `version_control_link` | Строка | Для `git` и `git_only_last` | URL репозитория на GitHub или GitLab. Клиент читает релизы этого репозитория. Этот URL также используется для проверки обновлений. |
| `download_link` | Строка | Для `git_only_last` и `subscription` | Прямой URL HTTPS файла. |
| `filetype` | Строка | Для `git`, `git_only_last` и `subscription` | Тип загрузки. См. [Типы загрузки](#download-types). |
| `preffered_to_download_file_name` | Строка | Нет | Текст, который должно содержать имя ресурса релиза (с учётом регистра). |
| `sha256` | Строка | Нет | SHA-256 скачанного файла (64 шестнадцатеричные цифры). См. [SHA-256 загрузки](#sha-256-загрузки). |
| `archive_root_folder` | Строка | Нет | Папка архива, которую нужно распаковать. См. [Корень распаковки](#extraction-root). |
| `target_executable_file` | Строка | Нет | Имя файла без расширения для файла, который не распаковывается. Для компонента GUI запускает `<item folder>\<target_executable_file>.exe`. Это должно быть только имя файла: значение с `\`, `/` или `..` останавливает загрузку. |
| `files_to_download` | Список записей загрузки | Для `several_repos` | См. [Запись загрузки](#download-entry-files_to_download). |
| `before_install_actions` | Строка | Нет | Действия LScript перед регистрацией в базе данных. |
| `after_install_actions` | Строка | Нет | Действия LScript после регистрации в базе данных. |

Клиент сам задаёт `category_id`. Не записывайте это поле в файл.

<a id="download-entry-files_to_download"></a>
### Запись загрузки (`files_to_download[]`)

У элементов `several_repos` есть одна запись для каждого файла. Клиент скачивает записи в порядке списка в одну папку элемента.

| Поле | Тип | Обязательное | Назначение |
|---|---|---|---|
| `type` | Строка | Да | Тип загрузки. См. [Типы загрузки](#download-types). |
| `version_control` | Строка | Нет | `external_site_only_last` использует `download_link`. Все другие значения и отсутствие значения используют API релизов. |
| `version_control_link` | Строка | Для API релизов | URL репозитория на GitHub или GitLab. |
| `download_link` | Строка | Для `external_site_only_last` | Прямой URL HTTPS файла. |
| `preffered_version` | Строка | Нет | Тег релиза для загрузки. Пустое значение означает последний релиз. |
| `preffered_to_download_file_name` | Строка | Нет | Текст, который должно содержать имя ресурса релиза (с учётом регистра). |
| `sha256` | Строка | Нет | SHA-256 скачанного файла (64 шестнадцатеричные цифры). См. [SHA-256 загрузки](#sha-256-загрузки). |
| `archive_root_folder` | Строка | Нет | Папка архива, которую нужно распаковать. |
| `actions` | Строка | Нет | `remove` удаляет скопированный файл после загрузки и после установки MSI. Все другие значения сохраняют файл. |

У записи загрузки нет `target_executable_file`. Клиент берёт имя файла из URL.
Версия элемента `several_repos` — это версия последней записи, у которой есть версия. У записи `external_site_only_last` версии нет.

### Готовые комплекты

Список комплектов (`<kits_directory>/init.json`) имеет такие поля:

| Поле | Тип | Обязательное | Назначение |
|---|---|---|---|
| `kits` | Список строк | Да | Имена комплектов в порядке показа. |
| `kits_directories` | Объект: имя → папка | Да | Папка каждого комплекта. Имя без записи пропускается. |

Файл комплекта (`<kit>/init.json`) имеет такие поля:

| Поле | Тип | Обязательное | Назначение |
|---|---|---|---|
| `store_id` | Строка | Да | Id комплекта. |
| `items` | Список строк | Да | Id элементов комплекта. Список не должен быть пустым. |
| `name`, `short_name`, `icon`, `background`, `small_description`, `description` | Строка | Нет | Данные для показа, как у элемента. |
| `recommended` | Логическое | Нет | Отмечает рекомендованный комплект. Старое написание `recomended` тоже читается. |

Комплект без `store_id` или с пустым списком `items` пропускается.

ВНИМАНИЕ: Файл комплекта с недопустимым JSON останавливает загрузку всех комплектов, которые идут после него в `kits`.

<a id="download-sources"></a>
## Источники загрузки

`version_control` выбирает, как клиент находит ссылку для загрузки. Регистр значения не учитывается. Неизвестное значение вызывает исключение, и установка завершается ошибкой.

| `version_control` | Ссылка | Версия |
|---|---|---|
| `git` | Ресурс релиза `version_control_link`, который соответствует `filetype` | Тег релиза |
| `git_only_last` | `download_link`. Клиент также читает релиз `version_control_link`, и ресурс должен соответствовать `filetype`. | Тег релиза |
| `several_repos` | Одна ссылка для каждой записи `files_to_download` | Версия последней записи, у которой она есть |
| `subscription` | `download_link` | Всегда `0.0.0` |

Установленного элемента приложения (`CDPIUIAppSt`) нет в каталоге. `GetItemInfoFromStoreId` создаёт его данные: `version_control` равно `git`, `target_executable_file` равно `patch`, а `filetype` равно `CDPIUIUpdateItem` (портативная установка) или `UPDmsi` (установка MSI).

### API релизов

`APIWorker` читает релиз с хоста из настройки Магазина (`STORE/versionControlType`), а не с хоста URL:

| Хост | Последний релиз | Заданная версия |
|---|---|---|
| GitHub | `https://api.github.com/repos/<owner>/<repo>/releases/latest` | `.../releases/tags/<version>` |
| GitLab | `https://gitlab.com/api/v4/projects/<owner%2Frepo>/releases/permalink/latest` | `.../releases/<version>` |

Клиент выбирает ресурс релиза по таким правилам:

1. Имя ресурса равно значению `filetype`, или имя заканчивается расширением типа (см. [Типы загрузки](#download-types)).
2. Если задано `preffered_to_download_file_name`, имя содержит это значение.
3. Подходить должен ровно один ресурс. Если ресурсов нет, результат — `INVALID_URI`. Если ресурсов больше одного, результат — `TOO_MANY_VARIANTS`.

Клиент отправляет запрос к API релизов с токеном (`Secret.GitHubToken` или `Secret.GitLabToken`), если токен задан. Без токена запрос анонимный: открытые репозитории работают с более низким лимитом запросов API, закрытые возвращают ошибку HTTP.

### Проверка обновлений

`StoreHelper.CheckUpdates` проверяет все установленные элементы, кроме `LocalUserStorage` и элементов с `version_control` `local`.
Он использует сохранённый URL обновлений, если его хост совпадает с хостом Магазина. Иначе он использует `version_control_link` из каталога.
Обновление доступно, если тег последнего релиза новее установленной версии (`VersionHelper.CompareVersionStrings`).

## Проверки для всех онлайн-загрузок

`RequestWorker` и `DownloadWorker` применяют к каждой загрузке такие проверки:

- URL должен быть абсолютным URL HTTPS. Это также относится к запросам `DOWNLOAD=` из действий установки. У запроса `DOWNLOAD=` тоже может быть SHA-256: см. `download_easy_designer_annotation_file` в [LScript/README.ru.md](../LScript/README.ru.md).
- Клиент отправляет токен Магазина только на `github.com`, `api.github.com`, `codeload.github.com`, `raw.githubusercontent.com`, `objects.githubusercontent.com` и `gitlab.com`.
- Если сервер сообщает размер файла, на диске должно быть достаточно свободного места.
- Загрузка останавливается, если данные не приходят 60 секунд.
- Статус ошибки HTTP даёт `ERR_DOWNLOAD_UNEXPECTED_STATUS_CODE_<code>`.
- Файл сначала попадает в `TempFiles\Downloads`. Клиент удаляет его после операции.

Если `STORE/ProxyEnable` равно `true` и хост Магазина — GitHub, клиент отправляет запросы через прокси Магазина (`Secret.ProxyURL`).

### SHA-256 загрузки

У `DownloadWorker.DownloadAndExtractAsync` есть параметр `expectedSha256`. Если он задан, клиент вычисляет SHA-256 скачанного файла перед распаковкой или установкой.

- Символы, которые не являются шестнадцатеричными цифрами, игнорируются. Значение должно содержать 64 шестнадцатеричные цифры.
- Если хеш отличается или значение не содержит 64 шестнадцатеричные цифры, загрузка завершается ошибкой `ERR_DOWNLOAD_HASH_MISMATCH`.

Файл каталога может задать хеш в поле `sha256`:

- В записи `files_to_download` метод `APIWorker.GetDownloadLinksAsync` копирует `FileToDownload.sha256` в `DownloadLinkModel.sha256`.
- В элементе без `files_to_download` сервис `OnlineItemsInstallationService` копирует `RepoItemModel.sha256` в ссылку из API релизов.

`OnlineItemsInstallationService` передаёт `DownloadLinkModel.sha256` в `expectedSha256`. Пустое или отсутствующее поле отключает проверку.

ВНИМАНИЕ: Хеш закрепляет один файл. Задавайте его только вместе с фиксированной версией (`preffered_version`) или фиксированной ссылкой. При `git_only_last` или `subscription` у каждого нового релиза новый хеш, и клиент отклоняет загрузку, пока каталог не обновят.

<a id="download-types"></a>
## Типы загрузки

Тип — это значение `FileExtensionTypes`. `OnlineItemsInstallationService` читает его без учёта регистра. Неизвестное значение становится `temp`.
API релизов требует известного типа: для неизвестного значения выбор ресурса вызывает исключение, и установка завершается ошибкой.
Столбец расширения содержит расширение, которое должно быть у ресурса релиза, и расширение скопированного файла (`FileSystemService.FileExtensions`).

| Тип | Расширение | Обработка | Проверки |
|---|---|---|---|
| `signedZip` | `.cdpisignedpack` | Распаковывается в `<item folder>.verify-<GUID>`. Файлы перемещаются в папку элемента, только если каталог действителен. Существующие файлы заменяются. | Ограничения архивов, подпись каталога |
| `archive` | `.zip` | Распаковывается прямо в папку элемента. Файлы `.txt` объединяются. | Ограничения архивов |
| `configPack` | `.cdpiconfigpack` | Так же, как `archive` | Ограничения архивов |
| `WIN32application` | `.exe` | Копируется в папку элемента | В GUI нет. Трей проверяет программу при запуске (см. [Запуск с правами администратора](#elevated-start)). |
| `msi` | `.msi` | Копируется в папку элемента. Затем трей устанавливает его командой `msiexec.exe /i "<file>" /passive /norestart`. | Authenticode в трее |
| `elmsi` | `.exe` | Копируется в папку элемента. Затем трей запускает его с `/install /passive /norestart`. | Authenticode в трее |
| `CDPIUIUpdateItem` | `.cdpipatch` | Только элемент приложения. Копируется как `patch.cdpipatch`. Затем `ApplicationUpdateService` готовит патч. | Подпись каталога каждого слоя, в GUI и в трее |
| `UPDmsi` | `.msi` | Только элемент приложения. Копируется как `patch.msi`. GUI его не устанавливает. Трей запускает его для обновления приложения. | Закреплённая подпись Authenticode в трее |
| `lst` | `.lst` | Копируется в папку элемента | Нет |
| `raw` | `.txt` | Копируется в папку элемента | Нет |
| `temp`, `none` | `.cdpitempfile`, нет | Копируется в папку элемента | Нет |

Распаковываются только `signedZip`, `archive` и `configPack` (`FileSystemService.CompressedFileTypes`).
Онлайн-загрузки никогда не пропускают файлы при распаковке. Список пропуска есть только у локальных наборов без подписи.

### Имя скопированного файла

Файл, который не распаковывается, получает имя `<name><extension>`:

- `<name>` — это `target_executable_file`, если оно задано. У записи загрузки `several_repos` нет `target_executable_file`.
- Иначе `<name>` — последний сегмент пути URL без расширения.
- `<extension>` берётся из таблицы выше.

Для `msi` и `elmsi` GUI передаёт трею скопированный файл `<item folder>\<name><extension>`.

ПРИМЕЧАНИЕ: Для элемента `msi` или `elmsi` установщик запускается из скопированного файла, поэтому `target_executable_file` задаёт и имя установщика.

Если установка `msi` или `elmsi` завершилась ошибкой, клиент удаляет скопированный установщик.

### Подпись каталога

Проверка каталога (`ZipService.VerifyCatalog`, `CatalogVerifier.VerifyAsync`) состоит из таких шагов:

1. Файл `catalog.cat` должен находиться в корне распаковки. Если его нет, результат — `NoSignature`.
2. Каждый сертификат подписавшего каталог должен быть в `Secret.TrustedCertificates` (отпечатки SHA-1). Подпись должна быть действительной. Цепочка не строится.
3. `Test-FileCatalog` (Windows PowerShell) должен вернуть `Valid` для распакованной папки. Пропускается только `catalog.cat`. Ограничение времени — 2 минуты.

Если один шаг завершился ошибкой, папка распаковки удаляется, и установка завершается ошибкой. Ни один файл не попадает в папку элемента.
`catalog.cat` перемещается в папку элемента вместе с другими файлами. Позже его использует `ExecutableTrust`.

<a id="archive-limits"></a>
### Ограничения архивов

`ArchivePaths` применяет такие ограничения к архивам Магазина, локальным наборам и патчам приложения:

| Ограничение | Значение |
|---|---|
| Записи | Не более 20000 (`MaxEntries`) |
| Размер после распаковки | Не более 1 ГиБ (`MaxTotalBytes`) |
| Степень сжатия | Не более 200 для записи размером 1 МиБ или больше (`MaxCompressionRatio`, `RatioCheckMinimumBytes`) |
| Размер записи | Запись не должна давать больше данных, чем указано в её заголовке (`ExtractToFileBounded`) |
| Путь | Путь должен оставаться в целевой папке. Буква диска и корневой путь отклоняются. |
| Ссылки | Целевой путь и его папки не должны быть ссылками или точками соединения (`EnsureNoReparsePoints`). |

<a id="extraction-root"></a>
### Корень распаковки

`archive_root_folder` выбирает папку архива, которую распаковывает клиент (`ZipService.ExtractEntries`):

- Пустое значение или `/` распаковывает весь архив.
- Клиент удаляет `/` в конце и делит значение на сегменты по `/`.
- Каждый сегмент должен быть равен сегменту пути записи. Регистр не учитывается.
- `$ANY` соответствует любому одному сегменту, например корневой папке архива релиза с номером версии.
- Записи вне корня игнорируются. Клиент удаляет сегменты корня из пути каждой распакованной записи.

Пример: с `archive_root_folder` `$ANY/bin` запись `<any folder>/bin/<file>` распаковывается как `<file>`, а запись `<any folder>/<file>` игнорируется.

### Объединение текстовых файлов

Если архив не подписан, клиент объединяет файлы `.txt`:

- Если целевой файл `.txt` существует, клиент добавляет строки архива, которые не пустые и которых ещё нет в файле. Он сравнивает строки без начальных и конечных пробелов.
- Если существует `<file>.txt.bak`, клиент добавляет строки в файл `.bak`, а не в файл `.txt`.

Подписанный архив заменяет файлы `.txt`.

## Локальные наборы

GUI принимает локальные наборы при активации файла (`App.ProcessFiles`) и из настроек Магазина (`ManualItemInstallation_Click` в `Views/Store/SettingsPage.xaml.cs`). Оба пути открывают `StoreLocalItemInstallingDialog`.

| Файл | Подписан | Проверка каталога | Пропускаемые файлы | `ExecutableFile` и действия установки |
|---|---|---|---|---|
| `.cdpisignedpack` | Да | Да | Нет | Используются |
| `.cdpiconfigpack` и все другие расширения | Нет | Нет | См. ниже | Игнорируются |

`LocalItemsInstallerHelper` сравнивает расширение с `.cdpisignedpack` с учётом регистра. Файл с расширением `.CDPISIGNEDPACK` обрабатывается как набор без подписи.

Набор без подписи не может содержать файлы с такими расширениями (`UnsignedPackSkippedFiles`):
`.exe`, `.dll`, `.sys`, `.msi`, `.com`, `.scr`, `.cpl`, `.bat`, `.cmd`, `.ps1`, `.vbs`, `.vbe`, `.js`, `.jse`, `.wsf`, `.hta`, `.lnk`, `.url`.
Клиент пропускает эти файлы при распаковке. Он не показывает ошибку.

Клиент обрабатывает локальный набор в такой последовательности:

1. Он распаковывает весь архив в `TempFiles\Offline\<temporary name>`. Подписанный набор проходит через папку `.verify-<GUID>` и проверку каталога.
2. Файл `init.json` должен находиться в корне архива. Иначе ошибка — `ERR_LOCAL_ITEM_UNSUPPORTED`.
3. `StoreId` должен соответствовать `^[A-Za-z0-9_-]{1,64}$`. Он не должен быть `CDPIUIAppSt` или `LocalUserStorage`.
4. Очередь перемещает распакованную папку в `Store\Items\<StoreId>`. Старая папка перемещается в `<folder>.previous-<GUID>` и возвращается, если перемещение не удалось.
5. Клиент регистрирует элемент с `filetype` `localPack` и `version_control` `local`. Проверка обновлений его пропускает.

Ограничения архивов относятся ко всем локальным наборам. Значок набора (`$DYNAMICIMAGE(...)`) показывается в диалоге, только если файл находится в наборе и имеет расширение `.png`, `.jpg`, `.jpeg`, `.ico`, `.bmp` или `.gif`.

### Файл набора (`init.json` локального набора)

Поля берутся из `LocalItemInitModel`.

| Поле | Тип | Обязательное | Назначение |
|---|---|---|---|
| `StoreId` | Строка | Да | Id элемента и имя папки. |
| `Type` | Строка | Нет | Тип элемента. Значение по умолчанию — `configlist`. |
| `Version` | Строка | Нет | Установленная версия. |
| `Name` | Строка | Нет | Отображаемое имя. По умолчанию — `ShortName`, затем `StoreId`. |
| `ShortName` | Строка | Нет | Короткое имя. По умолчанию — `StoreId`. |
| `Icon` | Строка | Нет | Значок, например `$DYNAMICIMAGE(icon.png)`. Клиент сохраняет полный путь, если файл находится в папке элемента. |
| `Color` | Строка | Нет | Цвет фона. |
| `Developer` | Строка | Нет | Имя разработчика. |
| `Requirements` | Список `[ itemId, version ]` | Нет | То же, что `dependencies` элемента каталога. |
| `ExecutableFile` | Строка | Нет | То же, что `target_executable_file`. Только для подписанных наборов. |
| `BeforeInstallActions`, `AfterInstallActions` | Строка | Нет | То же, что `before_install_actions` и `after_install_actions`. Только для подписанных наборов. |

### Патч приложения (`.cdpipatch`)

Локальный файл `.cdpipatch` открывает `ApplicationUpdateFileDialogWindow`. `ApplicationUpdate.InstallApplicationUpdateFromFile` добавляет в очередь элемент `CDPIUIAppSt` с этим файлом.
Клиент копирует файл в `Store\Items\CDPIUIAppSt\patch.cdpipatch`. В портативной установке `ApplicationUpdateService` затем проверяет патч. Шаги и формат описаны в [.github/RELEASES.ru.md](../../.github/RELEASES.ru.md#how-the-application-applies-a-patch).

### Общий пресет (`.cdpiconfig`)

Файл `.cdpiconfig` не является элементом Магазина. Его читает ConfigShare (`CDPIUI.AddOns/ConfigShare`). У ConfigShare свои ограничения: 4096 записей, 512 МиБ, степень сжатия 200 для записей больше 1 МиБ, манифест не более 4 МиБ и SHA-256 для каждого ресурса. Он отклоняет символические ссылки и параметры, которые записывают файлы вне пакета. Он не выполняет действия установки.
См. [CDPIUI.AddOns/ConfigShare/README.ru.md](../../CDPIUI.AddOns/ConfigShare/README.ru.md).

## Сводка проверок

| Источник | Подпись каталога | Authenticode | Ограничения архивов | Пропускаемые файлы | Действия установки | `target_executable_file` |
|---|---|---|---|---|---|---|
| Онлайн `signedZip` | Да | Нет | Да | Нет | Да | Да |
| Онлайн `archive`, `configPack` | Нет | Нет | Да | Нет | Да | Да |
| Онлайн `WIN32application`, `lst`, `raw` | Нет | Нет | Не применяется | Не применяется | Да | Да |
| Онлайн `msi`, `elmsi` | Нет | Да, в трее (цепочка до доверенного корня) | Не применяется | Не применяется | Да | Не задавайте |
| `.cdpisignedpack` | Да | Нет | Да | Нет | Да | Да (`ExecutableFile`) |
| `.cdpiconfigpack` и другие локальные наборы | Нет | Нет | Да | `UnsignedPackSkippedFiles` | Игнорируются | Игнорируется |
| `.cdpipatch` (онлайн или локальный) | Да, каждый слой, в GUI и в трее | Нет | Да | Нет | Нет | Не применяется |
| `patch.msi` (`UPDmsi`) | Нет | Да, закреплённый сертификат, в трее | Не применяется | Не применяется | Нет | Не применяется |
| Действие `install_msi` | Нет | Да, в трее | Не применяется | Не применяется | Не применяется | Не применяется |

## Папка установки

Все элементы Магазина устанавливаются в `<DataDirectory>\Store\Items\<store_id>`. `DataDirectory` описана в [CDPIUI.Core/README.ru.md](../README.ru.md).

- Ни один вызов не задаёт `cleanDirectoryBeforeInstalling` равным `true`. Поэтому онлайн-обновление записывает файлы поверх старой папки, и файлы прежних версий остаются.
- База данных элементов (`storedata.db`) хранит папку, `target_executable_file` как `Executable`, `filetype` как `DownloadFileType`, `version_control` как `VersionControlType` и `version_control_link` как `UpdateCheckUrl`.

## Установщики в трее

`MsiInstallerService` передаёт трею путь установщика. `MsiInstallerHelper.ProcessMsi` в трее выполняет такие шаги:

1. Путь должен находиться в папке приложения (`TrustedPaths.IsAllowed`). Разрешённые папки — папка трея, `%LOCALAPPDATA%\CDPIUI` пользователя трея и `%LOCALAPPDATA%\CDPIUI` пользователя рабочего стола.
2. `PackageStaging.Stage` копирует файл в `<tray folder>\.staging\<GUID>\`. Трей проверяет и запускает эту копию.
3. `PackageStaging.HasValidSignature` вызывает `WinVerifyTrust`. Подпись должна быть действительной, а цепочка должна доходить до доверенного корня. Отзыв сертификата не проверяется.
4. Файл `.msi` запускается командой `msiexec.exe /i "<file>" /passive /norestart`. Другие файлы запускаются с `/install /passive /norestart`.
5. Коды выхода `0` и `1638` дают `Complete`. `3010` и `1641` дают `CompleteRestartRequest`. Другие коды дают `ExceptionHappens`.

GUI ждёт результата не более 30 минут.

<a id="elevated-start"></a>
## Запуск с правами администратора

Трей проверяет каждую программу перед запуском с правами администратора: процесс компонента, получение справки программы и GoodCheck (`ExecutableTrust.AcquireAsync`).

1. Путь должен находиться в папке приложения (`TrustedPaths.IsAllowed`). Иначе программа не запускается.
2. Если установка портативная (`Utils.IsInstalledByMsi` равно `false`) или программа находится в папке трея, других проверок нет.
3. Трей открывает дескрипторы чтения для программы и для всех файлов `.exe`, `.dll` и `.sys` в её папке (`ExecutableLease`). Пока дескрипторы открыты, эти файлы нельзя изменить.
4. Трей вычисляет отпечаток: SHA-256 по именам и SHA-256 этих файлов.
5. Если в `<tray folder>\.trust\approved.txt` для этого пути есть тот же отпечаток, программа запускается.
6. Иначе, если программа находится в папке элемента Магазина (`Store\Items\<id>`) с действительным `catalog.cat`, программа запускается. Эта проверка сравнивает только файлы кода (`.exe`, `.dll`, `.sys`). Имена всех других файлов пропускаются, потому что пользователь может менять списки и настройки.
7. Иначе трей спрашивает пользователя в окне сообщения. Окно показывает путь, SHA-256 и подписавшего. Ответ по умолчанию — «Нет».
8. После ответа «Да» или после проверки действительного каталога трей записывает отпечаток в `approved.txt`.

Поэтому в установке MSI элемент `signedZip` или `.cdpisignedpack` с каталогом запускается без вопроса. Компонент из загрузки `archive` или `WIN32application` требует подтверждения пользователя после каждого изменения его файлов кода.
