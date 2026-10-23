# Triggers and actions

### Triggers

There are three trigger types:

| Trigger | Function |
| ------- | -------- |
| [Hot key] | Starts the task when you push the selected key combination. |
| [When a process starts] | Starts the task after a process starts. |
| [When a process stops] | Starts the task after a process stops. |

You can enter a process name with the `.exe` extension or without it. A delay makes sure that the process state did not change by accident.

More than one trigger operates with OR logic. Each trigger can start the task.

### Actions

Select an action. Then complete the fields that show. The application hides the fields that are not necessary.

The actions run from top to bottom. To change their sequence, use the arrow buttons.

Example:

1. Add a [Hot key] trigger.
2. Select a key combination.
3. Add the [Start component] action.
4. Select an installed component.
5. Save the task.
6. Select [Run] to test the task.

If [Stop task when an action fails] is selected, the actions after an error do not run.
