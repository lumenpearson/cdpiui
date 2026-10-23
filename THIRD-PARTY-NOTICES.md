**English** | [Русский](THIRD-PARTY-NOTICES.ru.md)

# Third-party notices

CDPI UI is licensed under the Apache License 2.0 (see `LICENSE`). The installer and the portable
build also contain the libraries below, each under its own license. The license texts are
included in the NuGet packages and at the linked project pages.

Components installed later from the Store (GoodbyeDPI, Zapret, ByeDPI, WinDivert, add-ons and
others) are not part of this distribution and keep their own licenses.

| Library | License | Project |
|---|---|---|
| AK.Toolkit.WinUI3.RichTextBlockExtensions | MIT | https://github.com/AndrewKeepCoding/AK.Toolkit |
| Collections.Pooled | MIT | https://github.com/jtmueller/Collections.Pooled |
| CommunityToolkit.WinUI (Animations, Behaviors, Controls, Converters, Extensions, Triggers) | MIT | https://github.com/CommunityToolkit/Windows |
| CommunityToolkit.Labs.WinUI (DataTable, MarkdownTextBlock, Shimmer, TokenView) | MIT | https://github.com/CommunityToolkit/Labs-Windows |
| HtmlAgilityPack | MIT | https://github.com/zzzprojects/html-agility-pack |
| Markdig | BSD-2-Clause | https://github.com/xoofx/markdig |
| Microsoft.Data.Sqlite | MIT | https://github.com/dotnet/efcore |
| Microsoft.Graphics.Win2D | see package license | https://github.com/microsoft/Win2D |
| Microsoft.Toolkit.Uwp.Notifications | MIT | https://github.com/CommunityToolkit/WindowsCommunityToolkit |
| Microsoft.WindowsAppSDK (WinUI 3 and runtime files) | see package `license.txt` | https://github.com/microsoft/WindowsAppSDK |
| Microsoft.Xaml.Behaviors.WinUI.Managed | MIT | https://github.com/microsoft/XamlBehaviors |
| Newtonsoft.Json | MIT | https://github.com/JamesNK/Newtonsoft.Json |
| Semver | MIT | https://github.com/WalkerCodeRanger/semver |
| SQLite (e_sqlite3) | Public domain | https://sqlite.org/copyright.html |
| SQLitePCLRaw | Apache-2.0 | https://github.com/ericsink/SQLitePCL.raw |
| System.Drawing.Common, System.Security.Cryptography.Pkcs, System.ServiceProcess.ServiceController | MIT | https://github.com/dotnet/runtime |
| TaskScheduler | MIT | https://github.com/dahall/taskscheduler |
| TextControlBox.WinUI | MIT | https://github.com/FrozenAssassine/TextControlBox-WinUI |
| Unidecode.NET | MIT | https://github.com/thecoderok/Unidecode.NET |
| WinUI3Localizer | MIT | https://github.com/AndrewKeepCoding/WinUI3Localizer |
| WinUIEx | MIT | https://github.com/dotMorten/WinUIEx |
| XBindLeakFix | MIT | https://www.nuget.org/packages/XBindLeakFix |

Libraries that only come in as dependencies of the packages above (for example `RomanNumerals`)
are listed with their versions in `CDPIUI.deps.json` of a build; their licenses are included in
their NuGet packages.
