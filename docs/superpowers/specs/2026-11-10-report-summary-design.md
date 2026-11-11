# BlockCheck2: сводка отчёта

После принятого selection103/docs57ede2c выделить ещё одну самостоятельную обязанность большой
BlockCheck2ResultPageViewModel: выбор severity/title/message для статуса отчёта. Internal sealed
BlockCheck2ReportSummaryPresenter в том же каталоге принимает пять ленивых bool-портов HasReport,
NoBypassRequired, Success, IsBestEffort, IsHttpOnly, Func<string,string> text и Action<severity,string,string>
show. Конструктор только сохраняет их; Show повторяет старый ShowReportSummary без новых правил.

Пустой отчёт читает только HasReport, локализует EmptyTitle затем EmptyMessage и показывает Error.
Иначе severity выбирает NoBypass→Informational, Success→Success, остальноеWarning. Заголовок читает
NoBypass/Success/BestEffort заново, затем message ещё раз; HTTP-only читается только внутри NoBypass.
Text может изменить данные между title/message, Show может повторно войти. Это не атомарный snapshot:
все старые чтения и короткое замыкание сохраняются, исключения идут наружу без частичного показа.

Host сохраняет private ShowReportSummary и все остальные методы/свойства/XAML/lifecycle. Порты через
lambda читают текущий Result, Text/ShowActionMessage остаются host-bound. Нет новых model/schema/API,
настроек, настоящих компонентов или UI workflows. Работа inline, пользователь уже разрешил выбор
границ/продолжение без подтверждений; security и чужие worktrees остаются отдельными.

Проверки: absent-owner RED, literal branch keys/precedence/port sequence/reentry/faults; fullsuite/GUI/API.
Новый actual-host107=103старых+4summary groups, realPage/Result/report/Localizer/private forward,
view разрешает только ShowStatus. Новые группы не вызывают UpdateView/OnLoaded/Test/component calls.
Предзапусковое ревью, отдельная UI-очередь, точные артефакты и postchecks, exactsecurityjoint и finalreview.
Сохранять все прошлые pins/копии и их пределы; nativevisual/fullstartup/OS isolation не доказываются.
