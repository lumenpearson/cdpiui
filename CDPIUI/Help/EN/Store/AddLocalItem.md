# Add an item from a local package

From version 0.3.0.0, the Store can install and update items from local packages.

### Why is this necessary?

A local installation of items and updates is useful in these conditions:

- The application cannot connect to the Store server to download and update items.
- You want to install an old version of an item. The Store does not have this version.
- The Store does not have the item at this time.
- You want a fast local installation of the application without the Store.

### Is it safe?

All packages have a mandatory certification. The application also verifies the integrity of the package signature. But a signed package can also contain malware.

WARNING: Install a package only if you are sure that it is safe and you trust the provider.

### Which package types are available?

| Package type | Signature | Contents |
| ------------ | --------- | -------- |
| **CDPISIGNEDPACK** | Signed | A standard package. It can contain an add-on, a component or a preset set. |
| **CDPICONFIGPACK** | Not signed | A package for preset sets. It can contain only a preset set. The supported file types for unpacking are limited. |
| **CDPIPATCH** | Signed | An application update package. It contains the files for the application update. |

### How do I install a local package?

Use one of these procedures.

**Procedure 1**

1. Drag the package onto the application shortcut.

**Procedure 2**

1. Right-click the package file.
2. Select [Open with].
3. Select the application executable file, `CDPIUI.exe`.

**Procedure 3**

1. Open the Store.
2. Open the [Settings] page.
3. Select [Add item from local package].
4. Read the warning. To accept the risks, select [Continue anyway].
5. Select the package file.

A dialog opens and asks you to install the item. For a CDPIPATCH package, the application update dialog opens.

1. Select [Install].
2. Wait until the installation is complete.
