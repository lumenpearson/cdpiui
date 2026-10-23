# Review, test and save

On the **Import complete** page, each selected file has a card. Examine the card before you save it.

### Name and component

- If the source file name is not clear, change the name. The component settings show the preset with this name.
- Make sure that the correct component is selected. The list shows only installed components.
- If a yellow compatibility warning shows, select the component for which the preset was made. Ignore the warning only if you know that the arguments are compatible.

### Status and details

| Status | Meaning |
| ------ | ------- |
| **Ready** | The wizard found no problem that prevents the save. |
| **Warnings** | You can save the preset, but you must examine the result. |
| **Failed** | An error prevents the test and the save. |

To see the details and the problem code, select the status. A warning does not always mean that the preset is defective. The **Ready** status does not replace a real test of the necessary sites.

### Test the preset

1. Select the button with the triangle. The selected component starts with the imported arguments.
2. Check the necessary sites.
3. Select the button again to stop the test.

Before the test, the application stops a running instance of this component. After the test, the application does not restore the previous preset. If necessary, start it again in the usual component settings.

CAUTION: Do not use the test as a sandbox. The test starts the real executable file of the installed component.

### Save the preset

- The disk button saves one card.
- **Save all** saves all ready cards in sequence.
- The list removes the cards that were saved. The cards with errors stay in the list. You can open their details.

After the save, select the new preset in the settings of the related component. If the preset has more switches or values, set them before usual operation.

### For advanced users

If you select Zapret2 for a Zapret Legacy preset, the wizard can convert the final `winws` arguments to `winws2`. The wizard cannot convert Zapret2 arguments to Zapret Legacy. The wizard does not convert arguments for other component pairs.

If a problem occurs, see [Errors and limits](cdpiui://Help/ConfigImport/5ConfigImportTroubleshooting/).
