# BlockCheck2: состояние выбранной стратегии

Продолжение после закрытого actual-host97 на product839cc952, docs855dbd3. Пользователь поручил
продолжать декомпозицию самостоятельно без подтверждений и без регрессий. Реальные компоненты,
настройки, security/staging/Update и чужие рабочие деревья не менять.

Выделить internal sealed `BlockCheck2ResultSelection : ObservableObject` в существующем каталоге
ViewModels/BlockCheck2. Он владеет пятнадцатью свойствами доступности/деталей выбранной стратегии и
алгоритмом UpdateSelectedStrategy. Host оставляет публичные свойства и точные setter accessibility,
перенаправляет их владельцу и пересылает PropertyChanged с host sender. Нового partial или XAML нет.

Семь ленивых портов: выбранный real BlockCheck2StrategyEvidenceItem; текущий HasSession;
strategyTestRunning; manualTestRunning; CanAddForSiteList(item); Text(key); NotifyMenuStateChanged.
Конструктор только сохраняет порты и исходные default values. Сохраняются все повторные чтения
running/manual, короткое замыкание, захваченный в начале item, порядок setter/локализации/menu и
string.Format с текущей культурой. Null item пропускает HasSession, сбрасывает семь деталей и
локализует выбор; nonnull без session всё равно показывает детали. CanAddForSiteList вызывается до
последнего running guard. Уведомление menu происходит до деталей и может синхронно менять host.
Никакого snapshot, reentry guard, rollback, catch, дедупликации уведомлений или новых валидаций.

Host сохраняет текущие Result/view/поле strategyTestRunning/поле manualTestRunning, все тестовые,
component lifecycle и assignment workflows. Никаких реальных стратегий/компонентов/сети не запускать.
Прежняя native4/d347 проверка сохраняет свой pin; не выдавать её за проверку нового host wiring.

Доказательства: managed linked-source real evidence/model/formatter + controlled side-effect ports;
literal notifications/traces/defaults/flags/culture/reentry/partial exception states. Полный suite,
обычный GUI x64 Release, source/compiled recorded API. Затем новый pinned actual-host стенд со старым97
и real PageViewModel/owner/localizer/session + inert IBlockCheck2ResultPageView; обращаться только к
UpdateSelectedStrategy и свойствам, без OnLoaded/OnUnloaded/Test/Stop/backend. Независимое prelaunch,
эксклюзивная очередь для программного WinUI прогона, postchecks и joint committed security. Финальное
fresh review всего продолжения. Сохранять старые артефакты; результаты имеют ограниченные scopes.
