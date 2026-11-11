> Historical local-stage record for frozen dev/audit-class-decomposition at eebc7e8.
> The previous local composition is tracked in [its historical plan](superpowers/plans/2026-11-11-class-integration.md).
> Current stack-through227 integration and its original acceptance gates are tracked in [the current plan](superpowers/plans/2026-11-12-class-stack-through227-integration.md).

# Приёмочная сводка декомпозиции, 9 ноября 2026

Исторический проверенный срез первого временного окна, product e42. Позднее пользователь возобновил работу без дедлайна; результаты новых этапов и актуальный pin находятся в [текущем статусе](DECOMPOSITION-STATUS.ru.md). Числа этой сводки сохраняют исходную область проверки.

Кодовый срез: **e42ac481ab230866bd65ea06ccb7496a52a75c9c**, база: **f39e8a0a7267e4fcd4a02eef186a3ee08df8dcd9**. Ветка `dev/audit-class-decomposition`, отдельное рабочее дерево `C:/Users/lumen/.codex/worktrees/audit-class-decomposition/cdpiui`. После последней продуктовой проверки меняется только документация. История этапов находится в [отчёте](DECOMPOSITION-STATUS.ru.md), исходные границы — в [согласованном проекте](superpowers/specs/2026-11-08-class-decomposition-design.md), проверочные gates — в [плане](superpowers/plans/2026-11-08-class-decomposition.md).

Числа, pins, hashes, размеры классов, архитектурная карта и ссылки на первичный аудит сверены отдельным read-only factual review; повторные test/build/runtime executions для проверки документа не выполнялись. История действий принимается из рабочего журнала, а пределы каждого технического доказательства указаны ниже.

В **10:45 UTC / 13:45 MSK** начат этап завершения. Новые продуктовые изменения и новые refactoring stages не начинаются; код остаётся e42. Финальная передача состояния назначена к 11:00 UTC / 14:00 MSK. Два native-gate ниже не считаются выполненными по истечении срока.

## Запрос и пределы приёмки

Пользователь согласовал порядок ConfigMaker → App → BlockCheck2 → остальные крупные ViewModel и самостоятельные безопасные решения до **14:00 9 ноября 2026 MSK (11:00 UTC)**. Требование — сохранить поведение, работать параллельно с security-сессией, избегать необратимых действий. Отсутствие обнаруженных регрессий относится к выполненным проверкам; абсолютное отсутствие регрессий не доказано. Native gates 15/16 остаются открытыми.

Приложенный `F:/Telegram/AUDIT.md` использован как данные. В разделе 5 приведены God-классы; рекомендация начинать с ConfigMakerUserControl/App — пункт 20 **исторического плана раздела 7**. Раздел 16 содержит более поздний сводный план. Раздел 35.1 посвящён nullable reference types; глобальная nullable-миграция не выполнена. Рекомендации документа не расширяют полномочия пользователя автоматически.

Аудит указывает для прежнего среза ConfigMakerUserControl 3574, App 999, BlockCheck2ViewModel 1270 строк. Эти числа нельзя использовать как исходные размеры текущей ветки: на базе f39 уже есть partial-файлы и предыдущие изменения. В таблице ниже считаются физические строки соответствующих исходных классов базы f39 и e42, включая using и комментарии, без bin/obj; это не сложность и не процент покрытия.

| Владелец | База f39 | Срез e42 | Итог |
| --- | ---: | ---: | --- |
| ConfigMakerViewModel, все partial-файлы | 2143 | 1117 | Извлечены самостоятельные обязанности; UI host ещё крупный |
| App, все partial-файлы | 955 | 754 | Извлечены activation routing и координация окон |
| BlockCheck2ViewModel, все partial-файлы | 999 | 555 | Извлечены options, progress, run result и run controller |
| BlockCheck2ResultViewModel | 565 | 268 | Выделены назначения, manual preset state и test controller |
| ConfigMakerPresetResources | 507 | 232 | Совместимый фасад к registry/reference editor |
| CreateNewConfigViewModel, все partial-файлы | 1037 | 1001 | Ограниченный проход: только condition draft |

Новые partial-части больших классов не добавлялись. Извлечённые объекты владеют отдельным состоянием/процессом и получают узкие зависимости; перенос строк между partial-файлами не считается завершённой декомпозицией.

## Карта ответственности после изменений

| Область | Самостоятельные владельцы | Что остаётся в host / адаптерах |
| --- | --- | --- |
| ConfigMaker structure/resources | `ConfigMakerPresetTreeBuilder`, WinUI tree adapter, resource registry, reference editor | Публичные binding-типы, UI-коллекции, реальные операции редактора и разрешение внешних путей |
| ConfigMaker help/test/designer | CommandHelpPanel, CommandHelpLoader, TestSession, SimpleDesigner | DispatcherQueue, локализованные UI-эффекты, прежние component/backend endpoints |
| ConfigMaker document/files/variables | DocumentCoordinator, PresetFileReplacer, PresetFileActions, VariableActions | Те же document/collection identities, dialogs/editor/diagnostics callbacks, внешние storage/IO endpoints |
| App | ActivationDispatcher/request adapter, FileActivationHandler/operations, WindowCoordinator | Startup/platform interop, реальные окна, migration/protocol/direct endpoints и их политики |
| BlockCheck2 main | OptionsViewModel, ProgressViewModel, RunResultViewModel, RunController | State, Targets, site lists, подготовка/вызов backend, history wrapper, generated-root cleanup и сообщения |
| BlockCheck2 result | ManualAssignmentEditor, ManualPresetState, ManualStrategyTestController | Load и публичные коллекции/уведомления, реальные IssueItem/localizer, исходные service endpoints |
| CreateNewConfig | `CreateConfigConditionDraft` | Остальные переменные, Remove/VariableChanged, Save, Import, Test, Logger, native flyout/view |

Новые конструкторы владельцев сохраняют данные и callbacks; внешние endpoints вызываются в прежних местах через адаптеры. Это не новая система persistence, security или запуска компонентов.

## Проверенный финальный срез

Все строки ниже относятся к указанным pins. Более ранние зелёные проверки в историческом отчёте не подменяют эти результаты.

| Проверка | Фактический результат | Предел доказательства |
| --- | --- | --- |
| Root xUnit, e42 | **1441/1441**, 0 failed, 0 skipped, 13 s | Production-исходники и настоящие модели, управляемые endpoints; не весь WinUI runtime |
| Root GUI Release x64, SDK 8.0.427 | **0 warnings, 0 errors**, 42.20 s | Компиляция/XAML; приложение не запускается этой командой |
| Actual-host fixture, e42 | **51 PASS + COMPLETE**, exit 0, без FAIL/stdout/stderr | Реальные host/owner/adapters с 13 оговорёнными boundary-файлами, 10 bootstrap, инертными внешними вызовами; не native UI smoke |
| Source inventory, f39 → e42 | **451 исходный exported type**, 517 → 574 GUI C# files, 0 удалённых/изменённых сравниваемых сигнатур | Syntax inventory; не semantic inheritance или ABI |
| Compiled contract, pristine f39 → normal e42 DLL | **452 исходных exported types**, 0 удалённых/изменённых recorded shapes, 7 добавленных internal-interface declarations | Ограниченное сравнение metadata, детали ниже |
| Совместный committed срез GUI e42 + security ee47716 | **1653/1653**, 0 failed/skipped, 38 s; GUI **0 warnings/errors**, 62.55 s | Только названная комбинация commits; WIP другой сессии исключён |

Security pin целиком: `ee4771675956fe213634675f37579362efb7e0eb`; в документации владельца этого среза указан отдельный suite 980/980. Совместная копия: `C:/Users/lumen/Programming/cdpiui-joint-e42-ee47716-20261109`. Отсутствие product-overlap и аддитивное объединение test project проверены; чужое рабочее дерево не изменялось. Этот результат не утверждает, что все будущие security commits совместимы или что весь security-аудит закрыт.

Actual-host копия: `C:/Users/lumen/Programming/cdpiui-complete-e42ac48-20261109`. PID 42532, запуск `10:26:59.2287230Z`, завершение `10:27:01.2864720Z`, exit 0. До запуска проверены 1213 product-root files и полный архив 1457 tracked repository files, build inputs и точные boundary substitutions. Сборка стенда — 0 warnings/errors, 59.36 s. Независимое safety/assertion review принято до первого выполнения. После выполнения sourcecheck прошёл, процесс завершился, три входных файла CreateNew сохранены побайтно.

51 группа покрывает document composition (7), file replacement (5), file actions (5), designer codec (4), App file activation (5), manual preset (5), manual strategy test (5), ConfigMaker variables (5), BlockCheck run lifecycle (5), CreateNew condition draft (5). Подменены ровно внешние границы, перечисленные в manifest: storage/export/autocorrect, окна/activation, settings/backend/history/logger/cleanup и соответствующие view callbacks. Реальные HTTP/probe/component/driver, history IO, native dialogs и production startup не выполнялись. Конструктор CreateNew, condition owner, formatter, validator, localizer и уведомления проверены фактически, с fake view только для SetExitAsk.

Обычная e42 DLL и runtime fixture DLL различаются намеренно:

- Normal product SHA256: `DAFB35EC6A4CCD6B728A4D05FB1712465C79420A3B38633B0F199628BA02552B`.
- Runtime fixture SHA256: `814BB17DAF15F1B930C1FFCAFB472DD11C7F13325D92FFB4697EFD0AF1AE0733`.
- Pristine baseline DLL SHA256: `63E250F6B9553663F6601C7F955377E4E0E49E2CC0003684F775888118C58A14`.

## Совместимость и принятые решения ревью

Компилированная baseline-копия содержит ровно 1374 tracked файла f39 и пустой generated Secret stub; проверена до/после сборки, без выполнения приложения. PEReader-анализатор не загружает WinUI. Identical/recorded-shape positive controls прошли; negative control вернул ожидаемый exit 2 и 16 изменений, отдельные assertions подтвердили 11 категорий изменения контракта. Positive control намеренно имеет другое поведение методов и доказывает только сохранение recorded shapes.

Первый compiled comparison обнаружил 16 изменений Virtual/Final/NewSlot от неявной реализации внутренних портов прежними public members. В checkpoint 74659b7 это устранено явными private interface forwards в пяти файлах; прежние endpoints вызываются ровно один раз, storage Tasks возвращаются непосредственно. Финальный e42 comparison подтверждает сохранение записанной поверхности. Сравниваются declared public/protected members, direct base/interface identities, relevant flags, generic constraints, parameter names и Constant-table defaults/literals. Не доказаны полный inherited/dependency surface, runtime MethodImpl maps, assembly-version compatibility, все attributes/NRT/params/caller-info, attribute-encoded decimal/DateTime defaults, interop layout/marshalling или полный managed/native ABI.

Независимые ревью отдельных диапазонов и общего промежуточного diff учитывались до checkpoints. Important-замечание общего review к повторному text/designer roundtrip неизвестного inline-аргумента исправлено в 80521fc: сохранены нужные `=` и quotes, добавлены 21 multi-visit case. Это отдельный проверенный codec fix, а не утверждение, что произвольные форматы команд теперь поддерживаются. Единственное отложенное Minor — дублирование file-kind mapping между facade/reference editor; дефекта поведения этим review не установлено.

При extraction сознательно сохранены исходные правила и их практические ограничения:

- Document self-load может очистить собственные данные; после await/events читается текущий документ. Параллельные saves публикуют результат по порядку завершения, readonly перед confirmation проверяется однократно. Новые snapshots, rollback или serialization policy не вводились.
- App сохраняет различие core-direct/GUI-direct assignments, повторное чтение redirected Launch arguments после await и прежние file/protocol/direct endpoints. Это не закрытие protocol/security рекомендаций аудита.
- Manual preset/test сохраняет current-session/current-list чтения, completion order, stale-item и pending Load поведение; cancellation остаётся обязанностью backend. Новые locks/rollback/унификация comparers не добавлены.
- Variable Edit сохраняет найденный после await index при последующих reentrant events, но передаёт редактору текущий текст. Исходный dialog adapter формирует OriginalName после ожидания. Возможные старые late-dialog/reentry/partial-error эффекты остаются.
- Run prelude остаётся вне try; CTS создаётся до Running, final state вычисляется до await history. ReferenceEquals защищает очистку поля CTS; прежний finally всё ещё выполняет shared progress finish и cleanup текущего root при reentry. Новая безопасность generated-root lifecycle этим не доказана. Реальный history wrapper по-прежнему сообщает ошибку IO, не отменяя успешный результат.
- CreateNew сохраняет ASCII validator, удаление только обычных пробелов из preview name, whitespace On/Off, raw Add без validation guard/trim/reset и частичные изменения при синхронном исключении observer.

Эти ограничения зафиксированы в тестах/ревью там, где проверялись. Они не считаются исправленными дефектами исходной версии и не маскируются новым поведением в рамках рефакторинга.

## Открытые gates и следующий проход

Native Task 15 (Add/Move в реальном интерфейсе) и Task 16 (интерактивные control/workflow observations) **не закрыты**. Исправленный отдельный native-стенд `C:/Users/lumen/Programming/cdpiui-ui-manual-9211253-20261109` подготовлен и собран на прежнем pin **9211253**, но не запущен; это не native-проверка финального e42. Последний read-only просмотр содержимого окна **в 10:59 UTC 9 ноября** вновь показал consent.exe 3803510: UAC для ToolsSmokeTest.exe неизвестного издателя; точные path/args не подтверждены, UIA предоставляет только корневое окно. Последующие наблюдения наличия того же handle не заменяют новый просмотр его содержимого. По этому окну не выполнялись подтверждение/отмена или обход. Программные проверки моделей и actual hosts не заменяют эти два gates. В последующей сессии после появления доступного desktop нужно перенести подготовленные ограниченные native-сценарии в новую копию проверяемого product pin, заново проверить точные boundary substitutions/build inputs и получить safety review, затем выполнить сценарии и записать наблюдения. Старую pinned-копию сохранить.

Следующие крупные обязанности остаются отдельной работой: остальной ConfigMaker host/code-behind; оставшийся startup/platform/modal/theme/error lifecycle App; site lists, внешние операции и cleanup BlockCheck2; Save/Import/Test и прочая логика CreateNewConfig; WelcomeViewModel. God-классы ZapretConfigImportService, ConfigurationService, Zapret1ToZapret2Converter из аудита этим проходом не декомпозированы. Новые изменения в этих областях требуют своего ограниченного проекта, tests и review; текущая сводка не даёт им статус готовности.

Security/staging/ZIP/Update и persistence принадлежат параллельной сессии. Durable crash journal, installation mutex, signed-package/runtime handoff, argument leases, final restart/MSI/uninstall и прочие её открытые границы не закрыты GUI-декомпозицией. Глобальные nullable/async/analyzer рекомендации аудита также не объявлены выполненными.

## Воспроизведение и локальные свидетельства

В этом окружении используется `C:/Users/lumen/.dotnet/dotnet.exe` **8.0.427**, конфигурация Release, GUI Platform=x64. Для повторной проверки в подготовленном рабочем дереве:

```powershell
& C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore --verbosity quiet
& C:/Users/lumen/.dotnet/dotnet.exe build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 --no-restore --verbosity quiet
git diff --check
```

Логи и анализаторы сохраняются локально в ignored `.superpowers/sdd/2026-11-08-class-decomposition/`; они не публиковались. Ключевые файлы: `task37-full-tests.log`, `task37-gui.log`, `complete-host-build.log`, `complete-host-run.log`, `complete-host-run-manifest.txt`, `joint-e42-ee47716-tests.log`, `joint-e42-ee47716-gui.log`, `joint-e42-ee47716-manifest.txt`, `api-compatibility/result-e42ac48.log`, `compiled-contract/product-condition-draft-result.log` и соответствующие JSON/binary snapshots. Ранее полученные RED/GREEN логи и pinned copies сохранены. Генераторы копий и одноразовые runners нельзя повторно запускать поверх уже существующих каталогов.

Работа сохраняется локальными коммитами в собственной ветке. Merge, push/publish, force-push, reset, удаление исходников/веток и правки чужого незакоммиченного дерева не выполнялись. Завершённые проверенные проходы готовы к отдельному рассмотрению и последующей интеграции; весь аудит и непроверенные native gates остаются явно открытыми.
