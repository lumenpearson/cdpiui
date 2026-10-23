# Troubleshoot component exceptions

### Before you start

Find the exception code. The code is usually at the top of the [Pseudoconsole (View process output)] window, in the "Component stopped with an error" message.

| Exception code | Description | Possible fixes |
| -------------- | ----------- | -------------- |
| PARAMETER_ERROR | The component does not support the flag. | Update the component to the latest version.<br>Make sure that the component has a selected preset and that the preset is not empty.<br>If you made the preset manually, read the official documentation of the component. Your platform possibly does not support some of the flags. |
| FILTER_OPEN_ERROR | The WinDivert filter did not start. | Install the component again.<br>Make sure that the component preset has the correct port data.<br>Make sure that no software blocks the traffic control on your PC. |
| HOSTLIST_LOAD_ERROR<br>FILE_READ_ERROR | The site list did not load. | Install the preset set again.<br>Make sure that all site lists and IP lists of the preset are in the preset set folder. |
| PORT_FILTER_ERROR<br>PORT_FILTER_WRONG_VALUE_ERROR | WinDivert cannot find the port. | Make sure that the component preset has the correct port data. |
| COMPONENT_INSTALL_ERROR | The component installation is not correct. | Install the component again. |
| INVALID_VALUE_ERROR | The flag value is not valid. | Update the component to the latest version.<br>If you made the preset manually, read the official documentation of the component. Your platform possibly does not support some of the flags or values. |
| ALREADY_RUNNING_WARN | An instance of WinDivert already operates on this PC. | Stop the WinDivert service.<br>Remove or disable all other bypass tools. |
| ACCESS_DENIED | The component cannot open a file. | Make sure that the application, the component executable file and the WinDivert service have read access and write access to the installation directory. |
