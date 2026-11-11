# BlockCheck2: действия ручных назначений

Продолжение принятого72c5554. Пользователь поручил продолжать декомпозицию самостоятельно,
без регрессий и подтверждений. Агент выбирает самостоятельный internal BlockCheck2AssignmentActions
для операций Page над назначениями: add site/list, move, remove, clear, quick fix, result notice.
Оставшиеся presentation flags, private setters, UpdateAssignmentView/Buttons и форматирование preview
сохраняются в Page. Работа не меняет model/editor/preset policy, XAML, тексты и форматы.

Альтернативы: оставить методы в Page сохраняет смешение действий и состояния; перенести одновременно
все assignment properties/notifications расширяет риск reentry и объём. Выбран перенос только
протокола действий, с отдельным узким internal host и private nested Page adapter. Это решение агента,
не новый запрос пользователя. Новых partial частей и public интерфейсов host не будет.

Перед product edit граница уточнена по переданному координирующей сессией критерию: извлекать
нейтральные небольшие механизмы там, где граница действительно существует. Новый
`Helper/UserExperience/SelectedItemChoiceAction` — static generic механизм с четырьмя портами
selected/choices/chooser/apply; TItem/TChoice reference types. Он ничего не знает о Page, BlockCheck2,
WinUI, notices, Result и пресетах. В нём живут capture/snapshot/optional await/null cancel/apply.
AssignmentActions остаётся небольшим доменным orchestration для notices и коротких команд, а не
новым крупным владельцем всей Page. Иные file-replacement workflows не используют этот механизм:
их whitespace/catch/backend контракты отличаются, общее применение потребовало бы смены поведения.

Owner constructor только сохраняет IBlockCheck2AssignmentActionsHost. Интерфейс содержит13members:
SelectedStrategy; GetSiteListPaths(item); ChooseSiteListAsync(paths); AddForSite(item);
AddForSiteList(item,path); SelectedAssignment get/set; MoveAssignment(item,offset);
RemoveAssignment(item); ClearAssignments(); UpdateAssignmentView();
UpdateActionButtons(); Text(key); ShowMessage(severity,title,message).
Все model обращения в adapter читают текущий Page.Result при вызове. Constructor не читает view,
даже null, не запускает manager/component/backend. Public Page методы и private ShowAssignmentResult
сохраняются как forwards. Async list entrypoint возвращает faulted Task для setup faults как прежде.

Add site: один captured SelectedStrategy, null возвращает; AddForSite на current Result, затем
ShowResult(bool). Add list: captured item, current GetSiteListPaths(item).ToArray() до await;
zero paths возвращает, one path без chooser; multiple paths передаются как тот же snapshot array.
Отказ только path==null, whitespace/empty/path вне списка не валидируются заново. После await
AddForSiteList использует captured item и выбранный путь, но текущий Result. Массив для chooser
остаётся изменяемым, первый путь для single path берётся до chooser. Runtime null в single-path
массиве передаётся apply как прежде: null guard находится только внутри multiple-choice ветки.
Текущий потребитель нейтрального механизма один; существующее повторное использование не заявляется.
Никаких locks/guards/dedup/
cancellation/catch/ConfigureAwait(false) не добавляется. Одновременные вызовы независимы.

ShowResult: bool выбирает Success/AssignmentAddedTitle/AssignmentAddedMessage либо
Warning/AssignmentNotAddedTitle/AssignmentDuplicateMessage. Text title читается до message;
ShowMessage вызывается после обоих; UpdateAssignmentView, затем отдельный UpdateActionButtons.
Существующий UpdateAssignmentView уже вызывает UpdateActionButtons: двойной menu update сохраняется.
Исключения локализации/notice/view распространяются прежним экземпляром и обрывают последующие эффекты.

Move: captured SelectedAssignment; при nonnull и MoveAssignment(current Result,selected,offset)==true
сначала UpdateAssignmentView, затем SelectedAssignment=тот же captured object. Изменение selection
или Result из update callback не меняет объект восстановления. Любой offset передаётся без нормализации.
Remove: null guard; RemoveAssignment, затем UpdateAssignmentView даже если model ничего не удалил.
Clear: ClearAssignments, затем UpdateAssignmentView даже на пустой модели. QuickFix принимает Func<bool>
операции: Page closure читает current Result и передаёт captured issue. False возвращает
без UI; true показывает Success/ManualIssueQuickFixCompletedTitle/ManualScopeOverlapAcceptedMessage,
затем UpdateAssignmentView без отдельного UpdateActionButtons. Все live ports/reentry/fault границы прежние.

Проверки нейтрального механизма используют plain object/string вне BlockCheck2. Domain проверки:
absent-owner RED, literal traces/guards, paths snapshot identity, pending selection/model
changes, independent concurrent choices, cancellation/faulted Task timing, observer/localizer reentry,
exception identity/partial effects. Контрольная мутация повторного чтения selected после await должна
упасть. Full suite, normal GUIx64 Release, source451/compiled452 no losses/+7 prior adds, sizes/diff.
Новый bounded actual125=121прежних+4assignment groups: реальная Page/Result/editor/localizer/preview
formatter/private nested adapter; controlled selection/chooser/status/menu. Новых source cuts и IO нет:
19cuts/25bootstrap sources, прежние3inputs/log/2reports=6newbinfiles. Проверяются live model после chooser,
реальные memory assignments/move/remove/clear/quickfix, original private forward и частичные faults.
Independent prelaunch READY, явная согласованная UI lease, exact committed-security joint, final review.
Full startup/настоящие dialogs/component/backend/visual/wholeOS/ABI не объявляются проверенными.

Прежние evidence/копии и чужие worktrees сохраняются. Нет deletion/reset/rebase/merge/push/publication.
