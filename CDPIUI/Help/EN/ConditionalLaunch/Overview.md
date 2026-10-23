# Conditional Launch

Conditional Launch runs the selected actions when you push a hot key or when a process starts or stops. The tasks operate also when the main CDPI UI window is closed.

### Task structure

A task contains triggers and actions.

A trigger starts the task. If a task has more than one trigger, each trigger can start the task.

The actions run in sequence from top to bottom. For example, a task can apply a preset, wait some seconds and start a component.

### Priority

An event can match more than one task. Then only the tasks with the highest priority run. If the matching tasks have the same priority, all of them run.

### Important information

- A disabled task does not start from triggers.
- To test a task manually, select [Run].
- A process trigger can have a delay. If the process goes back to its initial state before the end of the delay, the task does not start.
- To move tasks to a different computer, use `.cdpitask` files.

Next: [Create and edit tasks](cdpiui://Help/ConditionalLaunch/CreatingTasks/).
