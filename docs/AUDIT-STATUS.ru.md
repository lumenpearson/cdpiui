# Состояние форка по AUDIT-2.md

Дата сверки: 8 ноября 2026 года. Это рабочий журнал продолжения аудита, а не заключение о безопасности релиза.

## Источники и границы проверки

- Исходный аудит: `F:/Telegram/AUDIT-2.md`, SHA-256 `EE7516160403ECE00373A19D9327E0567F5F37457CD1DB92A872750C8E370194`. Файл не изменялся.
- Аудит описывает upstream `Storik4pro/cdpiui` на `f6f59c6`. После fetch upstream/main по-прежнему указывает на `f6f59c6bf5d4bb15a8ce42f8b94c8a0aa7658436`.
- Опубликованный fork main: `d72ad478bc05c6ab9e53f46da1a2b6c58690b202`, на 345 коммитов впереди upstream, без отставания.
- Продолжение: `audit-defects`, исходный HEAD этой сверки `9c2b9bdfe61ab95cf407cec239b4047a2f54b502`, ещё 17 коммитов поверх fork main, открытый draft [PR #10](https://github.com/lumenpearson/cdpiui/pull/10).
- Локальный checkout `cdpiui` на `57c5558` отстаёт от origin/main на 139 коммитов. Это не потерянные изменения и не состояние опубликованного форка. Он не переключался и не сбрасывался.
- История origin/main содержит merge PR #3 (security-hardening), #4 (maintenance-pass), #5 (refactoring), #6/#8 (mvvm), #9 (security-final). Их работу нельзя повторно объявлять неначатой.
- Прочитаны аудит, история изменений, текущие реализации ключевых защит, тесты, README модулей, handover `session-recovery-20261101/SESSION_CONTINUATION.md` и последние содержательные сообщения Claude в `59b837bf-6f85-42af-acff-1223e0f1f115.jsonl`.

Пометки **✔ в исходном аудите означают проверку достоверности находки автором**, а не исправление. Статус ниже выводится из кода и истории. «Реализовано» означает наличие исправления, не повторный живой прогон каждого пользовательского сценария. Пункты с несколькими независимыми подпунктами не считаются полностью закрытыми по одному тесту.

## Откуда продолжена работа

Последний содержательный ответ Claude: проверка профильных переменных .NET через Планировщик завершена; через launcher runtime-переменные не прошли, контрольная прошла. Запись реального hosts через launcher и ручное подтверждение UAC тогда отдельно не проверялись. Это исторический результат Claude, здесь профиль пользователя повторно не изменялся.

После Claude в PR #10 уже добавлены исправления pipe, ACL через handle, ограничения XML задач, SHA-256 staging MSI, проверки portable/MSI доверия, embedded SYSTEM cleanup и reparse-проверки. Последняя задача — smoke-запуск опубликованных инструментов.

Старый handover «функции и стиль не начаты» описывает последующие этапы повторного аудита. В качестве описания всей истории форка он неточен: декомпозиция, форматирование и MVVM уже присутствуют в main.

Отдельная запрошенная проверка PR #10 в чате `01a1a7da-185d-73e1-9edf-5de49e4da937` завершилась ошибкой лимита до выполнения ревью. Успешного независимого ревью из этого чата нет.

## Безопасность: разделы 2–3 и 20

| Пункт | Состояние | Основание и остаток |
|---|---|---|
| C1: Everyone pipe, статический GUID, произвольный elevated start | Базовые исправления в main; архитектурный план частично | `PipeAccessPolicy`, случайный pipe/session key, проверка peer, `ExecutableTrust`, `ExecutableLease`, проверки аргументов. Коммиты `fe36066`, `4b77021`, `688759c`. Полностью типизированный протокол только с ID не введён; файловые пути ещё передаются и проверяются. |
| C2: произвольный Everyone FullControl | Ограниченная замена реализована, усилена в PR10 | Команда не удалена целиком: путь ограничен, ACL применяется через проверенный handle в `DirectoryAclGuard`, GUI сам создаёт каталог. `c8ab7af`; Windows-регрессии в `DirectoryAclGuardTests`. Не называть это буквальным выполнением требования удалить команду. |
| C3: произвольный MSI/EXE и TOCTOU пакета | Реализованы проверки, усилены в PR10 | Проверка подписи и staging в tray (`0a93c11`), ожидаемый SHA-256 staging-копии (`13e1860`). Код находится в `PackageStaging`/`MsiInstallerHelper`. |
| H1: компоненты в пользовательском каталоге | Частично; остаётся архитектурный долг | Вместо полного переноса введены fingerprint, approval и leases на кодовые файлы. Компоненты всё ещё могут находиться в LocalAppData. Обязательное admin-only хранилище `ProgramData/Components` и установка только повышенной стороной не реализованы в форме плана. |
| H2: Highest autorun и доступная на запись установка | Реализованы дополнительные ограничения в PR10; зависит от H1 | Проверка владельцев/DACL/всей цепочки MSI и portable-каталогов: `f06655d`, `3f16742`, `3ec5bb2`. Небезопасный контекст отклоняется. Это не завершает перенос хранилища компонентов. |
| H3: захват pipe / impersonation | Реализовано в main | Случайное имя, FirstPipeInstance, повтор создания, peer validation; `b922dde`, `688759c`. Новые тесты реального pipe и отмены добавлены в PR10 (`a2946b3`). |
| M1: elevated shell для логов | Изменения реализованы в main | Desktop-user запуск и абсолютные системные пути; `RunHelper`, `e49d163`. |
| M2: ReloadTasks создаёт произвольную папку | Исправлено в PR10 | Повышенный tray не создаёт папку задач; GUI создаёт её сам (`13e1860`). |
| M3: XML/settings и задачи | Реализованы ограничения; продолжается проверка файловых операций | Запрет DTD/лимиты в чтении настроек и задач; `8c58c96`, `13e1860`. |
| GUID в логах, debug default | Исправления реализованы | Session key заменяет старый секрет; настройки уровня лога берутся из шаблона (`0acfe15`). |
| S1: ZIP traversal и запись до проверки подписи | Реализовано, продолжено в PR10 | `ArchivePaths`, staging/catalog verification; `d206830`, `4668ee1`, `e39e97b`, `dfdb479`, `6782559`; текущая работа дополнительно проверяет предков extraction root. |
| S2: traversal Update.exe | Реализовано | `8bf30e7`; Update компилирует общий `ArchivePaths` как linked source и применяет общие архивные лимиты (`5994042`). |
| S3: извлечение сертификата вместо проверки подписи | Замена реализована | `CatalogVerifier`/WinVerifyTrust, `7521f30`, `CatalogVerifierTests`. Исходный PoC обхода старой реализации не следует выдавать за выполненный. |
| S4: неподписанные пакеты / LScript | Реализованы запреты | `bd0f647`, `30a477e`, проверки импортируемых аргументов и путей. Дополнительные trust gaps закрывались в `4cd0e6b`. |
| S5: сквозное доверие к Магазину | Не завершено | HTTPS и опциональный SHA-256 есть (`b385a4f`, `72a4df1`, `DownloadHashTests`). `VersionControlData` всё ещё загружает zipball/main; обязательного подписанного индекса и обязательного хеша каждого артефакта нет. |
| Токен репозитория уходит на Store proxy | Исправлено в main | `674df7e`, `StoreRequestCredentialsTests`. |
| StoreId / DOWNLOAD filename / temp file | Исправления реализованы | Валидаторы ID/имён, HTTPS, уникальные временные имена; `b385a4f`, `DownloadFileNameTests`, `dc085c5`. |
| requirements.json / MSI update trust | Изменения реализованы, включены в повторный аудит | Catalog/package validation выполняется перед запуском в tray; `0a93c11`, `4cd0e6b`, `13e1860`. |
| Раздел 20: PAT в публичной истории | Только частично закрывается кодом | Gitleaks в CI есть (`735e307`, `7c198ac`). Отзыв старого токена и проверку его использования нельзя подтвердить из git. История не переписывалась; само наличие секрета в старой истории не устранено. Не публиковать значение и не проверять его работоспособность. |

## Сводный план раздела 16

| Пункт | Состояние и свидетельства |
|---|---|
| 0.1 public URI / ShowWindow allowlist | Реализовано: `ab56f0b`, обработчики активации; сырые внешние pipe-сообщения отклоняются. |
| 0.2 аргументы импортируемых пресетов | Реализованы показ и проверки: `91d699e`, `FileWritingOptionsTests`, `ConfigShareWriteOptionsTests`. |
| 0.3 DOWNLOAD filename | Реализовано: `DownloadFileNameTests`, безопасная комбинация путей. |
| 1.1 таймаут pipe / сохранение аргументов | Реализованы повторы и передача аргументов: `1fb1abf`; отмена, обрыв и объяснение смерти tray усилены в `a2946b3`. |
| 1.2 размеры свёрнутого окна | Реализовано: `907ef2a`. |
| 1.3 дубликаты задач / меню | Реализовано: `67abc7f`, `b5cbbf5`, последующая сериализация команд `e801d34`, `136e070`; `TasksManageServiceTests`. |
| 1.4 GoodCheck через БД / config.ini | Реализовано: `3f00d61`, `2f7ae6d`. |
| 1.5 apply_config | Реализовано путём прекращения генерации неизвестной команды: `f31bb22`. |
| 1.6 версия установленного пакета | Реализовано: `9786dde`. |
| 1.7 остановка перед обновлением | Реализовано: `340b6b6`; живые сценарии драйвера требуют отдельного системного прогона. |
| 1.8 готовность локализатора | Реализовано: `e901e71`; критическое окно дополнительно проверялось в предыдущей сессии PR10. |
| 1.9 quiet / конфликты / MSI1603 / errors / useProxy | Исправления реализованы: `cf96261`, `9c4c1db`, `d6549e9`, `a6ababa`, `7edc97c`; соответствующие unit tests. |
| 2.1 proxy snapshot/restore | Реализовано: `40c4bad`, `fd625e0`, `RegeditHelper.RestoreLeftoverSnapshot`. Живой crash/restart сценарий не повторён при этой сверке. |
| 2.2 Job object / shutdown | Реализовано: `6815d95`, `ChildProcessJob`, завершение при закрытии задания. |
| 2.3 uninstall cleanup | Реализовано в main и усилено в PR10: `3fe6ee5`, `5d31bdd`, `bd5c999`, `c63a5b3`, `1ba5cf7`. Последнее исправляет порядок до RemoveRegistryValues. Полный install/uninstall на отдельной VM остаётся проверкой релиза. |
| 2.4 Update staging/rollback | Реализовано: `0578bb6`, `779e10e`; `UpdateInstallHelperTests`. |
| 2.5 Store staging / чужие компоненты | Реализованы изменения Store installation: сохранение старой версии при ошибке, изоляция неудачной первой установки (`94a7a6f`, `StoreHelper.Installation`). Не считать все виды отмены/зависимостей проверенными живым запуском. |
| 2.6 hosts encoding/atomic/recover | Реализовано: `d13acdf`, `63e7a09`; `EditHostFileTests`. Реальный системный hosts текущие smoke-тесты не меняют. |
| 2.7 безопасная замена ссылок | Исправления присутствуют в `FileSystemLinksManager`; отдельная повторная проверка cross-volume/отказа прав остаётся в backlog. |
| 3.1 Zapret conversion/cache | Реализованы исправления и golden tests: `74586cf`, `Zapret1ToZapret2ConverterTests`. Неподтверждённые upstream-семантики wf-tcp/badseq/compat-Lua требуют сверки с winws отдельно. |
| 3.2 пути и кавычки | Реализовано: `ReplaceFilesPathTests`, `ConfigCommandLineTests`, `66faab6`. |
| 3.3 BlockCheck DNS/заглушки/ошибки запуска | Изменения и tests присутствуют: `CurlBlockCheckProbeRunnerTests`, службы scan/execution. Требуется живой сетевой сценарий с заблокированными целями, а не только моки. |
| 3.4 ранжирование и GoodCheck log | Исправления реализованы: `ec240b0`, изменения GoodCheck parser/log monitor и `BlockCheckReportTests`. |
| 3.5 ConPTY Unicode/error/handles | Реализованы изменения и декомпозиция: `d20f2bc`, `7edc97c`, `ConPTYNativeMethods`/reader/watcher. |
| 3.6 второй tray / pipe session | Реализовано: mutex, ожидание живого tray, случайный pipe/session file (`0a45b6d`, `688759c`). |
| 4: инженерные практики | Значительная часть реализована; см. таблицу ниже. Общее завершение этапа не подтверждено. |

## Остальные находки разделов 4–6, 9–14, 21–24

| Группа исходного аудита | Реализованные изменения / остаток |
|---|---|
| 4: кодек pipe, settings, семафоры, async MSI | `04da03c`, `d71de58`, `fd0b1ea`, `9f42050`, `30a477e`; тесты PipeMessage, StreamString, XmlSettings, TasksManage. Изменённый persistence код повторно проверяется как часть файлового аудита. |
| 5: global exceptions, оставшийся winws, GoodCheck subscriptions | `e0e7071`, `1ca25c9`, `798bb3b`, `63450ce`, `cd397f2`, `d6d0cbe`. Это уже выполненная работа, а не будущий план с нуля. |
| 5: локализация/доступность | Локализованы окна/диалоги, добавлены accessible names: `1c7817c`, `564052d`, `9e5e484`, `ca4ea43`, `1589bfb`. Числа 76 строк и 27 AutomationProperties из старого аудита больше не являются текущими метриками. |
| 9.1–9.3 URI, второй GUI, geometry | `ab56f0b`, `5dc48d3`, `907ef2a`. |
| 9.4–9.7 activation args, localization, preset ranking | `1fb1abf`, `e901e71`, `ec240b0`; полный перечень мелких UI-сценариев ещё требует отдельного regression pass. |
| 10.1–10.12 Store/version/transaction/queues/download/MSI/links | Реализации переработаны в StoreHelper partials, StoreUpdatesChecker, DownloadWorker, MsiInstallerQueueManager; `9786dde`, `7636d4c`, `94a7a6f`, `651075d`. Не объединять все 12 находок в один статус «полностью проверено»: таймаут тела HTTP, отмена MSI и cross-volume links остаются отдельными проверками. |
| 11.1–11.11 config/converter/LScript/import/export | Значительная часть реализована, покрыта тестами ProcessService, ConfigItemContent, ReplaceFilesPath, ConfigImportGolden, LScript, converter. `66faab6`, `9fdfa54`, `032185b`; семантики winws, помеченные неподтверждёнными, не закрыты одним успешным build. |
| 12: proxy, tray menu, job objects, ConPTY, MSI status | Реализованы перечисленные выше изменения; `d317778`, `ac5735f`, `d20f2bc`, `ffc88e1`. Повторный полный живой проход tray не завершён. |
| 13.1–13.11 Shared/Update/hosts/MSI/errors/tasks | Реализованы указанные выше исправления, `376ed7f`, `dc085c5`, `5bafb69`. Ограничения задач и доверие к uninstall дополнены PR10. |
| 14.1–14.12 AddOns | Исправления diagnostics/GoodCheck/import/BlockCheck присутствуют; `9c4c1db`, `3f00d61`, `cbb9f0c`, `1c7cc56`, `b681cc2`, `944743b`. Сетевые/системные сценарии и весь перечень мелких подпунктов ещё не перепроверены по одному. |
| 21.1 empty ProxyServer | `08119cb`, `ProxySettingsTests`. |
| 21.2 закрытие несохранённого пресета | `4056575`. |
| 21.3 формирование patch до перемещения UI | Исправления ApplicationUpdateService, `c773687`, `UpdateInstallHelperTests`; исходный порядок больше не следует считать актуальным. |
| 21.4 dependencies == null | `76ddd0c`. |
| 21.5 make_patch parser | Скрипт переработан, используется PowerShell 7: `691e81e`, `824bb1f`. Старое неподтверждённое предположение не равно текущей ошибке. |
| 21.6 Update.exe отсутствует в MSI | Исправлен release layout; CI main/PR10 проверяет список файлов MSI и собирает пакет. |
| 22.1 tray controls/environment/arguments/ConPTY | `d317778`, `ac5735f`, `b5cbbf5`, `5bafb69`, `d20f2bc`, `651075d`. |
| 22.2 Store exceptions/HTTP/DBNull/locales | `7636d4c`, `a9d092e`, `2d9d4f9`, `0864c1f`. |
| 22.3 Shared output/key parsing/random | `ebc5f50`, `456f3f3`, переработка XMLSettingsService. |
| 22.4 AddOns BAT/atomic synthesis/report limits | `e691a4a`, `b681cc2`, tests ConfigImport/BlockCheckReport. |
| 22.5 GUI pages | `e9af308`, `8aa911b`, `6dd1d5a`, `9acd4fc`, последующие MVVM fixes `077691f`/`8f10696`. |
| 22.6 GUI controls/helpers | `66e7d09`, `d531db4`, `0b200f5`, `10dae1f`; часть состояния затем перенесена в view models. |
| 22.7 Update before handle / hosts newline | `9e47d12`, `EditHostFileTests`. |
| 23: низкоприоритетные находки | Есть отдельные исправления групп: `3de9f91` (tray/core), `bb5362d` (AddOns), `c053223` (BlockCheck), `86a9f80` (GUI), `824bb1f` (installer/patch). Это доказательство выполненной работы; новый построчный контроль каждого подпункта не завершён. |
| 17–19, 24–25, 27–28, 32, 37 | История, уточнения, метрики, требования сохранения функций и сравнительный контекст. Не являются одним закрываемым дефектом. Сохраняются как требования к изменениям; старые метрики не переносить на новую ревизию без пересчёта. |

## Раздел 29: новые функции

| Пункт | Текущее состояние |
|---|---|
| 29.1 Starting → Ready / Failed, health profiles | Не реализован полный поток: `ComponentStateReader` использует IsProcessRunning; нет ComponentHealthProfile, ProcessReady/ProcessFailed и readyTimeoutSec. |
| 29.2 подписки на списки с provider и отдельными custom-файлами | Нет полного ListSubscriptionService/UpdateLists и соответствующего UI. Имеющийся Store subscription — не выполнение этой функции. |
| 29.3 ConditionalLaunch v2 | Не реализован: enum содержит HotKey, ProcessStarted, ProcessStopped. Нет ProcessRunning/NetworkChanged/Schedule/SessionUnlocked/ResumeFromSleep и владения компонентом. |
| 29.4 Google Global Cache resolver | Реализация GoogleVideoNodeResolver и общий поток выбора ближайшего узла не найдены. |
| 29.5 Android .bbd | Адаптер ByeDpiAndroidFormat и полный импорт/экспорт .bbd не найдены. |
| 29.6 перезагрузка после MSI | Частично: коды 3010/1641 и CoreEvents.RestartRequired/IsRestartPending существуют. Не подтверждено полное соответствие всем предложенным UI-точкам и подтверждённому действию shutdown. |
| 29.7 migration runner / post-update wizard | Нет SettingsMigrationRunner/ISettingsMigration и полного трёхшагового потока. Defaults из шаблона и существующее окно «Что нового» не заменяют миграции схемы. |
| 29.8 cache/channel/localized release notes | Полный набор не реализован/не подтверждён. Не найдены lastUpdateCheck и предлагаемый поток каналов; требуется отдельная реализация с тестами. |
| 29.9 animations system/on/off / low performance | Полный предложенный глобальный режим и предложение для слабого ПК не подтверждены; наличие анимированных контролов не закрывает пункт. |
| 29.10 StartupTrace / diagnostic ZIP | Полный StartupTrace и безопасный обезличенный экспорт не найдены. Существующие логи/diagnostics не заменяют этот поток. |

## Разделы 6, 30–36: инженерные практики и стиль

| Требование | Состояние |
|---|---|
| CI build/test/secrets/portable/MSI | Реализовано. Последний исходный run `41883768621`: libraries/gui/secrets success, tools failure. Ниже описана обнаруженная гонка и её исправление. |
| xUnit и regression assets | Реализовано. При этой сверке полный suite: 749 passed, 0 failed, 0 skipped. Это число тестов, не процент покрытия строк. |
| Update/EditHostFile .NET8 x64 | Реализовано: `e54c449`; Native AOT launcher — `4ce988b`. |
| CPM/global.json/package mapping | Реализовано: Directory.Packages.props, global.json, nuget.config (`809ce28`, `8c58c96`). Directory.Build.props отсутствует. |
| Nullable / warnings as errors | Частично: CS8600 всё ещё none, CS8602 suggestion в .editorconfig; общего TreatWarningsAsErrors нет. AOT launcher проверяется строго. |
| Зависимости / лицензии | Удалены уязвимые и неиспользуемые зависимости, добавлены THIRD-PARTY-NOTICES (`19a12af`, `26c90ba`). Microsoft.Toolkit.Uwp.Notifications 7.1.3 ещё используется; переход на AppNotifications не завершён. |
| WiX file list | Путь и список исправлены, CI сверяет с publish. Это контроль списка, а не полная автоматическая генерация WiX. |
| 33: именование | `3c1558c`, `af0a0ea`; совместимость сериализованных имён требует сохранения. Не переименовывать ключи Store вслепую. |
| 34 / 36.0–2: .editorconfig/формат/декомпозиция | Значительная работа уже выполнена: `a0efd14`, `af0a0ea`, PR #5, множество split-коммитов. Повторный style pass и строгость анализаторов не закончены. |
| 35 / 36.5: MVVM | Уже есть ViewModels, ObservableObject и commands, PR #6/#8. Это не полный переход всего GUI на CommunityToolkit.Mvvm. |
| 35 / 36.5: DI/Hosting/ILogger/Options/STJ/CsWin32 | Общий переход не завершён: сохраняются синглтоны, собственный logger, Newtonsoft.Json и ручной interop. Это будущие миграции, не основание переписывать всё вместе с security fixes. |
| 30 A–C | Большая часть точечных A/B исправлений реализована; доверие к Store/хранилищу компонентов и protocol v2 в C не завершены. |
| 30 D | Новые функции из 29 в основном остаются backlog. Не выдавать старые аналогичные функции за реализацию нового дизайна. |
| 30 E / 31 | Частично: tests, CI, notices, localization, MVVM есть; подпись релизов, nullable baseline, обновлённые метрики доступности и соблюдение всей дизайн-системы требуют отдельной проверки. |

## Изменения этого продолжения и проверки

1. В логе tools job `126762659685` видно: Get-Content сообщает об отсутствующем отчёте в 18:08:27 UTC, а три PASS приходят в 18:08:30–31. PowerShell не дождался GUI executable. Workflow теперь использует Start-Process -Wait -PassThru, читает отчёт после завершения и проверяет ExitCode именно этого процесса.
2. В ArchivePaths проверялись root и его потомки, но не родители root. Регрессионные тесты LinkedAncestorOfExtractionRootIsRejected и DanglingLinkedAncestorOfMissingExtractionRootIsRejected падали на исходной версии: исключение не выбрасывалось. Теперь проверяется вся цепочка до корня тома, а назначение вне extraction root отклоняется.
3. Полный suite после исправления: 749/749, без пропусков. Release publish Update.exe прошёл. Проверка исправленного CI-ожидания в повышенном PowerShell завершилась с кодом 0; runner и все три инструмента дали PASS, отчёт создан после времени начала запуска.
4. Path-based reparse checks не доказывают отсутствие всех гонок между проверкой и записью. Проверка атомарности/handle-based операций в остальных файловых потоках остаётся открытой.

## Очередь продолжения

1. CI154 на `e69269d` завершён: libraries, tools, secrets, gui — success. Держать PR черновым до завершения проверки дефектов.
2. Продолжить файловый аудит: staging/promotion, FileSystemLinksManager, settings/backup, cleanup и гонки замены каталогов. Проверять по конкретным операциям и регрессиям, не отмечать весь раздел закрытым после одного fix.
3. Отдельно воспроизвести cancellation/timeout Store/MSI, rollback при заблокированной DLL, полный install/uninstall на изолированной Windows VM.
4. Завершить H1/S5: определить модель защищённого хранилища и подписанного Store index. Не превращать опциональный хеш в обязательный без согласованного формата каталога.
5. Подтвердить отзыв исторических credentials у владельца; кодом это не удостоверить.
6. После стабилизации дефектов — функции раздела 29, затем оставшиеся engineering/style migrations отдельными изменениями с совместимостью форматов.

Чужие рабочие деревья и исходный аудит не перезаписывались. Все изменения продолжения выполняются в cdpiui-s2/audit-defects.

## Файловый проход 2 — staging, links, settings, backup, cleanup

Продолжение от `e69269d`, 8 ноября 2026 года. Это дополнительные исправления, а не закрытие всего файлового аудита.

### Подтверждённые дефекты и исправления

| ID | Область | Результат |
|---|---|---|
| FS-01 | `Update/InstallHelper.ApplyStaged` | До исправления symlink/junction в папке назначения, staging или backup позволял переносить файлы вне ожидаемого дерева. Три теста воспроизвели успешный небезопасный apply. Теперь весь план проверяется до первого переноса, пути проверяются повторно после progress callback и создания папок; rollback проверяет собственные пути. |
| FS-02 | Update backup/cleanup | Новый StageAsync безусловно удалял непустую `.update-backup`, оставленную предыдущим обновлением. Тест воспроизвёл удаление единственного оригинала. Теперь операция останавливается со статусом NoOldVersion, backup сохраняется для ручного восстановления. Cleanup также отклоняет reparse points в пути удаляемой папки. |
| FS-03 | `ZipService.MergeTextFileAsync` | Выбор `.bak` происходил после проверки исходного пути. Тест с `.bak`-symlink воспроизвёл запись во внешний файл. Проверяется именно выбранный backup, до чтения и перед append. Проверка hard links и конкурентной подмены после проверки остаётся открытой. |
| FS-04 | `SettingsFileStore` / `XMLSettingsService.SaveChange` | При UnauthorizedAccessException открытие lock возвращало null, но признак timeout оставался false: SetValue сохранял файл без lock. Тест использует каталог на месте `.lock` и воспроизвёл запись. Теперь единственное основание для записи — ненулевой объект полученной блокировки; без него изменение остаётся в памяти. |
| FS-05 | Замена soft/hard link | Старый файл удалялся/переносился в backup до финального Move без восстановления при ошибке. `LinkFileTransaction` сохраняет вытесненный файл и возвращает его при ошибке публикации. Старый `.bak` больше не перезаписывается. Ошибка cleanup временного файла не маскирует ошибку операции. |
| FS-06 | Восстановление original вместо link | Раньше RemoveLink сначала удалял рабочую ссылку, затем пытался переместить backup: sharing violation оставлял путь пустым. Теперь ссылка временно сохраняется и возвращается, если backup нельзя перенести. При ошибке самого отката сообщение содержит пути сохранённых файлов. Отсутствующий backup не удаляет работающую ссылку; dangling link без original удаляется. |

### Проверки

- Шесть тестов FS-01–04 запускались на прежнем коде: 6 failed / 0 passed; внешние записи, обход lock и удаление backup подтверждены.
- Добавлено 19 регрессий: 1 settings, 1 ZIP backup, 6 updater, 11 link transaction. Windows sharing violations моделируются настоящими открытыми файлами без разрешения rename/delete, без изменения пользовательских настроек или системных файлов. Отдельно проверен полный цикл публикации настоящей symbolic link и восстановления оригинала.
- Два updater-сценария моделируют подмену каталогов через существующие события прогресса: после уведомления до apply и после первого файла перед rollback. Внешний файл сохраняется; при небезопасном rollback оригинал остаётся в backup.
- Полный набор после изменений: 768 passed, 0 failed, 0 skipped. Release publish Update.exe и smoke-запуск трёх инструментов прошли; итог CI нового коммита фиксируется в SESSION_CONTINUATION.md.
- Изменения поведения documented в README Update/Core/Shared на EN/RU.

### Оставшиеся окна гонок и следующие проверки

| ID | Статус | Конкретный остаток |
|---|---|---|
| FS-07 | Реализована защита staging; проверка полного MSI/patch потока остаётся | См. проход 3: отдельный admin-owned cache, атомарные ACL, no-follow source lease и leases потребителей. Защита пакета больше не зависит от writable portable root или жизни tray. Очистка orphan sessions остаётся FS-12. |
| FS-08 | Защита исходного снимка реализована; назначение и rollback открыты | Signed ZIP удерживает фиксированный набор обычных файлов и каталогов до конца копирования; поздние добавления не устанавливаются. Проверки назначения относятся к FS-09, атомарность нескольких файлов — к FS-12. См. проход 4. |
| FS-09 | ZIP и Windows updater apply/rollback исправлены; прочие операции открыты | No-follow write/append и guarded creation дополнены hash при extraction, readonly staging snapshot и native file publication/rollback updater. Settings, links, runtime write-target и recursive cleanup ещё требуют следующих проходов; см. проходы 5–7. |
| FS-10 | Windows settings I/O исправлен; crash/recovery policy см. FS-11 | Initial/recovery и SaveChange используют lock; settings/backup/lock/diagnostic/notice открываются no-follow и проверяются по handles. Atomic publication удерживает родителей, DACL копируется до записи данных. Lock сам по себе не ограничивает процесс, намеренно нарушающий протокол; защиту записи дают handles. См. проход8. |
| FS-11 | Открыт, durability | LinkFileTransaction откатывает исключения, но не обеспечивает crash consistency между файловой системой и Settings.xml. При power loss нужен журнал восстановления. Новый updater сохраняет старый backup, но не сериализует два одновременно запущенных Update.exe. |
| FS-12 | Открыт, cleanup | PackageStaging.Release/CleanUp и рекурсивные удаления Store/verify нуждаются в проверке владения деревом и гонок замены родителей. Не добавлять delete с повторной проверкой пути под видом атомарной защиты. `StoreItemFolders.ReplaceItemFolder` откатывает неудачный Move; online update всё ещё может смешивать версии, как указано в Core README. |

Следующий security-приоритет: FS-07–09 — проектирование и тестирование удерживаемых handles для staging/проверки/promotion/записи. FS-10–12 и живые cancellation/crash/uninstall сценарии остаются в очереди. Этот проход сохраняет обратную совместимость форматов; системный hosts, proxy и установленные компоненты не изменялись.

## Файловый проход 3 — защищённый staging и Windows readonly leases

Продолжение от `f39e8a0`, 8 ноября 2026 года. В соответствии с прямым запросом пользователя работа согласована с чатом «Декомпозировать большие классы» (`01a1bc96-ea11-73a1-a4a1-5cd6db1617fa`). Там закреплён ConfigMaker GUI в отдельном worktree, здесь — security/staging/Update. Общий тестовый csproj объединяется аддитивно. GUI/ConfigMaker этой сессией не изменяются.

### Реализация FS-07

- `ReadOnlyPathLease` открывает существующий файл и всю цепочку родителей от корня тома через Windows handles. Для каждого компонента используется OPEN_REPARSE_POINT; каталог должен быть обычным, файл должен иметь ровно одно имя. Read sharing допускает читателей и запрещает запись, удаление и rename до Dispose. FILE_LIST_DIRECTORY применяется вместо attribute-only handle, который не обеспечивает нужные sharing restrictions.
- `ProtectedStagingArea` копирует источник через такой lease в `<Common Files>/CDPIUI-Staging/<GUID>/`. Источник не может быть подменён или изменён во время копирования. Размер пакета ограничен 1 ГиБ; ADS-имена отклоняются.
- Владелец Administrators и защищённая DACL Administrators/SYSTEM задаются при создании каталогов и файла, без промежутка с пользовательским владельцем. Небезопасные существующие пути не исправляются и не принимаются. Проверка предков разрешает создание соседнего каталога, но запрещает изменение/удаление/замену защищённого потомка. Null DACL, пользовательский owner, применимые GENERIC_WRITE/GENERIC_ALL и опасные права отклоняются; InheritOnly ACE не применяется к текущему объекту.
- Копия остаётся защищённой от непривилегированного пользователя после выхода/падения tray. Store MSI держит дополнительный lease от проверки hash/signature до завершения процесса. ZIP-updater получает собственный lease до остановки tray, чтобы запуск потребителя не оставлял окно передачи защиты.
- Каталоги и файлы временной распаковки для проверки подписанного patch также создаются с защищёнными ACL/owner. Общая bounded-copy функция применяется к открытому защищённому output stream.
- Release принимает только пакет в непосредственной GUID-сессии защищённого корня и проверяет дерево. Startup больше не удаляет пакеты вслепую: завершение tray не доказывает завершение MSI. Незавершённые сессии могут оставаться на диске; протокол безопасной сборки и политика квот/возраста относятся к FS-12.

### Проверки и ограничения

- Тесты Windows проверяют блокировку записи, удаления и rename файла/родителя, разрешение второго читателя, передачу lease потребителю, отказ при уже открытом writer, symbolic/dangling/hard links и освобождение handles на ошибках.
- ACL-тесты проверяют отказ от пользовательского owner, null DACL, опасных прав предка, отказ от принятия/исправления пользовательского каталога и границы Release. Отдельно различаются право создавать соседей и право изменять существующий staging.
- Elevated ToolsSmokeTest создаёт уникальный `CDPIUI-Staging.Smoke-<GUID>` в Common Files, проверяет копирование, owner/DACL всего дерева, чтение через lease и удаляет только собственную сессию и тестовый корень. Системные hosts/proxy и реальные MSI не запускаются.
- Первый sandbox-прогон lease не смог открыть handle профиля; вне sandbox проверки прошли. Это ограничение среды, не повод ослаблять sharing. Restore tray потребовал сетевого доступа к настроенным NuGet feeds; аудит пакетов не отключался.
- Smoke выявил, что существующая MSI install-location policy отклоняет стандартный C:\\ из-за CreateDirectories у Authenticated Users. Для staging введена отдельная политика; `ProtectedInstallLocationPolicy` оставлена без изменений. Это отдельная находка для воспроизведения на установленном MSI, а не заявленное исправление MSI launcher.
- FS-08 (неизменность всего signed-ZIP набора между VerifyCatalog и promotion), FS-09 (no-follow записи/append, hard links и родители назначения) пока открыты. Новый readonly primitive — основа для этих шагов, а не доказательство их завершения.
- Полный end-to-end MSI upgrade, tray crash во время установки и patch/update с живыми компонентами требуют изолированной Windows VM. Остатки кэша не удалять вручную при неизвестном активном потребителе.
- Окончательный локальный результат: 790 passed, 0 failed, 0 skipped (22 новых Windows-регрессии сверх прохода 2); Release x64 tray — 0 warnings/errors; publish Update/ToolsSmokeTest успешен. Elevated smoke: защищённая копия/owner/DACL/read lease/cleanup и все три инструмента — PASS, exit 0. Каталоги проверки ограничены 64 уровнями; traversal/избыточная глубина отвергаются до создания дерева. Итог CI этого checkpoint фиксируется в SESSION_CONTINUATION.md.
- Основание для Windows API: [CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew) (no-follow и sharing) и [FileSystemAclExtensions.Create](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemaclextensions.create?view=net-8.0) (security descriptor при создании).

Следующая точка: проверить завершённый CI прохода 3, затем продолжить FS-08 и FS-09 в том же порядке; отдельно сохранить новую находку о MSI ACL policy и FS-12 cleanup/quota. Согласование пересечений с сессией декомпозиции продолжается перед изменением общего модуля.

## Файловый проход 4 — фиксированный снимок signed ZIP (FS-08)

8 ноября 2026 года, продолжение от `4b5a0ba`. CI этого базового коммита завершён: tools, secrets, libraries и gui — success, [run 42516916898](https://github.com/lumenpearson/cdpiui/actions/runs/42516916898).

- `ReadOnlyDirectorySnapshot` захватывает обычные файлы и каталоги до проверки каталога и удерживает их до конца promotion. Рекурсивная обработка начинается только после no-follow открытия каждого дочернего каталога; symbolic/junction/hard links отвергаются. Ограничения: 20000 элементов, 1 ГиБ данных и 64 уровня.
- `ZipService` больше не перечисляет исходное дерево после проверки и не переоткрывает исходные файлы по имени. Копируется фиксированный список через удерживаемые streams; поздно добавленные файлы и каталоги не устанавливаются. Ошибка проверки не меняет назначение и освобождает handles.
- Дочерние leases разделяют время жизни удерживаемого родителя, без повторного открытия всей цепочки для каждого файла. Dispose родителя не освобождает защиту, пока жив дочерний lease; ошибка открытия освобождает ссылку.
- Обнаружен и воспроизведён побочный эффект исходного FS-07 primitive: deny-write всех предков блокировал rename посторонних соседних каталогов. После захвата цепочки предки переходят на read/write sharing без delete sharing; каждый удерживаемый потомок не даёт предку стать пустым, а NTFS не разрешает установить reparse point на непустой каталог. Строгая блокировка сохраняется на конечном каталоге. Временная строгая фаза захвата цепочки остаётся короткой; Windows filesystem тесты этих primitives выделены в непараллельную collection.
- Отдельные настоящие Windows-тесты подтверждают: соседний каталог можно переименовать во время независимого lease; открытие writable handle предка допустимо, но FSCTL_SET_REPARSE_POINT возвращает ERROR_DIR_NOT_EMPTY; удерживаемый дочерний каталог нельзя перенести. Основание: [FSCTL_SET_REPARSE_POINT](https://learn.microsoft.com/en-us/windows-hardware/drivers/ifs/fsctl-set-reparse-point).
- Регрессии подмены файла и позднего добавления воспроизведены на прежнем promotion (2 FAIL), затем прошли с фиксированным снимком. Полный локальный suite: 801 passed, 0 failed, 0 skipped.
- Ruling: destination пока сохраняет прежнюю модель проверок пути и копирования. Это намеренно отдельный шаг FS-09; текущая защита источника не заявляется защитой назначения или crash-consistent транзакцией. Неудачное копирование нескольких файлов ещё может оставить смешанную версию (FS-12).

Следующая точка: FS-09 — no-follow открытие назначения без предварительного усечения, проверка типа и числа hard links по открытому handle, удержание родителей при создании и записи. Затем settings/backup/cleanup и сериализация updater.

## Файловый проход 5 — Windows no-follow запись и имена путей (FS-09)

8 ноября 2026 года, продолжение от `a0ff9d4`. Его [CI 42518432026](https://github.com/lumenpearson/cdpiui/actions/runs/42518432026) полностью зелёный (tools/secrets/libraries/gui).

- `WritablePathLease` открывает leaf с OPEN_REPARSE_POINT и OPEN_ALWAYS, сохраняя существующие байты до проверки. Тип объекта и единственное имя проверяются по полученному handle, затем caller может усечь/записать. FileShare.None удерживает файл, readonly directory leases удерживают родителей.
- Недостающие каталоги создаются по одному под удерживаемым обычным родителем; уже подставленный link отклоняется до перехода внутрь. Это применяется к ZIP назначениям и созданию staging/destination/backup каталогов updater.
- Windows `ExtractToFileBounded`, signed promotion и `.txt` merge используют новый write lease. Merge читает, определяет конец строки и дописывает через один handle, включая выбранный `.bak`. Для non-Windows library consumers сохранён прежний managed I/O; гарантии Windows handles к нему не приписываются.
- `WindowsPathNames` отклоняет Win32 aliases: trailing dot/space в компонентах, управляющие/недопустимые символы, DOS device names (включая COM с надстрочными цифрами). ArchivePaths проверяет сырые компоненты до нормализации; native opens также проверяют полный путь. Device-path prefixes не принимаются. Обычные `.`/`..` архивов сохраняются только при результате внутри root.
- Воспроизведены 7 отказов validation и 3 настоящие hard-link регрессии: архивный destination, `.bak` при merge, destination, подставленный во время catalog verification. После исправления все проходят, внешние байты сохраняются.
- Подтверждена отдельная ошибка `ExecutableLease`: managed long для FILETIME давал неверное выравнивание BY_HANDLE_FILE_INFORMATION и считывание числа ссылок из file index. Обычный existing write target ошибочно отклонялся как hard link (RED). DWORD-пары исправляют layout; обычный файл можно дописать, настоящий hard link отвергается (GREEN). Это не объявляется устранением всей гонки runtime write target.
- Полный локальный suite: 819 passed, 0 failed, 0 skipped. Tray Release x64 — 0 warnings/errors, Update и ToolsSmokeTest publish успешны. Текущий ToolsSmokeTest включает общий WindowsPathNames; запуск всех инструментов проверяется также CI без нового локального UAC.
- Ruling: FS-09 закрыт только для описанных открытий ZIP write/append и создания каталогов. `File.Move`/rollback/recursive cleanup updater ещё используют имена и повторные path checks; между staging и apply требуется защита извлечённых байтов. Settings initial/recovery/lock/backup, LinkFileTransaction publication и запись дочернего компонента через GuardWriteTarget ещё требуют отдельных решений. Многофайловая атомарность и crash recovery остаются FS-11/12.

Новый срок прямого запроса пользователя: до14:00 9ноября2026 Europe/Moscow =11:00UTC. Продолжать самостоятельно, сохранять журнал и согласовывать пересечения с декомпозицией. Следующая точка — оставшийся updater staging/apply и затем FS-10 settings; не считать общий файловый аудит завершённым.

Дополнение прохода5: отдельный `InstallHelper.TryGetDestination` сохранял дублированный validator и принимал Win32 aliases. Три updater-регрессии воспроизведены RED, validator делегирует ArchivePaths с сохранением null-out при отказе. Suite822/822,0skipped. Локальный elevated ToolsSmokeTest после пересборки Update завершён exit0: защищённый staging probe и Update/EditHostFile/ElevatedLauncher PASS. UAC был обнаружен, но к получению window уже исчез; никаких кликов в чужих окнах не делалось.

## Файловый проход6 — перенос и удаление конкретного открытого файла

Native foundation после ec45260: OpenDirectoryForMutation удерживает directory name без delete sharing, разрешая rename дочерних имён. Уникальный пустой child с DeleteOnClose не даёт каталогу стать пустым/превратиться в junction и убирается по своему handle при выходе или crash. CreateForMove создаёт новый ordinary file с DELETE access; OpenExistingForMove открывает существующий файл только для чтения/rename. MoveTo использует SetFileInformationByHandle(FileRenameInfo) на источнике и удерживает всю цепочку назначения. Конфликт без replacement сохраняет оба файла; replacement удаляет имя hard link, не записывая во внешний файл. DeleteOnClose помечает только удерживаемый объект, без повторного path lookup.

Ruling: выбран документированный common case RootDirectory=NULL с абсолютным destination под удерживаемой цепочкой, вместо относительного RootDirectory ABI. Сам источник переименовывается по handle, все родители назначения закреплены. Основание: https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info и https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle .

Пять новых native-регрессий RED→GREEN; полный suite до следующего этапа827/827,0skipped. Это foundation, ещё не изменение updater apply. Следующие3регрессии updater уже воспроизведены RED: staged bytes меняются доapply, во время Copying callback и уже опубликованный файл/родитель меняются до конца транзакции. Для updater нужен hash, снятый при bounded extraction, immutable snapshot с проверкой всех hash до первого изменения, native backup/publish/rollback и удержание опубликованных файлов до завершения.

## Файловый проход7 — Windows updater: проверенные байты и native транзакция

Продолжение от `98f32d9`, 8 ноября 2026 года.

- SHA256 считается в CryptoStream во время bounded extraction, не из пользовательского staging после закрытия write handle. Hash manifest хранится в памяти; более поздний слой обновляет hash. Список дедуплицируется по каноническому относительному пути.
- `UpdateFileTransaction` захватывает readonly-снимок всего staging и сверяет каждый запланированный hash до изменения первого установленного файла. Пропавший/изменённый источник или несовпадение плана прерывает apply. Snapshot source-linked в Update без зависимости от DLL из заменяемой установки.
- Оригинал открывается no-follow с READ/DELETE, проверяется и переносится в backup по handle. Проверенные байты копируются из snapshot stream в CREATE_NEW temporary file; тот публикуется по handle без overwrite конфликтующего назначения. Backup originals и новые опубликованные объекты удерживаются до завершения всех файлов и progress callbacks.
- Rollback удерживает родителя, помечает для удаления только собственный новый открытый объект и восстанавливает оригинал по handle. Небезопасный путь или конфликт не уничтожает backup. `WritablePathLease.CurrentPath` отражает успешный rename до освобождения прежнего родителя, чтобы откат видел фактическое положение объекта.
- Три воспроизведённые updater-регрессии RED→GREEN: подмена staging до apply, подмена другого staged file в Copying callback, изменение опубликованного файла/родителя до завершения. Дополнительный тест меняет readonly attribute нового файла через attribute-only операцию и вызывает rollback: при отказе удаления единственный оригинал остаётся в backup, статус NoOldVersion.
- Ruling: прежний rollback/link тест удалял второй staged file до apply, что теперь отклоняется preflight. Он изменён на реальную ошибку после первой публикации; Windows удержание родителя блокирует подмену и позволяет вернуть оригинал. Legacy non-Windows путь по-прежнему сохраняет backup при подменённом родителе. Проверяется более сильное новое поведение, защита ради старого теста не ослаблялась.
- Проверки: updater39/39; весь suite831/831,0skipped; Update Release single-file publish PASS. Windows path test collection дополнена updater и ArchiveLimits, чтобы краткий acquisition общих предков не пересекался с чужими filesystem tests.
- Остаток: recursive staging/backup cleanup, updater log append, резервирование служебных updater путей, сериализация двух Update, durable journal/crash recovery и final restart. Core signed ZIP multi-file rollback, SettingsFileStore и runtime write target тоже остаются отдельными задачами. Нельзя объявлять весь FS-09–12 закрытым.

Дополнение прохода7: updater log append теперь использует Windows write lease, отказываясь от symbolic/hard links без изменения внешних байтов. Два настоящих лог-регрессионных сценария воспроизведены RED→GREEN; обычный append сохраняется. План архива после канонизации отклоняет .update-staging, .update-backup, .update-manifest и update.log в первом компоненте; четыре reserved-path регрессии RED→GREEN. Полный suite838/838,0skipped, Update publishPASS. CI ada2a4b завершён: все4success, run42521704337. Следующая очередь — FS10SettingsFileStore и затем FS11/12; backup/staging recursive cleanup всё ещё не заявлен безопасным.

## Файловый проход8 — SettingsFileStore (FS-10)

Продолжение от8a204f3, ночь8–9ноября2026. CI базового коммита42522168072 — все4success.

- Windows Load читает XML через ReadOnlyPathLease; DTD остаётся запрещённым. Symbolic/dangling/hard links и небезопасные родители не читаются. Native missing-file/path ошибки сохраняют смысл FileNotFound/DirectoryNotFound для initial creation.
- `.lock` открывается no-follow/exclusive, проверяется обычный тип, единственное имя и нулевая длина. DeleteOnClose задаётся только после проверки. Ранее symbolic `.lock` удалял внешний target при закрытии: это воспроизведено на собственном временном файле и исправлено. Небезопасный/непустой lock не удаляется и не разрешает запись settings.
- XMLSettingsService initial creation и recovery теперь берут общий lock и перечитывают источник после ожидания. Без lock восстановление только в памяти: нет diagnostic copy, notice и записи XML. Публичный API, форматы, ValueCodec и SettingsXmlTree не менялись.
- WindowsSettingsFiles (малый отдельный backend) создаёт CREATE_NEW temporary через checked handle. Исходный XML при необходимости копируется через readonly stream в backup temporary. После закрытия источника native rename публикует backup и атомарно заменяет имя Settings.xml; окна отсутствия основного файла нет. Вся цепочка родителей удерживается. Неопубликованные temporary удаляются только через собственные handles/CurrentPath, без нового File.Delete по имени.
- Diagnostic copy читает только обычный исходник; notice пишет через проверенный write handle. SaveChange после ошибки чтения не записывает испорченный primary поверх последнего хорошего `.bak`.
- Дополнительная проверка поймала регрессию нового rename-пути до коммита: temporary наследовал DACL родителя вместо существующих access rules. Теперь DACL снимается с удерживаемого оригинала и передаётся в SECURITY_ATTRIBUTES при создании temporary/backup/diagnostic, до записи приватных байтов. Проверены protected и inherited ACL, отсутствие роста ACE после повторных saves. Сравниваются реальные rules и protection, а не косметический SDDL marker AUTO_INHERITED.
- Ruling: сохраняется DACL; owner/прочие new-file metadata не обещаны идентичными оригиналу. Settings — пользовательские данные, не источник доверия по владельцу. Код не пытается получать SeRestorePrivilege или менять владельцев существующих файлов. Нужда в точном сохранении owner/ADS/EFS требует отдельного решения; текущая гарантия — проверенные paths/bytes и сохранение access rules.
- Проверки:10 public safety regressions RED→GREEN (settings/backup/lock links, notice, parent, initial/recovery lock), испорченный primary/last-good backup RED→GREEN, validated lock и typed missing-file contracts RED→GREEN,2 ACL regressions. Полный suite852/852,0skipped,22sec; tray Release x64 0warnings/errors; Update single-file publishPASS. Нет оснований расширять native API ради speculative performance optimization: последний suite сопоставим с21sec baseline838.
- Координация: чужая декомпозиция независимо проверила именно ada2a4b+ecc7828 —969/969 и GUIx64Release0/0 в отдельной короткой копии. Более новые security/GUI коммиты этим числом не покрыты; они требуют следующего joint run.

Основание API: [GetKernelObjectSecurity](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-getkernelobjectsecurity), [SECURITY_ATTRIBUTES](https://learn.microsoft.com/en-us/windows/win32/api/wtypesbase/ns-wtypesbase-security_attributes). Следующая очередь: links/FS-11 crash consistency и updater serialization, затем FS-12 cleanup/Core multi-file promotion, runtime write target и final restart/MSI policy. Все эти пункты остаются открытыми.
