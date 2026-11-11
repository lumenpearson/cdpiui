# BlockCheck2: сохранение отчётов

Продолжить аудит больших классов после принятого ff9db65, выделив сохранение JSON/text из
BlockCheck2ResultPageViewModel.Export.cs в internal BlockCheck2ReportExport. Это выбор агента в рамках
разрешённого автономного продолжения; поведение, форматы и backend не меняются.

Конструктор сохраняет семь портов без вызовов: текущий report, picker(title/defaultName/filter),
saveJson(path,report), saveText(path,report), text, show notice и error. Host сохраняет serializer,
публичные SaveJsonReportAsync/SaveTextReportAsync и private SaveAsync(Func<Task>) как forwards.
Копирование в clipboard и все соседние обязанности остаются на месте.
Прежний конструктор host допускает null view, пока операции не требуют view. Сохранить эту
отложенность: picker lambda читает поле только при выборе файла; null report exports с null view безопасны.

SaveJsonAsync/SaveTextAsync повторяют исходные алгоритмы. Null report прекращает работу до любого UI
порта. Затем Text(title), Text(filter), PickSaveFile с точными прежними именами и ключами. Только null
filename означает отмену; empty/whitespace не нормализуются. Guard, локализация picker и сам picker
находятся вне try сохранения. Исключения здесь возвращаются в Task без error notice. Отчёт читается
заново внутри save action после picker; изменения Result/report во время локализации/выбора сохраняются.

SaveAsync catch охватывает saveAction, await, обе success-локализации и ShowActionMessage. Success
сначала локализует BlockCheck2ActionCompletedTitle, затем BlockCheck2FileSavedMessage. Любая ошибка
этой области передаёт exception.Message в ShowActionError. Ошибка error callback выходит наружу с той
же идентичностью; отмена задачи обрабатывается как error. Не добавлять retry, loading guard,
сериализацию параллельных сохранений, новые catch/cancellation или ConfigureAwait. Pending задачи
и повторные входы сохраняют исходный порядок; каждый вызов использует свой выбранный путь.

Проверки: absent-owner RED; literal ключи/порядок, cancel/null/empty filename, live report после picker,
изменения report между text callbacks, pending/concurrent/reentry, setup vs save failure boundaries,
real Canceled task, ошибка success notice и exception identity. В тестах setup faults сам вызов до
Assert.ThrowsAsync должен вернуть Task, а не бросить синхронно. Full managed suite, GUI, прежние
source/metadata inventories. Новый bounded actual115 сохраняет старые111 и добавляет4 группы реальной
Page/Report/Localizer/serializer: guard/cancel/порты, JSON текущего Result, text текущего Result, private
SaveAsync/error boundaries и null report после picker. Настоящий picker не открывается; два новых
report-файла пишутся реальным serializer строго в корень binary directory новой F-копии, сохраняются
и читаются обратно. Третий отрицательный путь должен остаться отсутствующим. Других IO, backend,
manual component lifecycle или desktop UI нет. В postchecks разрешены ровно6 новых имён вместо4:
прежние3inputs+log и2report outputs. Нужны independent prelaunch review, pins всех артефактов/исходников,
эксклюзивная очередь, все postchecks и exact security joint. Старые native/actual копии не запускать.

Не менять настройки, XAML, формат сериализации или security worktree. Все evidence сохраняются;
коммиты локальные, без удаления/reset/push/merge/publish. Проверка композиции с ограниченным report IO
не доказывает full startup, интерактивный picker, visual/XAML поведение или изоляцию всего OS.
