# Последовательность двух групп переходов

Продолжение принятого9b65f3a по standing authorization пользователя без подтверждений. Координатор
сверил9worktrees/live sessions: другого активного implementer или tracked-WIP UtilityButtonControls
не найдено. Агент выбирает этот536строчный control вместо вмешательства в Store install/backend
или создания нового большого владельца всех анимаций. Security/persistence policy не меняется.

Самостоятельный нейтральный `Helper/UserExperience/TwoPhaseTransitionSequence` содержит только
механизм двух последовательных групп действий с паузами. System/collections/Task/CancellationToken,
без Button/Visibility/Composition/WinUI/локализатора. Один текущий потребитель, повторное использование
не заявляется. В control остаются составление групп, callbacks/анимации/навигация, настройки, отмена
предыдущей последовательности и тело финализации текущего CTS. Новых partial частей/публичных API нет.

API: static Task RunAsync<TItem,TState>(IReadOnlyList<(TItem Item,TState State)> first,
IReadOnlyList<(TItem Item,TState State)> second, Func<TItem,bool> inspect,
Action<TItem,TState> apply, Func<bool,TimeSpan> firstDelay, Func<bool,TimeSpan> secondDelay,
Func<TimeSpan,CancellationToken,Task> delay, CancellationTokenSource cancellation, Action onFinally).
Конструкторов/состояния нет. Source CTS заимствован: utility не отменяет и не освобождает его.

Обязательная последовательность: first.Count, затем second.Count читаются по одному разу до любых
действий, transitionCount фиксируется. Первый foreach завершает все first, затем foreach second.
Перечисляются сами переданные списки, без ToArray/Concat/new guards. Для каждого элемента inspect
выполняется до apply, bool захватывается; apply предшествует увеличению индекса. Только если index
меньше исходного transitionCount, вызывается соответствующий delay-selector, затем читается
cancellation.Token, затем delay, затем await в текущем контексте. Последний элемент всё ещё вызывает
inspect/apply, но не delay-selector/token/delay. После await модели/списки/inspect остаются live.
Повторный подсчёт count, cache inspect/вычисление delay до apply или снимок списков — изменение поведения.

Никакой ранней проверки отмены: уже canceled CTS не подавляет первое действие и одиночный переход.
Даже disposed CTS допустим для empty/single без Token-read, но faulted Task при реальном ожидании.
Setup/inspect/apply/delay failures возвращаются через Task с прежним исключением. Utility сохраняет
прежний catch(any OperationCanceledException) и finally с вызовом onFinally; никаких новых catch и
ConfigureAwait(false) нет. Non-OCE распространяются, OCE из тела подавляются как прежде; исключение
самого onFinally не перехватывается и может перекрыть прежнюю ошибку, как исходный finally.
Host private RunVisibilitySequenceAsync с прежними тремя параметрами становится прямым Task forward,
без дополнительного await/state machine. OnFinally callback содержит прежнее literal тело: освобождает
переданный CTS всегда, обнуляет поле только при ReferenceEquals текущему CTS; старый завершившийся
запуск не очищает более новый. Cleanup выполняется до завершения того же Task, без нового перехода
между контекстами. Это уточнение агента до product edit: внешний async wrapper мог изменить порядок
queued observers относительно финализации. Fire-and-forget вызов/SetButtonVisibilities не меняются.

Host ports: HasVisibleButtonToTheLeft, Button.Visibility setter; first delay выбирает Reposition или
Hide плюс gap, second — Max(Show,Reposition/2) либо Show плюс gap. Сами selector-выражения выполняются
после apply и только перед нужной паузой. Реальные Task.Delay и lazy CTS.Token остаются прежними.
Max/HasVisible/private forward сохраняют сигнатуры; UISettings и Composition code не переносятся.

TDD: absent-helper RED; plain object/string states, exact trace/count/order/timing/durations,
no-final-delay, mutable-list/current inspect после await, captured count, independent calls,
pre-canceled/disposed CTS, setup and callback fault identity/Task status, borrowed lifetime и onFinally
ровно один раз/до завершения/ошибка поверх прежней. Контрольная
мутация inspect-after-apply или early cancellation должна упасть; исходные bytes/SHA восстановить.
Полный suite/normal GUIx64 Release/source451/compiled452 no losses и7прежних добавлений, размеры/diff.

Actual129=125прежних+4bounded utility-control groups, новая immutable F-копия/25oldbootstraps+1,
прежние19cuts без добавления. Реальные UtilityButtonControls constructor/compiled XAML/standalone
Buttons/Visibility/Items/HasVisible/private forward/Task.Delay. UISettings только readonly
AnimationsEnabled; control не добавляется в окно и не получает Loaded, Composition animations и
навигация не запускаются, help/settings/backend не вызываются. Никаких desktop snapshots/input.
Проверить два этапа/применение/финальныйCTS null+Dispose; canceled и старый/new CTS identity cleanup;
single/empty и отсутствие новых IO. Один собственный pending Task проверяется reflection по
StateMachine пинованного.NET8: это доказывает прямой forward, настоящий control predicate/source и
четыре literal duration значения bound selectors без измерения wall-clock. Это fixture-only зависимость
от compiler/runtime implementation, не стабильный API контракт. Newer CTS в identity case — sentinel
замены без запуска второй новой последовательности; независимые concurrent циклы проверены managed.
Один owned DP callback записывает событие до reentry, порядок нескольких WinUI registrations не
предполагается. При teardown timeout не подавляется как ожидаемая task fault.
Visual appearance/full production startup/loaded animations/
wholeOS/realbackend не заявляются. Все старые125тексты/pins/3inputs/2reports/6newnames/absence/process
gates сохраняются; independent prelaunch READY и явная UI lease перед запуском. Exact security joint,
финальное read-only review с текущими limits. Никаких удалений/rebase/merge/push/чужих worktree правок.
