[English](RELEASES.md) | **Русский**

# CI и релизы

Файлы workflow находятся в `.github/workflows`. Скрипты, которые они запускают, находятся в `.github/scripts`.
Все задания (jobs) выполняются на `windows-latest`. Локальная сборка описана в [CONTRIBUTING.ru.md](CONTRIBUTING.ru.md).

## CI (`workflows/ci.yml`)

CI запускается при push в `main`, `dev` и `fix/**`, а также для pull request. У него есть только право
на чтение `contents`. Новый запуск для того же ref отменяет текущий запуск.

| Задание | Шаги |
|-----|-------|
| `libraries` | Устанавливает .NET 8 SDK. Запускает `create_secret_stub.ps1`. Собирает `CDPIUI.Shared`, `CDPIUI.Core` (x64), `CDPIUI.AddOns` и `CDPIUI.TrayIcon` (x64) в конфигурации Release. Запускает `dotnet test CDPIUI.Tests`. Восстанавливает пакеты этих четырёх проектов, `CDPIUI` и `CDPIUI.Tests` с `NuGetAuditMode=all` и завершается с ошибкой, если у прямого или транзитивного пакета есть известная уязвимость средней или более высокой серьёзности (предупреждения NU1902–NU1904 становятся ошибками). |
| `gui` | Запускает `build_portable.ps1` и `check_setup_files.ps1`. Собирает MSI. Загружает `NewSetup/bin/**/*.msi` как артефакт `setup-msi` на 7 дней. |
| `tools` | Устанавливает .NET 8 SDK. Собирает `Update/Update.csproj` и `EditHostFile/EditHostFile.csproj` (x64) в конфигурации Release с помощью `dotnet build`. Публикует каждый проект одним файлом, зависящим от среды выполнения (`win-x64`), в `artifacts/tools/<проект>`. Завершается с ошибкой, если в папке публикации есть другой файл, кроме файла `.exe`. |

У задания `gui` включён `continue-on-error`. Для сборки WinUI нужны фид CommunityToolkit Labs и
инструменты Windows App SDK. Задание носит только информационный характер, пока сборку нельзя
воспроизвести на hosted runners.

## Файлы релиза (`workflows/build_and_patch.yml`)

Пользователь запускает этот workflow вручную (`workflow_dispatch`).

### Входные параметры

| Параметр | По умолчанию | Значение |
|-------|---------|---------|
| `version` | пусто | Версия для `requirements.json`. Если значение пустое, workflow использует первый `FileVersion` в `CDPIUI/CDPIUI.csproj`. Значение должно соответствовать `^\d+(\.\d+){1,3}$`. |
| `previous_repository` | `Storik4pro/cdpiui` | Репозиторий, в релизе которого находится предыдущий `_portable.zip`. |
| `previous_tag` | пусто | Тег релиза с предыдущим `_portable.zip`. Если значение пустое, workflow использует последний релиз. Со значением `none` workflow создаёт полный патч. |
| `patch_urls` | пусто | Патчи, которые приложение должно установить сначала, через пробел. Workflow записывает их в `patch_urls`. |

### Секреты

| Секрет | Назначение |
|--------|-----|
| `STORE_GITHUB_TOKEN`, `STORE_GITLAB_TOKEN` | Токены Магазина в `Secret.cs` (`GitHubToken`, `GitLabToken`). Приложение делает их публичными, поэтому у них не должно быть доступа на запись (см. `create_release_secret.ps1`). |
| `STORE_PROXY_URL`, `STORE_PROXY_KEY` | Обратный прокси Магазина в `Secret.cs` (`ProxyURL`, `ProxyKey`). Ключ есть в каждой копии приложения. Поэтому прокси не должен доверять ключу больше, чем анонимному клиенту: ограничьте на прокси частоту запросов и разрешённые адреса. |
| `TRUSTED_CERTIFICATES` | SHA-1 отпечатки для `Secret.TrustedCertificates`, разделённые пробелами, запятыми или точками с запятой. |
| `CATALOG_CERTIFICATE_PFX_BASE64` | PFX-файл сертификата подписи каталога в Base64. Шаг создания патча записывает его в `RUNNER_TEMP\catalog.pfx` и удаляет его в конце шага. |
| `CATALOG_CERTIFICATE_PASSWORD` | Пароль PFX-файла. |

Все секреты для workflow необязательны. Шаг «Create Secret.cs» передаёт их в
`create_release_secret.ps1` как переменные окружения. PFX передаётся в `CATALOG_CERTIFICATE`.

ПРЕДУПРЕЖДЕНИЕ: Задайте `CATALOG_CERTIFICATE_PFX_BASE64` и `CATALOG_CERTIFICATE_PASSWORD` до того, как
создадите релиз. Без сертификата у каталогов нет подписи, и приложение отклоняет
патч.

ПРЕДУПРЕЖДЕНИЕ: Убедитесь, что `TrustedCertificates` не пуст в релизной сборке. Задайте
`TRUSTED_CERTIFICATES` или сертификат каталога. С пустым списком релизная сборка
отклоняет все подписанные каталоги, в том числе свои собственные патчи.

### Шаги

1. Записать `CDPIUI.Shared/Secrets/Secret.cs` с помощью `create_release_secret.ps1 -Force` (шаг «Create Secret.cs»).
2. Собрать портативную раскладку (`build_portable.ps1`). Скрипт сохраняет файл `Secret.cs` из шага 1.
3. Проверить список файлов установщика (`check_setup_files.ps1`).
4. Собрать MSI.
5. Определить версию.
6. Скачать предыдущий `_portable.zip` с помощью `gh release download`. Workflow пропускает этот шаг, если `previous_tag` равен `none`.
7. Создать патч с помощью `make_patch.ps1`.
8. Создать портативный архив:
   1. Скопировать папку publish.
   2. Переименовать `Update.exe` в `_Update.exe`.
   3. Создать `catalog.cat` (версия 2.0) для всей папки.
   4. Подписать каталог, если сертификат доступен. Если в подписи нет сертификата подписывающего лица, шаг завершается с ошибкой.
   5. Поместить каталог в папку и создать ZIP-файл.
9. Скопировать `Setup.msi` и подписать его сертификатом каталогов (Authenticode, SHA-256). Если в подписи нет сертификата подписывающего лица, шаг завершается с ошибкой. Без сертификата шаг выводит предупреждение: установленная копия не принимает неподписанный `Setup.msi` как обновление.

### Результат

Workflow создаёт артефакт `release-<version>`. Он содержит следующие файлы:

- `<version>_patch.cdpipatch`
- `_portable.zip`
- `Setup.msi`

Workflow не создаёт релиз на GitHub.

## Скрипты (`scripts/`)

### `build_portable.ps1`

Параметр: `-Configuration` (по умолчанию `Release`). Скрипту нужны `msbuild` (для GUI) и .NET 8 SDK.
Скрипт выполняет следующие шаги:

1. Запускает `create_secret_stub.ps1`.
2. Удаляет старую папку publish `CDPIUI/bin/x64/<Configuration>/net8.0-windows10.0.22621.0/publish`.
3. Публикует `CDPIUI/CDPIUI.csproj` в эту папку (x64, `win-x64`, framework-dependent).
4. Удаляет языковые папки, которые содержат только файлы `.mui`, кроме `en-us` и `ru-ru`. Windows App SDK копирует примерно 90 языков.
5. Публикует `CDPIUI.TrayIcon` как один файл в `artifacts/tools/tray`.
6. Публикует `Update` и `EditHostFile` как один файл каждый (x64, `win-x64`, framework-dependent) в `artifacts/tools/update` и `artifacts/tools/edithostfile`.
7. Копирует `CDPIUI_TrayIcon.exe`, `Update.exe` и `EditHostFile.exe` в папку publish.

`Update.exe` заменяет файлы приложения во время работы. Поэтому он не должен загружать DLL из
папки установки. Один файл содержит весь код инструмента.

### `check_setup_files.ps1`

У этого скрипта нет параметров. Он сравнивает два списка без учёта регистра:

- файлы в папке publish конфигурации Release, без файлов `*.pdb`;
- записи `Source="$(var.SourceDir)\..."` в `NewSetup/NewSetupFiles.wxs`.

Скрипт завершается с ошибкой, если файла нет в одном из списков.
Правило для списка файлов описано в [NewSetup/README.ru.md](../NewSetup/README.ru.md).

### `create_secret_stub.ps1`

У этого скрипта нет параметров. Git игнорирует `CDPIUI.Shared/Secrets/Secret.cs`. Скрипт выполняет
следующие шаги:

- Если файла нет, скрипт записывает класс `Secret` с пустыми токенами и пустым списком
  `TrustedCertificates`. С этим классом решение компилируется.
- Скрипт не изменяет существующий файл.

Ключ pipe не является секретом сборки. Трей создаёт новый ключ при каждом запуске (см. [Pipe](../CDPIUI.Shared/Pipe/README.ru.md)).

### `create_release_secret.ps1`

Этот скрипт запускает релизный workflow. Скрипт записывает `Secret.cs` из следующих переменных окружения:

| Переменная | Член класса |
|----------|--------|
| `STORE_GITHUB_TOKEN`, `STORE_GITLAB_TOKEN` | `GitHubToken`, `GitLabToken` |
| `STORE_PROXY_URL`, `STORE_PROXY_KEY` | `ProxyURL`, `ProxyKey` |
| `TRUSTED_CERTIFICATES` | `TrustedCertificates` |
| `CATALOG_CERTIFICATE`, `CATALOG_CERTIFICATE_PASSWORD` | `TrustedCertificates`, если `TRUSTED_CERTIFICATES` не даёт ни одного отпечатка |

Правила:

- Скрипт оставляет только отпечатки из 40 шестнадцатеричных символов, в верхнем регистре.
- Если `TRUSTED_CERTIFICATES` не даёт ни одного отпечатка, скрипт использует отпечаток PFX из `CATALOG_CERTIFICATE`.
- Если отпечатка нет, скрипт выводит предупреждение. Тогда сборка отклоняет подписанные пакеты и патчи.
- Скрипт проверяет токены Магазина до записи, потому что приложение делает их публичными:
  - У классического токена GitHub не должно быть областей (заголовок `X-OAuth-Scopes` ответа `https://api.github.com/rate_limit` пуст). У детализированного (fine-grained) токена в этом заголовке нет областей; выдавайте ему только доступ на чтение к публичным репозиториям.
  - У токена GitLab могут быть только `read_api`, `read_repository` и `read_registry` (`/api/v4/personal_access_tokens/self`).
  - Если у токена есть другие области или скрипт не может его проверить, скрипт завершается с ошибкой.
- Если `Secret.cs` существует, скрипт его не изменяет. С `-Force` скрипт заменяет его.

### `make_patch.ps1`

Скрипту нужен PowerShell 7. Windows PowerShell 5.1 может записывать `\` в имена записей ZIP, а
программа обновления не считает такие имена папками.

| Параметр | Обязательный | Значение |
|-----------|----------|---------|
| `-CurrentDir` | Да | Папка publish новой сборки. |
| `-PreviousZip` | Нет | Предыдущий `_portable.zip`. Если значение пустое, патч содержит все файлы. |
| `-Version` | Да | Версия для `requirements.json`, от `1.2` до `1.2.3.4`. |
| `-PatchUrls` | Нет | Значение `patch_urls` для `requirements.json`. |
| `-OutPatch` | Нет | Выходной файл. По умолчанию `patch.cdpipatch`. |
| `-CertificatePath`, `-CertificatePassword` | Нет | PFX-файл, которым подписывается каталог, и его пароль. |
| `-TimestampServer` | Нет | Сервер меток времени для `Set-AuthenticodeSignature`. Workflow его не использует. |

Правила:

- Патч содержит файл, если в предыдущей сборке его нет или если его хеш SHA-256 отличается.
- Скрипт игнорирует `catalog.cat` в текущей папке.
- Скрипт сохраняет и сравнивает `Update.exe` как `_Update.exe`.
- Скрипт завершается с ошибкой, если ни один файл не изменился.
- Скрипт завершается с ошибкой, если шаг подписи завершился с ошибкой.
- Скрипт завершается с ошибкой, если `Test-FileCatalog` не возвращает `Valid` для нового каталога.

## Формат `.cdpipatch`

Файл `.cdpipatch` — это ZIP-архив со следующей структурой:

```
requirements.json     {"patch_urls": [...], "version": "x.y.z.w"}
CDPIUI/catalog.cat    signed catalog (version 2.0) of all other files in CDPIUI/
CDPIUI/...            the changed files, at their installed paths
```

`make_patch.ps1` записывает `requirements.json` в UTF-8 без BOM. Приложение читает его в
`PatchRequirementsModel` (`CDPIUI.Core/Store/Application/PatchRequirementsModel.cs`).

<a id="how-the-application-applies-a-patch"></a>
## Как приложение применяет патч

<a id="gui-side"></a>
### Сторона GUI

`CDPIUI.Core/Store/Application/ApplicationUpdateService.cs` подготавливает патч. Для портативной
установки (`State.IsApplicationBuildAsSingleFile`) Магазин помещает патч в
`<Store items>/<application id>/patch.cdpipatch`. Затем `GetPatchReadyToInstall` выполняет следующие шаги:

1. Распаковывает патч в `Patches\<patch file name without extension>` в той же папке.
2. Читает `requirements.json`. Пустой `version` вызывает ошибку.
3. Отклоняет патч, если установленная версия такая же или новее.
4. Проверяет `CDPIUI/catalog.cat` по папке `CDPIUI/`.
5. Обрабатывает каждую запись `patch_urls`. Сервис пропускает пустые записи.
   1. Скачивает обязательный патч в `required/<index>` в папке патча. Индекс равен 0 для первой записи, 1 для следующей записи и так далее.
   2. Распаковывает весь архив.
   3. Читает версию из `requirements.json` обязательного патча. Если файл или версия недействительны, операция завершается с ошибкой.
   4. Если установленная версия такая же или новее, пропускает обязательный патч. Также пропускает его, если его версия такая же или новее версии основного патча, потому что основной патч всегда является последним слоем.
   5. Проверяет каталог папки `CDPIUI/` обязательного патча.
6. Сортирует обязательные патчи по версии, от самой старой. Порядок в `patch_urls` не важен.
7. Записывает новый `patch.cdpipatch` с одним слоем для каждого патча (`layers/0/`, `layers/1/`, ...).
   Основной патч — последний слой. Каждый слой сохраняет свой собственный `catalog.cat`.
8. Удаляет папку `Patches`.

Когда операция Магазина завершается без ошибок, GUI отправляет трею сообщение `BeginApplicationUpdate`
(`CDPIUI.Core/Features/ApplicationUpdate.cs`). Для установки из MSI
файл называется `patch.msi`. Трей устанавливает его командой `msiexec /i ... /qn+` только если у него
есть подпись Authenticode сертификатом из `Secret.TrustedCertificates` (`PackageStaging.HasPinnedSignature`).

### Сторона трея

`CDPIUI.TrayIcon/Helper/Utils.cs` и `CDPIUI.TrayIcon/Helper/PackageStaging.cs` выполняют следующие шаги:

1. Проверяют путь с помощью `TrustedPaths.IsAllowed`.
2. Копируют файл в `<install>\.staging\<GUID>\`. При установке для всех пользователей (per-machine) писать в эту папку могут только администраторы. Поэтому никто не может заменить файл между проверкой и установкой.
3. Проверяют ограничения архива (`ArchivePaths`: записи, размер, степень сжатия, ссылки). Затем распаковывают копию в `verify\`.
4. Проверяют каталог каждой папки `layers/*`. Если слоёв нет, проверяют папку `CDPIUI/`.
5. Если проверка не пройдена, останавливаются и показывают уведомление `UpdateFailure`.
6. Запускают `Update.exe --directory-to-zip "<staged file>" --destination-directory "<install>"`.

`Update.exe` описан в [Update/README.ru.md](../Update/README.ru.md). Трей удаляет папку `.staging`
при каждом запуске.

`Update.exe` не копирует файл `catalog.cat` из патча в папку установки. Он также
не удаляет файлы, которых нет в патче, потому что патч содержит только изменённые файлы.

### Проверка каталога

`CDPIUI.Shared/Security/CatalogVerifier.cs` проверяет каталог по следующим правилам:

- Каталог должен быть подписанным сообщением CMS с одним или несколькими подписывающими лицами.
- SHA-1 отпечаток сертификата каждого подписывающего лица должен быть в `Secret.TrustedCertificates`.
- Подпись каждого подписывающего лица должна быть действительной.
- Проверяющий модуль не строит цепочку сертификатов, потому что сертификаты закреплены (pinned) и самоподписаны.
- `Test-FileCatalog ... -FilesToSkip catalog.cat` должен вернуть `Valid` не более чем за 2 минуты.
- Проверка не учитывает отзыв и срок действия сертификата. Правила для сертификатов описаны в [CDPIUI.Shared/README.ru.md](../CDPIUI.Shared/README.ru.md#security).

Проверяющий модуль запускает `Test-FileCatalog` в `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`.
Он передаёт пути через переменные окружения.
