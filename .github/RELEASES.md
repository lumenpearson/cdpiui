**English** | [Русский](RELEASES.ru.md)

# CI and releases

The workflows are in `.github/workflows`. The scripts that they run are in `.github/scripts`.
All jobs run on `windows-latest`. [CONTRIBUTING.md](CONTRIBUTING.md) tells how to build locally.

## CI (`workflows/ci.yml`)

CI runs on pushes to `main`, `dev` and `fix/**`, and on pull requests. It has read-only
`contents` permission. A new run for the same ref cancels the current run.

| Job | Steps |
|-----|-------|
| `secrets` | Runs on Ubuntu. Checks out the full history without a stored token. Downloads gitleaks 8.30.1 and checks the archive against its SHA-256. Scans the files with `.gitleaks.toml`. Scans the commits that the pull request or the push adds (`base..HEAD`); older history is not scanned. A finding fails the job. |
| `libraries` | Installs the .NET 8 SDK. Runs `create_secret_stub.ps1`. Builds `CDPIUI.Shared`, `CDPIUI.Core` (x64), `CDPIUI.AddOns` and `CDPIUI.TrayIcon` (x64) in Release. Runs `dotnet test CDPIUI.Tests`. Restores these four projects, `CDPIUI` and `CDPIUI.Tests` with `NuGetAuditMode=all` and fails if a direct or transitive package has a known vulnerability of moderate or higher severity (warnings NU1902–NU1904 become errors). |
| `gui` | Runs `build_portable.ps1` and `check_setup_files.ps1`. Builds the MSI. Uploads `NewSetup/bin/**/*.msi` as the `setup-msi` artifact for 7 days. |
| `tools` | Installs the .NET 8 SDK. Builds `Update/Update.csproj` and `EditHostFile/EditHostFile.csproj` (x64) in Release with `dotnet build`. Publishes each project as a framework-dependent single file (`win-x64`) to `artifacts/tools/<project>`. Fails if the publish folder contains a file other than the `.exe` file. Builds `CDPIUI.ElevatedLauncher` and publishes it with Native AOT to `artifacts/tools/launcher`, with `TreatWarningsAsErrors`: a trim or AOT warning fails the job. Fails if the folder contains a file other than `CDPIUI_ElevatedLauncher.exe` or if this file is a .NET assembly. |

Each job must pass. The `gui` job blocks the check like the other jobs.

The workflows refer to each action by its full commit SHA. A tag can move to other code; a SHA
cannot. The comment after the SHA gives the version tag.

## Release files (`workflows/build_and_patch.yml`)

A user starts this workflow manually (`workflow_dispatch`).

### Inputs

| Input | Default | Meaning |
|-------|---------|---------|
| `version` | empty | The version for `requirements.json`. If empty, the workflow uses the first `FileVersion` in `CDPIUI/CDPIUI.csproj`. The value must match `^\d+(\.\d+){1,3}$`. |
| `previous_repository` | `Storik4pro/cdpiui` | The repository whose release contains the previous `_portable.zip`. |
| `previous_tag` | empty | The release tag of the previous `_portable.zip`. If empty, the workflow uses the latest release. With `none`, the workflow makes a full patch. |
| `patch_urls` | empty | Patches that the application must install first, separated by spaces. The workflow writes them to `patch_urls`. |

### Secrets

| Secret | Use |
|--------|-----|
| `STORE_GITHUB_TOKEN`, `STORE_GITLAB_TOKEN` | The Store tokens in `Secret.cs` (`GitHubToken`, `GitLabToken`). The application makes them public, so they must not have write access (see `create_release_secret.ps1`). |
| `STORE_PROXY_URL`, `STORE_PROXY_KEY` | The Store reverse proxy in `Secret.cs` (`ProxyURL`, `ProxyKey`). The key is in each copy of the application. Thus the proxy must not trust the key more than an anonymous client: limit the rate and the allowed targets on the proxy. The application does not send the Store tokens to the proxy. Give the proxy a GitHub token of its own. |
| `TRUSTED_CERTIFICATES` | The SHA-1 thumbprints for `Secret.TrustedCertificates`, separated by spaces, commas or semicolons. |
| `CATALOG_CERTIFICATE_PFX_BASE64` | The PFX file of the catalog signature certificate, in Base64. The patch step writes it to `RUNNER_TEMP\catalog.pfx` and deletes it at the end of the step. |
| `CATALOG_CERTIFICATE_PASSWORD` | The password of the PFX file. |

All secrets are optional for the workflow. The "Create Secret.cs" step gives them to
`create_release_secret.ps1` as environment variables. The PFX goes in `CATALOG_CERTIFICATE`.

WARNING: Set `CATALOG_CERTIFICATE_PFX_BASE64` and `CATALOG_CERTIFICATE_PASSWORD` before you make a
release. Without the certificate, the catalogs have no signature and the application rejects the
patch.

WARNING: Make sure that `TrustedCertificates` is not empty in a release build. Set
`TRUSTED_CERTIFICATES`, or set the catalog certificate. With an empty list, the release build
rejects all signed catalogs, also its own patches.

### Steps

1. Write `CDPIUI.Shared/Secrets/Secret.cs` with `create_release_secret.ps1 -Force` (step "Create Secret.cs").
2. Build the portable layout (`build_portable.ps1`). The script keeps the `Secret.cs` file of step 1.
3. Check the installer file list (`check_setup_files.ps1`).
4. Build the MSI.
5. Find the version.
6. Download the previous `_portable.zip` with `gh release download`. The workflow skips this step if `previous_tag` is `none`.
7. Make the patch with `make_patch.ps1`.
8. Make the portable archive:
   1. Copy the publish folder.
   2. Change the name of `Update.exe` to `_Update.exe`.
   3. Make a `catalog.cat` (version 2.0) of the full folder.
   4. Sign the catalog if the certificate is available. If the signature has no signer certificate, the step fails.
   5. Put the catalog in the folder and make the ZIP file.
9. Copy `Setup.msi` and sign it with the catalog certificate (Authenticode, SHA-256). If the signature has no signer certificate, the step fails. Without the certificate, the step shows a warning: an installed copy does not accept an unsigned `Setup.msi` as an update.

### Output

The workflow makes the artifact `release-<version>`. It contains these files:

- `<version>_patch.cdpipatch`
- `_portable.zip`
- `Setup.msi`

The workflow does not make a GitHub release.

## Scripts (`scripts/`)

### `build_portable.ps1`

Parameter: `-Configuration` (default `Release`). The script requires `msbuild` (for the GUI), the .NET 8 SDK and the C++ build tools of Visual Studio (for the Native AOT launcher). The hosted `windows-latest` image has them.
The script does these steps:

1. Run `create_secret_stub.ps1`.
2. Delete the old publish folder `CDPIUI/bin/x64/<Configuration>/net8.0-windows10.0.22621.0/publish`.
3. Publish `CDPIUI/CDPIUI.csproj` to that folder (x64, `win-x64`, framework-dependent).
4. Remove the language folders that contain only `.mui` files, except `en-us` and `ru-ru`. The Windows App SDK copies approximately 90 languages.
5. Publish `CDPIUI.TrayIcon` as a single file to `artifacts/tools/tray`.
6. Publish `Update` and `EditHostFile` as single files (x64, `win-x64`, framework-dependent) to `artifacts/tools/update` and `artifacts/tools/edithostfile`.
7. Publish `CDPIUI.ElevatedLauncher` with Native AOT (x64, `win-x64`) to `artifacts/tools/launcher`. Fail if `CDPIUI_ElevatedLauncher.exe` is missing or is a .NET assembly.
8. Copy `CDPIUI_TrayIcon.exe`, `Update.exe`, `EditHostFile.exe` and `CDPIUI_ElevatedLauncher.exe` into the publish folder.

`Update.exe` replaces the application files while it operates. Thus it must not load a DLL from
the installation folder. The single file contains all the code of the tool.

The GUI and the autorun task start the tray and `EditHostFile.exe` through `CDPIUI_ElevatedLauncher.exe`.
The launcher must have no .NET host. Otherwise the .NET variables of the user profile act on it.
Refer to [CDPIUI.ElevatedLauncher/README.md](../CDPIUI.ElevatedLauncher/README.md).

### `check_setup_files.ps1`

This script has no parameters. It compares two lists without case sensitivity:

- the files in the Release publish folder, without `*.pdb` files;
- the `Source="$(var.SourceDir)\..."` entries in `NewSetup/NewSetupFiles.wxs`.

The script fails if a file is missing from one of the lists.
[NewSetup/README.md](../NewSetup/README.md) gives the file list rule.

### `create_secret_stub.ps1`

This script has no parameters. Git ignores `CDPIUI.Shared/Secrets/Secret.cs`. The script does
these steps:

- If the file does not exist, the script writes a `Secret` class with empty tokens and an empty
  `TrustedCertificates` list. With this class, the solution compiles.
- The script does not change an existing file.

The pipe key is not a build secret. The tray makes a new key at each start (see [Pipe](../CDPIUI.Shared/Pipe/README.md)).

### `create_release_secret.ps1`

The release workflow runs this script. It writes `Secret.cs` from these environment variables:

| Variable | Member |
|----------|--------|
| `STORE_GITHUB_TOKEN`, `STORE_GITLAB_TOKEN` | `GitHubToken`, `GitLabToken` |
| `STORE_PROXY_URL`, `STORE_PROXY_KEY` | `ProxyURL`, `ProxyKey` |
| `TRUSTED_CERTIFICATES` | `TrustedCertificates` |
| `CATALOG_CERTIFICATE`, `CATALOG_CERTIFICATE_PASSWORD` | `TrustedCertificates`, if `TRUSTED_CERTIFICATES` gives no thumbprint |

Rules:

- The script keeps only thumbprints with 40 hexadecimal characters, in upper case.
- If `TRUSTED_CERTIFICATES` gives no thumbprint, the script uses the thumbprint of the PFX in `CATALOG_CERTIFICATE`.
- If there is no thumbprint, the script shows a warning. The build then rejects signed packages and patches.
- The script checks the Store tokens before it writes them, because the application makes them public:
  - A classic GitHub token must have no scopes (`X-OAuth-Scopes` of `https://api.github.com/rate_limit` is empty). A fine-grained token has no scopes in this header; give it only read access to public repositories.
  - A GitLab token can have only `read_api`, `read_repository` and `read_registry` (`/api/v4/personal_access_tokens/self`).
  - If a token has other scopes, or if the script cannot check it, the script fails.
- If `Secret.cs` exists, the script does not change it. With `-Force`, the script replaces it.

### `make_patch.ps1`

The script requires PowerShell 7. Windows PowerShell 5.1 can write `\` in ZIP entry names, and
the updater does not read these names as folders.

| Parameter | Required | Meaning |
|-----------|----------|---------|
| `-CurrentDir` | Yes | The publish folder of the new build. |
| `-PreviousZip` | No | The previous `_portable.zip`. If empty, the patch contains all files. |
| `-Version` | Yes | The version for `requirements.json`, from `1.2` to `1.2.3.4`. |
| `-PatchUrls` | No | The `patch_urls` for `requirements.json`. |
| `-OutPatch` | No | The output file. The default is `patch.cdpipatch`. |
| `-CertificatePath`, `-CertificatePassword` | No | The PFX file that signs the catalog, and its password. |
| `-TimestampServer` | No | The timestamp server for `Set-AuthenticodeSignature`. The workflow does not use it. |

Rules:

- The patch contains a file if the previous build does not have it, or if its SHA-256 hash is different.
- The script ignores `catalog.cat` in the current folder.
- The script stores and compares `Update.exe` as `_Update.exe`.
- The script fails if no file changed.
- The script fails if the signature step fails.
- The script fails if `Test-FileCatalog` does not return `Valid` for the new catalog.

## `.cdpipatch` format

A `.cdpipatch` file is a ZIP archive with this structure:

```
requirements.json     {"patch_urls": [...], "version": "x.y.z.w"}
CDPIUI/catalog.cat    signed catalog (version 2.0) of all other files in CDPIUI/
CDPIUI/...            the changed files, at their installed paths
```

`make_patch.ps1` writes `requirements.json` in UTF-8 without BOM. The application reads it into
`PatchRequirementsModel` (`CDPIUI.Core/Store/Application/PatchRequirementsModel.cs`).

## How the application applies a patch

### GUI side

`CDPIUI.Core/Store/Application/ApplicationUpdateService.cs` prepares the patch. For a portable
installation (`State.IsApplicationBuildAsSingleFile`), the Store puts the patch at
`<Store items>/<application id>/patch.cdpipatch`. `GetPatchReadyToInstall` then does these steps:

1. Extract the patch to `Patches\<patch file name without extension>` in the same folder.
2. Read `requirements.json`. An empty `version` causes an error.
3. Reject the patch if the installed version is the same or newer.
4. Verify `CDPIUI/catalog.cat` against the `CDPIUI/` folder.
5. Process each entry of `patch_urls`. The service skips empty entries.
   1. Download the required patch to `required/<index>` in the patch folder. The index is 0 for the first entry, 1 for the next entry, and so on.
   2. Extract the full archive.
   3. Read the version from the `requirements.json` of the required patch. If the file or the version is not valid, the operation fails.
   4. If the installed version is the same or newer, skip the required patch. Also skip it if its version is the same as or newer than the version of the main patch, because the main patch is always the last layer.
   5. Verify the catalog of the `CDPIUI/` folder of the required patch.
6. Sort the required patches by version, the oldest first. The sequence in `patch_urls` is not important.
7. Write a new `patch.cdpipatch` with one layer for each patch (`layers/0/`, `layers/1/`, ...).
   The main patch is the last layer. Each layer keeps its own `catalog.cat`.
8. Delete the `Patches` folder.

When the Store operation completes without errors, the GUI sends the `BeginApplicationUpdate`
message to the tray (`CDPIUI.Core/Features/ApplicationUpdate.cs`). For an MSI installation, the
file is `patch.msi`. The tray installs it with `msiexec /i ... /qn+` only if it has the Authenticode
signature of a certificate from `Secret.TrustedCertificates` (`PackageStaging.HasPinnedSignature`).

### Tray side

`CDPIUI.TrayIcon/Helper/Utils.cs` and `CDPIUI.TrayIcon/Helper/PackageStaging.cs` do these steps:

1. Check the path with `TrustedPaths.IsAllowed`.
2. Copy the file to `<install>\.staging\<GUID>\`. In a per-machine installation, only administrators can write to this folder. Thus nobody can replace the file between the check and the installation.
3. Check the limits of the archive (`ArchivePaths`: entries, size, compression ratio, links). Then extract the copy to `verify\`.
4. Verify the catalog of each `layers/*` folder. If there are no layers, verify the `CDPIUI/` folder.
5. If a check fails, stop and show the `UpdateFailure` notification.
6. Start `Update.exe --directory-to-zip "<staged file>" --destination-directory "<install>"`.

[Update/README.md](../Update/README.md) describes `Update.exe`. The tray deletes the `.staging`
folder at each start.

`Update.exe` does not copy a `catalog.cat` file from a patch into the installation folder. It also
does not remove files that are not in the patch, because a patch contains only the changed files.

### Catalog check

`CDPIUI.Shared/Security/CatalogVerifier.cs` checks a catalog with these rules:

- The catalog must be a CMS signed message with one or more signers.
- The SHA-1 thumbprint of each signer certificate must be in `Secret.TrustedCertificates`.
- The signature of each signer must be valid.
- The verifier does not build the certificate chain, because the certificates are pinned and self-signed.
- `Test-FileCatalog ... -FilesToSkip catalog.cat` must return `Valid` in a maximum of 2 minutes.
- The verifier does not check revocation or the expiry date. See the certificate rules in [CDPIUI.Shared/README.md](../CDPIUI.Shared/README.md#security).

The verifier runs `Test-FileCatalog` in `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`.
It gives the paths through environment variables.
