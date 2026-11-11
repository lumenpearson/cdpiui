> Historical local-stage record for frozen dev/audit-class-decomposition at eebc7e8.
> Current integration work is tracked in [the integration plan](superpowers/plans/2026-11-11-class-integration.md).

# Карта приёмки декомпозиции

Актуальная проверка: 11 ноября 2026 года. Текущийкодed390cd/issue215/продолжение89–91:
32focused/2108full/GUI0-0/source451(606)/compiled4520loss+7old; helper29/Editor149/control820.
ExactEEjoint2320/GUI0-0, correctedpeerf706+Root+EE2398/GUI0-0; source/XMLpostchecksPASS.
Native137/21cuts собран0-0/48.44s после3Count→Length+одногоusing исправлений в новом casefile;
первый failedbuild сохранён,23pins первой собранной копии проверены, но запуск запрещён из-за
ошибки атрибуции общих preference counters. Отдельная r2 собрана0-0/69.10s, independentREADY0C/I,
actual137PASS(first133)/COMPLETE/exit0/allpostchecks23pins/PID79052gone/explicit83→release84.
Final91ACCEPT0unresolvedCritical/Important/Minor наproducted390/docscee65ac;215closedcompleted6[x]
и actualProject85Done/P2ArchitectureMediumG2Lumens3labels послеreload подтверждены. Parent14/15 открыты.
Подробные текущие границы: [протокол215](superpowers/reports/2026-11-11-diagnostic-highlighting-verification.md).
Предыдущее принятое продолжение00f39b1/issue211/86–88:
30focused/2076full/GUI0-0,Core88(103→104files)/compiled88/0loss; GUI605sources unchanged174,
compiledGUI4520loss+7old. ExactEEjoint2288/GUI0-0, peer9a9+Root+EE2338/GUI0-0; final88ACCEPT0/0/0,211completed+actualProjectDone readback confirmed.
Предыдущее завершённое продолжение83–85/issue209: код174d1cb,
20focused/2046full/GUI0-0/source451(605)/compiled452(0+7); exactjoint2258/GUI0-0.
Native133PASS+COMPLETE/exit0/allpostchecks; final85technicalACCEPT0/0/0, issue209completed/actualProjectDone readback confirmed.
Базаf39e8a0a, последний завершённыйproduct/runtimea5a1b9b:
129PASS+COMPLETE/exit0,30focused2026full; joint exacta5+EE2238/2238/GUI0-0.
Source451(604files)/compiled452(0+7), normalGUI0-0; finalTask82evidence/docsreviewACCEPT0/0/0.
Предыдущаядетализация80–82 сохранена. Предыдущий61fd/productb5:125/47-1996/joint2208/final79ACCEPT0/0/0.
Предыдущиеcontinuations:2e/test491/121,72-1949/joint2161;0c5/115;ff6/111,
test-onlydfaccbd9/38-1846;930/107;de3/103;839/97. Матрица55–58 сохраняет историческийD347/test3ba;
native2 —product214. Предыдущаядетализация77–79 сохранена. Веткаdev/audit-class-decomposition,
деревоC:/Users/lumen/.codex/worktrees/audit-class-decomposition/cdpiui.

Пользователь согласовал порядок ConfigMaker → App → BlockCheck2, сохранение поведения и самостоятельное продолжение без срока. Аудит F:/Telegram/AUDIT.md используется как перечень наблюдений: §5 называет god-классы, пункт20 исторического §7 рекомендует начинать с ConfigMakerUserControl/App. Текст аудита не даёт дополнительных разрешений. Глобальная nullable-миграция §35.1, исправление всех дефектов аудита и завершение всех крупных классов не входят в выполненный пакет.

## Обязанности и физические размеры

| Исходный host | Выделенные самостоятельные владельцы | Было → сейчас |
| --- | --- | ---: |
| ConfigMakerViewModel, сумма9partial | TreeBuilder/Adapter, resource Registry/ReferenceEditor, CommandHelpPanel/Loader, SimpleDesigner, TestSession, DocumentCoordinator, FileReplacer/FileActions, VariableActions, Diagnostics, PresetStructure | 2143 →1012 |
| ConfigMakerUserControl, сумма5partial | PanelLayoutController с ленивым WinUI-адаптером, DiagnosticHighlightingUpdater; control сохраняет реальные editor/tree/dialog/DP bindings | 899 →820 |
| UtilityButtonControls.xaml.cs, текущий дополнительный проход80 | TwoPhaseTransitionSequence, System-only механизм; control сохраняет WinUI/Composition/CTS cleanup callback | 536 →500 |
| MenuBarController.cs, дополнительный проход83 | PostOrderTreePruner, System-only обход; claims/registry/physical mutation остаются в controller | 519 →502 |
| ConfigurationService.cs, дополнительный проход86 | ConfigurationVariableProjection, stateless domain read-model adapter; guards/lookup/locks/IO/updates остаются в service | 443 →411 |
| ConfigMakerPresetResources | ResourceRegistry и ReferenceEditor; прежний фасад/публичные entrypoints сохранены | 507 →232 |
| App, сумма6partial | ActivationDispatcher/RequestAdapter, FileActivationHandler/Operations, WindowCoordinator/Operations, WindowCloseCoordinator/Operations, IdleExitController | 955 →677 |
| BlockCheck2ViewModel, сумма3partial | Options, Progress, RunResult, RunController, SiteListController/backend | 999 →426 |
| BlockCheck2ResultViewModel | ManualAssignmentEditor/IssueItem, ManualPresetState/Operations, ManualStrategyTestController/Operations | 565 →268 |
| CreateNewConfigViewModel, сумма6partial | ConditionDraft, TestSession, GraphicDesigner, ComponentSelection | 1037 →818 |
| BlockCheck2ResultPageViewModel, сумма4partial, срез855dbd3 | ResultSelection, ReportSummaryPresenter, SelectedStrategyTest, ReportExport, ManualTestSession, AssignmentActions с нейтральным SelectedItemChoiceAction | 874 →513 |

Считаются физические строки вместе с using/комментариями. Уменьшение host не равно снижению общего количества строк. Новые объекты содержат перенесённые обязанности и явные зависимости; новых partial-частей для сокрытия объёма крупных классов не добавлено. В host остаются UI-адаптация и ранее согласованные соседние обязанности. Декомпозиция всех оставшихся крупных объектов не заявляется.

## Историческая матрица55–58: требование → свидетельство → предел

| Требование | Проверено | Предел |
| --- | --- | --- |
| Сохранить поведение при переносе | Целевые absent-owner RED→GREEN, characterization уведомлений/исключений/reentry/await/current-state, полный suite на текущем product1719/1719,0failed/skipped; повтор после test-only уточнения 11s | Тесты не доказывают отсутствие всех возможных регрессий |
| GUI продолжает собираться | Текущий normalGUI x64 Release, SDK8.0.427:0warnings/errors65.78s | x86/ARM/полный production startup не проверены |
| Исходный внешний контракт | Source:451исходный exported type/0потерь сравниваемых сигнатур,517→592GUI C#files | Синтаксическое сравнение, не полная semantic/attribute/API гарантия |
| Скомпилированный контракт |452исходных exported types/0потерь/изменений записанных shapes,7ранее добавленных internal interfaces; positive/negative controls анализатора | Нет полной гарантии NRT/всех attributes/inherited dependencies/interop/ABI/произвольной reflection; диагностические async stack/атрибуты могли переместиться |
| Проверить настоящее сочетание host/owner/adapter/model | Actual runtime наd347c617:91PASS+COMPLETE/exit0/postchecks | BackendIO и часть UI-targets заменены явно; отдельные дополнительные группы имеют свои pins |
| Проверить native manual editor Task15 | Native4 наd347:empty →Add split →Add disorder →Move split down, реальные кнопки/коллекции/identities/1-based order/flags/draft/action tokens/12events | Нет живого CURL/component test, Add for list, remove/clear UI, repair/report export |
| Проверить native document workflow Task16 | Native4 наd347:реальные editor/DP/model/document,readonly,Save cancel/success с вводом имени,Export coordination,New cancel/continue | Private storageCore/export/pickers/settings/task endpoints инертны; фактические файлы пресета/архив экспорта не записывались |
| Не принять частичный успех за завершение | Первыйnative PID52048:11UI PASS/COMPLETE/exit0,но общий FAIL из-за Settings.xml/bak. NEWnative2 PID36508:все11+postchecks PASS | Первый результат остаётся FAIL, даже после успешной новой копии |
| Не мешать security-сессии | Read-only committed `ee4771675956fe213634675f37579362efb7e0eb`; новый jointd347+EE:1931/1931,GUI0/0 | Её рабочее дерево/WIP не включались, последующие её изменения не проверялись |
| Сохранить проект и свидетельства | Локальные scoped commits, старые копии/входы/логи/архивы сохранены, нет reset/force-push/merge/publish/удаления исходников/веток | Ничего не отправлено на GitHub и не интегрировано в общую ветку |

## Точные исторические артефакты55–58

Normal DLL исторического productd347 SHA256 `07899304940A75B7749A052271874CE3D1A3E141919856D4663EA805F0A5B423`. Это обычный бинарник того этапа, не DLL с заглушками стенда. До закрытия58 после d347 менялись только тест и документация; последующие product/runtime pins приведены в продолжениях ниже.

Actual-host стенд: `F:/cdpiui-verify/cdpiui-struct2-d347c61-20261109`, GUI DLL `280A28248ECBA6CE51A545A7B3F92C12443FF4213F9E1FECCFE292A019B06EB6`, PID35892,91PASS/COMPLETE/exit0. Последний совместный срез: `F:/cdpiui-verify/cdpiui-joint-structure-ee47716-20261109`,1931/1931 иGUI0/0.

Native2: `F:/cdpiui-verify/cdpiui-native2-214b4b3-20261109`, GUI DLL `86EC3A2759FFF4A83A578F621DFC540B7D6A05A10BB248976BCE5CE6CF510ED3`,19явных cuts/1482repo inputs/1230project inputs, XAMLсовпадает с product. Реальный public storage Clone+Task.Run сохраняется; private core возвращает контролируемый результат. ConsoleFont/Search preferences в памяти, SettingsManager guard срабатывает до XML base и проверяется также после Application.Start. Стенд не является полной OS/filesystem sandbox.

PID36508:15:44:45.2758468Z→15:47:31.5834895Z;11PASS/COMPLETE/exit0,0FAIL/пустые stdout/stderr, sourcepostcheckPASS,процесс завершён. `Settings`, export иcustom output отсутствуют; только input+result log появились в bin, прежние имена сохранились. Input23bytes `native.example.invalid\n`, SHA256 `70CE5088BE2697D3D75728DEDE2F0DA5DF9FF27BCF9B8525650AC3C141E105F7`. Проверка имён не доказывает отсутствие transient IO/перезаписи старых bytes во всех местах системы.

Локальные проверочные scripts/logs/AX/manifest/retainedJSON находятся в `.superpowers/sdd/2026-11-08-class-decomposition/`, исходные архивы новых копий — в `F:/cdpiui-verify/archives`. По новому запросу пользователя37 завершённых C:копий перенесены на D с SHA256 и junction; старые пути и содержимое сохранены. Git worktrees не переносились. Для этих копий запрещён слепой повторный запуск builder/runner: предусмотрены freshness guards. [Текущая подробная история](DECOMPOSITION-STATUS.ru.md), [план](superpowers/plans/2026-11-08-class-decomposition.md), [спецификация](superpowers/specs/2026-11-08-class-decomposition-design.md).

## Оставшиеся границы

Не выполнена глобальная переработка App migration/Win32/tray, всей ConfigMaker/CreateNewConfig UI-оболочки и всех storage/import/export/settings сервисов. Реальные backend операции и полный production startup/window close policy/drag resize потребуют отдельных проверок. Security/staging/Update/ZIP/settings persistence остаются за параллельной сессией. Ни «весь аудит закрыт», ни абсолютное «регрессий нет» не утверждается; выполненные проверки не обнаружили product-регрессий в своей области.

Финальное независимое ревью всей ветки завершено после native Task53; его результат и отложенные замечания записаны ниже. Полный журнал принятых решений и отложенных Minor сохранён в [приложении](DECOMPOSITION-DECISIONS.ru.md).

## Историческое итоговое ревью Task54

Свежий reviewer без контекста переписки принял весь диапазон f39e8a0a→1d76cbdb: **0Critical,0Important,3Minor**. Просмотрены153пути/68коммитов за несколько проходов, четыре кластера и все пять Review Focus;96Presentation source links наGUI существуют/без дубликатов Link,30файлов presentation tests изменены. Ревью сверяло исходники и сохранённые результаты; оно не запускало повторно сборки/тесты/приложение/manifest writers. На завершении Task54 обязательных product-исправлений не было; тот review относится к214b4b3.

Три Minor после собственной сверки с baseline отложены:

- Несколько ConfigMaker properties потеряли **private** setters: PropertyInfo.CanWrite изменился true→false для IsHelpLoading,HasCommandModules,UsageText,CommandArgumentPlaceholder,CommandArgumentLabel,AdditionalDesignerArguments. Публичных C# setters раньше не было; зависимого потребителя/привязки в репозитории не найдено. Произвольная reflection-совместимость не гарантируется этими API inventories.
- PreferRequestedActions(null!) теперь получает None от адаптера и делает fallback без прежнего warning. Для штатного непустого WinRT event расхождения не найдено. Если диагностику неверного вызова восстанавливать позже, guard нужен внутри redirected try; initial null допускается.
- CDPIUI.Tests README.md/README.ru.md описывают первые Presentation группы и двусмысленно исключают GUI. Сейчас unit tests компилируют выбранные productionGUI sources, WinUI application вxUnit не запускается. Обзор README и ссылка на эту карту остаются polish-задачей.

Все13 сознательно исключённых reviewer областей получили отдельное решение с ценой ошибки в [полном приложении](DECOMPOSITION-DECISIONS.ru.md). Прежние13Minor также сохранены; некоторые их риски дополнительно проверены поздними actual-host группами, managed polish не выдаётся за исправленный. Нового source-кода после successful product проверок не было; для финальных текстовых изменений suite/GUI повторно не запускались. Локальная ветка и все материалы сохраняются, публикации/слияния/удаления не выполняются.


## Продолжение 55–58

Владелец состояния PresetStructure выделен наd347c617, 36 новых managed cases. Preliminary review всего
продолжения28ceef44→d347c617 принял product без Critical/Important. Единственный Minor усилен тестом
3ba7d388 и подтверждён повтором36/1719. Реальный host/owner/WinUI adapter прошёл91группу; точные три
входных файла проверены отдельно побайтовым сравнением. Нативный повтор11наблюдений наd347/native4 принят со всеми postchecks; Task57 закрыт.
Task58 принят итоговым независимым ревью с уточнением границы actual-host проверки. Предыдущие результаты сохраняют собственные pins.


Текущий native4: F:/cdpiui-verify/cdpiui-native4-d347c61-20261109, GUI SHA256
`E82D1B44B5596044725335F92505249EF5FD33B7D10504BED6B0F50E381104BA`, bootstrap
`7EDB334D3B997083E001E6D5250421E67492F4B42C86A7583D34E231390342CC`, точный source-boundary manifest
`E95C8649CE8CDD4EBE38782E68232629CDE5808D0F20BD498510DD11B6910101`. PID50036,11PASS/COMPLETE/exit0,
0FAIL/пустые streams/1231+1486 post-source/manifest/input23bytes/Settings-export-custom absent/
only2newbinfiles/processfinished подтверждены. Конструктор SettingsManager блокирует XML до base;
sticky switch проверен Finish и после Application.Start. Пределы native2 по реальным backend/IO/полному
OS наблюдению сохраняются. Native3 PID2228 остаётся FAIL/exit1: незапланированный ввод, причина не доказана.
Для native4 подтверждена исключительная очередь трёх чатов со всеми UI API; после завершения передана peer.

Итог review28ceef44→3ba7d388+документы:0Critical/0Important/1Minor. Actual-host reentry изменяет команду
и явные группы; компонент задан заранее. Его смена во время публикации остаётся managed+source-only
proof, отдельный actual-host case отложен с ценой возможного пропуска будущего кэша компонента.
Документальное уточнение выполнено, product-исправлений не потребовалось. Все пять Review Focus
проверены,111Ruling и17deferredMinor сохранены в приложении. Продолжение55–58 завершено в указанной области.

## Продолжение 59–61

Product839cc952: AppIdleExitController,39focused/1758full/GUI0warnings-errors52,94s,
source451/517→593/0comparedchanges, compiled452/0recordedchanges/7прежних interface adds.
Normal DLL `0DD76BD2DFC2E6101E2BB09CCB5FC094541EC2F547E821F3B65E5FB157F4BA1D`.
Joint exact839+ee47716:1970/1970 иGUI0/0. Fresh product review2400dee→839cc952:0Critical/Important/Minor,
все пять новых и пять исходных Review Focus, прежние App callers/Store subscription/close callback.

Actual idle host:97PASS+COMPLETE/exit0, PID61180,05:09:45.0171515Z→05:09:49.2267677Z 10ноября2026UTC,
1232project/1489repo/18boundary files/19bootstraps; сборка0/0 за71,39s и prelaunchREADY0/0/0.
Реальны current list/migration/private host forward/owner/App-bound callback, инертны Store/Good/Create/
Close/Exit/Warn; без настоящего Storeevent/PrepareWindow/idle-policy nativeExit/fullstartup.
Сам стенд вызывает реальный Application.Exit при завершении. GUI fixture SHA256
`CD9389B6B9DE9F21807F3A3DCCF2AC4FE55A037BE4B7056A1B5D0DF1A33206E4`.
Postchecks PASS: пустые streams/noFAIL/source+approved hashes/три точных входа/Settings-export-markers
absent/4новых binnames/прежние имена сохранены/processfinished. Общая OS isolation не доказана.

Явный handoffV4Root после peer process/windowfinished и lessonswithdrawn; Root noUIAPI, после97
передалV5peer/native-governance и allUIpaused. Runtimegate60 иreview61 закрыты:0Critical/Important,
1Minor о формулировке входных файлов исправлен (16/16/15байт). Native4/d347:11интерактивных наблюдений остаются на прежнем product. Старый pendingcheckpoint
b848620 и неуспешные native-прогоны сохранены. Новых продуктовых исправлений после839 нет.


## Продолжение62–64

Page main432→380/sum874→822, owner179lines/15properties/7lazyports. Productde3d3821:
28focused/1786full/GUI0-0/source451(594files)/compiled4520+7; joint exactde3+EE1998/GUI0-0.
NormalSHA9EBC1DD5C23DBA121C27584CAC999197A104BC67DC9932D9E9FCFAF0E6990E16.
Productreview0Critical/Important/1Minortestcoverage закрытtest-onlyffa65fc9: nullablelabelobserver,
controlled-cachemutantRED→originalsource29focused/1787fullGREEN. Product/normalGUI unchanged.

Actualselection2host103PASS+COMPLETE/exit0, PID45692,2026-11-10T05:39:53.4500889Z→05:39:57.3058215Z,
0FAIL/emptystreams/source1233project1492repo/exact18cuts20bootstraps/pins/3inputfiles16-16-15bytes/
Settings-export-markersabsent/4newbinnames/priornames/processfinished/5actualbinarySHAprelaunch+postexit.
Build0-0/55,58s/prelaunchREADY0/0/0. Firstselectioncopyunlaunchedpreserved afterfalse-positivecase review;
selection2distinguisheschangedResult/listpredicateandmenuactionsorder. ActualrealPage/owner/currentResult/
HasSession/localizer/evidence/listpredicate/hostsender+private-set, fakeviewSelected/Menuonly, no lifecycle/
component/backend/native calls; managedlocalizer/format/culturefaults remainownscope. Old97unchanged;
nativevisual/startup/wholeOS/ABI notproved. XAMLunchanged. GUI fixtureACC8ECD1…/source-boundary host-fixture-manifest.txt36A52817…/
runner798B3F99… exactfullhashesinSTATUS. V6Root→V7released afterpostchecks/allUIpaused.
Finalreview64ACCEPT0Critical/Important/Minor, test/doc уточнения закрыты. Все103unique/first97sameordertext,
оба журнала побайтно совпали,522→526bin files/currentdiskhashes matched,118Ruling17Minor exactledger.


## Продолжение65–67

Product93091a13 ReportSummaryPresenter,Page353/sum795.20focused/1807full/GUI0-0/API451(595)/452(0+7),
joint exact930+EE2019/2019/GUI0-0. NormalC4DCFA8E0BD20B07435276DB3912C0D82ABA8F11A89E2103E8DC643F717665F1.
Productreview0Critical/Important/1Minortestcoverageclosedby4d19d461: BestEffort-onlytitlecallback/repeatedread,
controlledcachedBestmutantRED→restoredsource21focused/1808fullGREEN; no product/GUIbinary change.

Summaryfixture107PASS+COMPLETE/exit0,PID45484,2026-11-10T06:00:48.7342523Z→06:00:52.8956430Z,
build0-0/68,59s,prelaunchREADY,source1234project1495repo18cuts21bootstraps. AllpostchecksPASS:
noFAIL/emptystreams/source+5sourcepins/3inputfiles16-16-15bytes/forbiddenoutputsabsence/4newbinfiles/
priornames/PIDgone/5actualbinarySHAprelaunch+postexit. Exacthashes/pathsSTATUS; source-boundarymanifest1A0C…
is distinct fromruntime-run-manifest. New4groupsrealPage/Result/report/Localizer/privateforward/hostdelegates,
inertShowStatusonly; otherviewcallsThrow, noUpdateView/lifecycle/component/native. Old103same scope,
managedTextreentry/localizerfaults notclaimedasnativeLocalizerinjection. No visual/startup/wholeOS/ABI proof.
V7released→explicitV8Rootafterallpaused+readyfalse/prelaunchREADY→V9released afterpostchecks/allUIpaused.
Finalruntime/docsreview67ACCEPT0Critical/Important/Minor;107unique/first103sameordertext/logsbyteequal,
5binary/5sourceSHA independentlychecked. No newproductsourceafter930; joint2019/native107retain930,
21/1808belongs4d19test-only. 120Ruling17Minor mirrorsretained; continuation65–67closed.

## Продолжение68–70

Product ff6bc636: SelectedStrategyTest, одиннадцать ленивых портов; host сохраняет публичный Task,
private result forward, поле running/private progress setter и sender. Main353→300/sum795→742.
36focused/1844full/GUI0-0/54,40с; source451(596files)/compiled452(0+7), immutable ff6 и working/current
GUI inputs совпадают. Обычная DLL965649633BC6B14C034ECEF43E4FD908C2A7F5FF03855418D14280E9342B42E1.

Product review: 0Critical/Important, два coverage Minor закрыты test-onlydfaccbd9:
реальный Canceled Task и замена Error issue при локализации формата. Cache-firstError mutant RED,
original source bytes/SHA restored;38focused/1846full GREEN, повторное review0/0/0. Runtime111 pinff6
содержит первоначальные36/1844; joint exactdfaccbd9+EE2058/2058/GUI0-0 включает оба новых теста.

Actual111: PID9112, UTC15:06:27.9269258→15:06:37.3261527 10ноября2026, build0-0/78,84с,
independent prelaunchREADY,1235project/1498repo/exact18cuts/22bootstraps. Реальные Page/Result/helper/
Localizer/private forward/host flags/sender/finally, отрицательный current Result через замену session
на sessionless в progress observer и backend trap0calls. Status/menu/command view контролируются,
реальные backend/component lifecycle не вызываются; pending/cancel/callback mutation — managed scope.
111unique/first107sameordertext/COMPLETE/exit0/noFAIL/empty streams/logsbyteequal/5actualbinarySHA
доlaunch-послеexit/6sourcepins/3inputfiles16-16-15bytes/forbiddenoutputsabsent/522→526binfiles4newnames/
allpriornames/PIDgone. Полные pins/paths в STATUS; source-boundarymanifest3BBDC5DF… не равен runtime manifest.
ЯвныйpipelineV15handoff→V16Root→V17released послеpostchecks/ALLRootUIpaused, pipeline уведомлён.
Старые107two-nonactivatedWindows/realExit teardown и прежние native pins сохраняют свой scope.
Full startup/interactive visual/native/whole OS/ABI proof не заявляется. Task70 закрыт final review0/0/0,
два тестовых Minor и историческая joint-фраза исправлены;122Ruling/17deferredMinor сохранены.

## Продолжение71–73

ReportExport: семь ленивых портов, публичные JSON/text Task и private SaveAsync forwards сохранены,
current report читается после picker внутри catch; no-report/cancel/setup faults/pending/concurrent/
reentry/realCanceledTask/success status fault/error identity покрыты31managedcases. Serializer/clipboard
не менялись. Page sum742→705/main300→305, export85→43. Product0c5ea124:31focused/1877full/GUI0-0
18,46с/source451(597)/compiled452(0+7). Normal0A96C87463214289EA0B9FE6B04D1328C312ABA9F7AE45D417E7A8903946B3EF.
Joint exact0c5+EE2089/2089/0failed-skipped47с/GUI0-0/69,41с.

Оба product review Minor закрыты: actualRED2111PASS+namedconstructorFAIL/innerNRE:55/exit1 наbd91343
подтвердил eager `_view.PickSaveFile`; lazy lambda fix0c5 сохраняет null-view/no-report поведение.
Test-only62e усилил returnedTask fault timing; контролируемое удалениеasync дало4JSONFAIL/4textPASS,
sourcebytes/SHA restored;31/1877GREEN. Первоначальный115bd и compile-failed red1 сохраненыUNLAUNCHED;
RED2 —ожидаемоеFAILсвидетельство, его4newfiles/3reportpathsabsent/postchecks прошли, это не GREEN115.

Исправленныйactual115 использует тот же B7AEBF2B casefile,23bootstraps/18cuts/1236project1501repo,
build0-0/69,96с/prelaunchREADY. PID71744 UTC15:52:46.0727531→15:52:50.1694270 10ноября2026,
115unique/first111sameordertext/oneCOMPLETE/exit0/noFAIL/empty streams/logsbyteequal/5actualbinarySHA
доlaunch-послеexit/7sourcepins/3inputs16-16-15/2realretainedownreportoutputs354-260bytes/6newnames/
522→528/priornames/negativepathSettingsmarkersabsent/PIDgone. JSON/text прочитаны обратно и внешне
проверены literalcurrentmodelfields+SHA; только2freshdirectchildпути вownFbinaryroot, inertpicker/status.
Core Page/Result/Report/Localizer/serializer/privatehelper реальны, null-view ctor теперьPASS.
Backend/componentlifecycle/desktopdialogs/fullstartup/interactivevisual/wholeOS/ABIнепроверялись.
Exactpaths/полныеpinsSTATUS; sourceboundarymanifest9AEC93BC… distinct отruntime manifest,
runnerAB85F091… . Same18cuts и21oldcasepartials неизменны относительно111 (толькоownExePathвcut).
ЯвныеhandoffsV19→20→21RED иV23→24→25GREEN,RootALLUIpausedпослеpostchecks/ack/requestfalse,
pipelineуведомлён; noRootdesktopUI/nootherprocess/worktreeedits. Productreview0/0/0,
finalTask73evidence/docsreviewACCEPT0/0/0;124Ruling/17deferredMinor сохранены. Проверки закрыты на0c5.
Отдельная история main d72ad478→7ead95f7 имеет одинаковыйtree33323ba5 и нулевойdiff; ancestry сменился.
Root не интегрировал новую историю; точные localsource/runtime/joint pins не смешиваются сlivePRdiff.

## Продолжение74–76

ManualTestSession product2e70779 владеет тремя прежними flags и всем lifecycle ручного теста.
Page сохраняет публичные Task/getter, private leave/event-handler forwards и исходный handler TargetPage.
Узкий internal host11members/private nested adapter передаёт эффекты; FuncTasks разрешает связанный
ComponentTasksManager receiver до каждого ID аргумента. GuardID читается отдельно, leave захватывает
одинID доtry. Существующие ConfigMaker adapter/consumers не меняются. Новых guards/locks/dedup нет.
Page705→572/main305→308/manual186→50; все четыре partial прежние, нового public interface нет.
Private поля перенесены: произвольная private reflection не обещается, единственный известный
selection fixture получил проверяемую замену только одной строки доступа к полю owner.

Absent-ownerCS0246 и receiver-portCS1503 RED сохранены; unconditional-restore mutant дал2FAIL.
На фактическом продукте прошли1947full/GUI0-0 за39,26с.
После этих полных проверок ID-before-receiver mutant дал1FAIL; original bytes/SHA восстановлены,
Затем70focused прошли на восстановленном исходнике, после них source451(600)/compiled452(0+7).
Product review0Critical/Important/2Minor к тестам закрыты
test-only491692bc: отдельные gates одновременных запусков и false/null button running→одинStopped.
DroppedStop(true) mutant дал2FAIL, sourcebytes/SHA restored, GUI diff2e пуст;72focused60ms/1949full10с
безfailed/skipped. Повторное product reviewACCEPT0/0/0. Normal GUI SHA256
`670A98479785F6083D76656EAA77EC2CE74243BE9D6A65C785319948D6C6953A`.
Joint exact491+securityee47716 —2161/2161/42с/GUI0-0/61,24с, security worktree read-only cleanEE.

Actual121: `F:/cdpiui-verify/cdpiui-manualsession-491692b-20261110`, build0-0/79,64с,
1506repo/1239project/exact19cuts/24bootstraps+verifiedemptySecretstub, prelaunchREADY.
Новый cut только в ManualTesting helpers: receiver/currentID/logger; mainPage/owner/bound adapter
совпадают с product. Единственная разрешённая правка старого selection helper — private-state bridge,
все остальные assertions и21oldcasepartial прежние. Новые6групп проверяют реальную Page/Result/
session/purewriter/Localizer/privatehost/originalhandler/dispatcher; tasks/confirm/status/menu/logger
инертны. Проверены captured row/current Result, pending confirm/run/stop, restore, один leave ID,
startup guard, две queued exits после stop, exception identity и setup/catch границы.
Boundmanageradapter проверен source/GUI compile, настоящий manager не вызывается. Backend/OSrestore/
native dialogs/fullstartup/interactivevisual/wholeOS/ABI не проверялись, новых IO в6группах нет.

PID69044 UTC17:46:43.3819843→17:46:47.3377306 10ноября2026:121unique/first115exactorder/oneCOMPLETE/
exit0/noFAIL/empty streams/logsbyteequal/5actualbinarySHAprepost+8sourcepins/3exactinputs16-16-15/
2realretainedreports354-260bytes+goldenSHA/currentliteralfields/6newnames522→528/priornames/
negativepathSettingsmarkersabsent/PIDgone. Normal670A неизменён. SHA-pinned дополнительный readback
повторил unique/prefix/logs/13pins/reports/names/absence/normalDLL; runner проверил три input-файла.
Выбранные полные SHA/paths в STATUS, полный список13path+SHA в scratch `page-manual-host-approved-pins.json`;
fixtureDLLCBDCCDEB/sourceboundarymanifest0ED94928/runnerC09821A4/
readback87A835C3; source-boundarymanifest отличается от runtime manifest.

Явный pipelineV27grant Root после его UIfinished подтверждён координатором. V28 только нормализовала
filenamealias запроса уже действующего Root grant. После postchecks/ownedPIDgone RootALLUIpaused,
idleack/requestcompletedfalse/V29releasedowner:null, обе сессии уведомлены. Root desktop/CUA API не
использовал. Task75/76 закрыты, final evidence/docs reviewACCEPT0Critical/0Important/0Minor.
Хронологический документальный Minor исправлен:full1947+GUI→receiver mutant→restore→70focused→API,
затем49172/1949; полный13-entry locator уточнён. Независимо сверены1506fixtureinputs и1533jointfiles/
166overlays/точная additive testcsproj;126Ruling/17deferredMinor сохранены. Новых product правок не было.

## Продолжение77–79

Productb5cce888/test61fd0e7: нейтральный SelectedItemChoiceAction39строк/4порта/System-only и маленький
AssignmentActions80строк/13memberinternalhost/privateadapter. Один текущий потребитель, без обещания
существующего reuse; domain notices/команды отделены от capture/snapshot/optionalchoice/apply.
Прежние Page state/private setters/notification sender/preview/model policy сохранены; sum572→513,
Assignments171→110/main308→310. Sync/Task fault timing, captured row/current Result после await,
mutable snapshot identity, single runtime-null vs multiple-null cancellation, independent choices,
move/reentry/duplicate menus/quickfix прошли характеризацию. Global neutralutility criterion закреплён
в плане и STATUS для всех дальнейших проходов, без массового rewrite принятых владельцев.

Absent-ownerRED, selected-reread-afterawait mutantFAIL1→byte/SHA restore;47focused/1996full/GUI0-0
41,73с/source451(603)/compiled452(0+7). Product review0/0/1Minor о SingleChoice тесте закрыт
test61fd: пары+Assert.Single после await в single/multiple plain-object cases, duplicate-singleapply
mutantFAIL1→restore→47focused38ms/1996full10сGREEN; re-reviewACCEPT0/0/0. Product GUI неизменён. Нормальная DLL24797CCA…,
полныйSHASTATUS. Координатор отдельно выполнил14case standalone plainobject probe на utilitySHA44872BE0…
и13/14negativecontrol(single-nullguard), Root прочёлJSON; это boundedutilityproof, не productruntime.

Exactjoint61fd+EE2208/2208/41с/GUI0-0/59,36с. Все603immutableGUIinputsсовпалисcurrentnormalizedLF.
Actual125 F:/cdpiui-verify/cdpiui-assignment-actions-61fd0e7-20261110:build0-0/71,78с,
1512repo1242project/exact19cuts25bootstraps/старые23casepartial прежние, no newIO/cuts.
Четыре realPage/Result/editor/localizer/formatter/privatehost группы описаныSTATUS: noitem/paths/add/
duplicate/doublemenus/privateforward; pendingcapturedrow/currentResult/snapshot/single/cancel; move/
reentry/reselect/remove/clear/guards/sender; quickfix/liveissuecollection/syncpartialfault/Tasksetupfault.
Chooser/selection/status/menu инертны, backend/components/native/visual/fullstartup/wholeOS/ABI не проверены.
RunnerB9BE0FBC/readbackBB7DAC29; выбранные полные SHA/пути вSTATUS, все14path+SHA —scratch
`assignment-actions-host-approved-pins.json`.
IndependentprelaunchREADY/0блокирующихзамечаний. Helper owncopy дискриминируется managed tests,
actual groups проверяют end-to-end array isolation (реальный editor сам возвращает новый массив).
Joint1538paths/1537Gitblobs+exactadditivecsproj109items/172overlaypaths независимо сверены.
Actual125 PID68696 UTC18:37:05.7871699→18:37:17.5133853 10ноября2026:125unique/first121exactorder/
oneCOMPLETE/exit0/noFAIL/empty streams/logsbyteequal/5actualbinaryprepost+9sourcepins/3inputs16-16-15/
2realreports354-260goldenSHA+literalcurrentfields/6newnames522→528/priornames/forbiddenabsence/PIDgone/
normal24797unchanged. SHA-pinnedreadback дополнительно повторил unique/prefix/logs/14pins/reports/
names/absence/normalDLL. После явного retrospectiveV31Rootgrant запуск выполнен один раз;
после postchecks/idleack/rootrequestfalse/RootALLUIpaused atomicV32active возвращён retrospective
ready-заявке, обе сессии уведомлены. NoRootdesktop/CUA API. Tasks78/79 закрыты,
final evidence/docsreviewACCEPT0Critical/0Important/0Minor. Два documentary locator Minor исправлены;
125/prefix/logs/14pins/inputsoutputs/528recursive names/absence/PID/queue/normalDLL и прежние source/
joint границы независимо сверены. 130Ruling17DeferredMinor сохранены. Product source послеb5 неизменён.

## Продолжение80–82

Producta5a1b9b, doc уточнение24bf329: TwoPhaseTransitionSequence50строк/9входов/двеliveгруппы,
System-only/безWinUI/Composition/доменов. Одинconsumer; UtilityButtonControls536→500, XAML/DP/
навигация/анимации/создание-отменаCTS и literal cleanup callback прежние. Две Count reads до операций,
captured total только дляпауз, inspect-beforeapply, selector-afterapply, lazyToken даже при0duration,
нетearlycancel/snapshots/Concat. Directprivate3paramTaskforward не добавляетawait; helpercatchanyOCE
тела/finallyonFinally, исключение финализатора снаружи catch. Caller-owned callback refchecks/Dispose,
helper самCTSнеCancel/Dispose. Перемещение private async attributes/stackframes вAPIscopeнеобещается.

AbsenthelperCS0103RED×2, inspect-afterapplymutantFAIL1→originalbytesSHA66FB2469…restored;
2026full11с/GUI0-0/52,89с/API451(604)0loss/compiled452(0+7)/focused30/51msGREEN.
Productreview82ACCEPT0Critical/Important/Minor; finalruntime/docreview82 такжеACCEPT0/0/0. Normal2F0BD022… полныйSHASTATUS.
Coordinatorstandalone12/12+expected11/12negativecontrol включаяqueuedSyncContext/cleanupbeforeobserver,
samehelper/raw66FB…; RootпрочёлTRANSITION-PROBE-RESULT.json, не подменяетactualcontrolproof.
Immutable604GUIsourcesmatchworkingnormalizedLF. Jointa5+EE2238/2238/44с/GUI0-0/85,38с,
independent1540paths/1539Gitblobs/175overlays/onlysharedcsproj+110exactGUIitems.

Actual129 F:/cdpiui-verify/cdpiui-transition-sequence-a5a1b9b-20261110:build0-0/70,42с,
1515repo1243project/exact19cuts26bootstraps/24oldcasebyteequal/15actualpins, prelaunchREADY×2.
New4groupsrealunloadedcontrolconstructor/compiledXAML/Buttons/Visibility/Items/privateTask/Task.Delay;
readonlyUISettings, noLoaded/Composition/navigation/help/settings/backend/desktop/newIO/cuts.
Owned pendingTask state machine inspection on pinned.NET8 verifiesdirecthelper/controlpredicate/source/
4literaldurations, no clocktiming/ABIguarantee. Normalphases/finalizer/preCancel/disposedempty-single/
Tokenfaultpartialstate/newerCTSsentinel ownership/liveDPcallback проверяются. Sentinel не доказывает
две параллельные native последовательности; concurrency managed. Одинowncallback гарантирует trace
before reentry без предположения о порядке нескольких callbacks. Timeoutнеподавляетсяexpectedfault.

SelectedfullSHA/pathsSTATUS, все15path+SHA вscratch `transition-control-host-approved-pins.json`.
Runner7D227A02…/readbackA122324B…проверяютсядоwrites/launchиpostrun. Prelaunch1515source/pins/PEasInvoker/
uiAccessfalse/nonadmin/noreparse/522names/freshpaths/MSILstatefields/XBF/ownprocessnone PASS.
ПовторныйguardпротивV39released увиделужеV40retrospectiveactiveиостановилсядоleasewrites/launch;
это coordination gate, нерезультатactualFAIL. После explicitretrospectiveV41Rootgrant запуск один раз.
PID81460 UTC19:40:40.6399817→19:40:46.0595732 10ноября2026:129unique/first125exactorder/
oneCOMPLETE/exit0/noFAIL/empty streams/logsbyteequal/15actualpins/5binaryprepost/3inputs16-16-15/
2realreports354-260goldenSHA+literalfields/522→528recursive6newnames/priornames/forbiddenabsence/
normal2F0Bunchanged/PIDgone. SHA-pinnedreadback повторилunique/prefix/logs/15pins/reports/names/absence/
normalDLL. Послеpostchecks/Rootidleack/requestfalse/ALLUIpaused atomicV42releasedowner:null,
обе сессии уведомлены, noRootdesktop/CUA APIs. Tasks81/82COMPLETE, finalreviewACCEPT0Critical/
0Important/0Minor; independent1515fixture/15pins/129prefix/IO/names/PID/queue/604GUI/joint1540paths/
110XMLitems/175overlays проверены.132Ruling17Minor сохранены, product/source послеa5 неизменён,
no redundant runs for docs.

## Матрица83–85: menu pruning, issue209

Spec: superpowers/specs/2026-11-10-menu-pruning-design.md; basea85/pin174d1cbf5699455d7768ae2398df4f8f241bddab.
Policy exact86417 imported as separatea85/7files; issue/Project actual In progress/P2/Architecture/Low/G2
confirmed before code. Other executor208 owns ConfigMaker/TextRelay/CreateNewConfig/App, untouched here.

| Контракт | Свидетельство | Граница |
| --- | --- | --- |
| Neutrality/postorder/live indices | PostOrderTreePruner23lines, one4port System-only API/oneconsumer; MenuBarController519→502;20tests | Menu claims and registry/physical removal remain in host |
| Child callback changes retention, reverse order/reentry/fault partial state | missing-fileRED→20GREEN; eager-retainmutant5FAIL→exactbyteSHA34B54E72restore→20focused/2046full | No transaction/index repair/null guards added |
| Normal GUI/contracts | GUI0-0/59.24s,BA70952D8A1598847C3CFB42856D2FE0F8AC2110B0CD29A5A09C2C0760E250AB; source451/605/0,meta452/0+7old | Recorded source/metadata shapes, not fullABI |
| Independent product review | Fresh reviewer ACCEPT0Critical/0Important/0Minor on174d1cb | Raw evidence read; reviewer did not rerun builds/tests/UI |
| ExactEE joint |2258/2258/1m25s,GUI0-0/90.99s;1542paths/1541blobs/178overlays/111additiveXMLitems | Isolated snapshot, no merge/security checkout change |
| Native fixture/runtime |133unique/first129/COMPLETE/exit0/allpostchecks;1525repo1244project/19oldcuts/27bootstraps/25oldcasebytes/18pins; build0-0/77.21s; prelaunchREADY | Explicit48Rootgrant; then49peer208handoff afterPIDgone/idleack/requestfalse |

Fixture F:/cdpiui-verify/cdpiui-menu-pruning-174d1cb-20261110. Four actual MenuBar/Frame/controller
checks cover weak nested pruning, strong/weak descendants and protected outcomes, mergedclaims/prefix,
already detached physical root/ignored Remove false/idreuse. Actual object identity checked in logical
lists/physical collections/registry. No Loaded/navigation/activation/backend/desktop/newIO/cuts.
Failure callback/reentry ordering is managed/source evidence, not real WinUI notification execution.
RunnerE47818EA49B87459CC72BAD7CBFAF15F07452F34ECEE21D47B374A83048B60C1 and verifier
56A171078C4AF5AED040D3F4BA3C87F41FE1022BAA02480239340623D244AE83 enforce old129orderedprefix,
133unique/oneCOMPLETE/exit0/18pins/3inputs/2goldenreports/6newnames/absence/process. Full selected
hashes and scope in STATUS; everyactualpath/hash in menu-pruning-host-approved-pins.json.
Actual133 runtime PID68204 UTC20:50:11.5149509Z→20:50:20.9279135+00:00,133unique/first129/
oneCOMPLETE/exit0/noFAIL/streams0/logsbyteequal/18actualpins/5binaryprepost/3inputs16-16-15/
2goldenreports354-260/522→528recursive6newnames/allpriornames/absence/normalBA7095unchanged/PIDgone.
FixtureDLL49EB7BED5E791D9DB359F6CB299B4839BEC34A2B0F3124584CA397AED0A30539; manifest696D1EB3...
Root48explicitgrant→postchecks/idleack/requestfalse/allUIpaused→atomic49readyreviewedpeer208grant.
Earlierfree46guardstoppedonactual47historybeforewrites; prematuregrantmessagecorrectedimmediately.
Additional independent threewayb577+174+EE:2287/2287/GUI0-0/74.70s,1544exactpaths/1543Gitblobs/
177GUIoverlays4Menudelta/112uniqueXMLadds/all14securityitems126total retained, onlycsproj overlap.
No jointnative137/merge/releaseclaim. Final85technicalACCEPT0/0/0; issue209closedcompleted+actualProjectDone/P2ArchitectureLowG2Lumens/3labels readback afterreload52 confirmed.
133Ruling17Minor retained. Physical controller count corrected519→502, prior501transcription
retained with additive ledger/issue correction; source/binaries unchanged.

## Матрица86–88: ConfigurationVariableProjection, issue211

Basec089/source00f39b1a9be70cb97cebd7290b5f24dac6a5ab47, spec2026-11-11-configuration-variable-projection-design.md.
ActualProject55Inprogress/P2ArchitectureLowG2Lumens/2canonicalLabels доimplementation,209Doneverified.

| Контракт | Свидетельство | Граница |
| --- | --- | --- |
| Two typed read-model loops, facade state stays | service443→411/helper45/internalstateless/usedprivateforward; guard/lookup/locks/IO/updates untouched | One consumer/domain adapter, no artificial generic framework |
| Model order/ownership/comparers/nulls |30actualpublicCoretests; original30→mutantFAIL3→exactrestore30; extractedmutantFAIL3→exactrestore30 | Current source dictionary enumeration order, no historical insertion promise |
| Full validation/contracts |2076/11s/0fail-skip;GUI0-0/70.77s; Core88source103→104/compiled88/0loss;605GUIfilesunchanged/GUI4520+7old | Metadata/syntax inventory, not complete ABI |
| ExactEE joint includesCore |2288/43s/GUI0-0/66.19s;1544paths/1543blobs/111XML/14securityitems | Onlycsproj overlap, emptygeneratedSecret onlyextra |
| Peer relay208+loader212+menu+Core+EE |2338/42s/GUI0-0/65.46s;1548paths/1547blobs/113dedupXML/14securityitems | Source/test/build composition, no merge/combined WinUI runtime/release claim |
| Independent review |Final source/joints/currentdocs ACCEPT0Critical0Important0Minor; orderMinor resolved/Ruling135 |211completed/6criteria[x]+actualProject64Done/P2ArchitectureLowG2Lumens/2labelsafterreload |

Selected source/assembly hashes, exactFroots and fullproof paths in STATUS; allCore104input hashes
and bothjoint fullpaths+SHA maps in their retained JSONs. Original/helper mutation restoration exact,
testedCore candidate equals actualtestbin, initializer/lazy metadata/first-match/nullshortcircuit checked.
ActualCoreWindows facade execution is the relevant changed-code proof; no WinUI/binding/startup code
changed, previous133on174 retainedas historical. 135Ruling17oldDeferredMinor remain. Worktree/source
ownership of peer208/212/214 respected; source00 remains unchanged after documentation clarification.

Task88localacceptancecomplete: source00f39/docsf573 finalreview0/0/0→accept6788006950→API211completed
→actualProject64Done/reload/finalmetadatarecord6788179295. RootreadfullsharedJSON+canonicalfields,
parents14/15LIVEopen. NoSource/testchangeafter00; finalclosurecheckpoint is docs-only.
