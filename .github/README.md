# CI and releases

The workflows are in `.github/workflows`. The scripts that they run are in `.github/scripts`.
All jobs run on `windows-latest`. [CONTRIBUTING.md](CONTRIBUTING.md) tells how to build locally.

## CI (`workflows/ci.yml`)

CI runs on pushes to `main`, `dev` and `fix/**`, and on pull requests. It has read-only
`contents` permission. A new run for the same ref cancels the current run.

| Job | Steps |
|-----|-------|
| `libraries` | Installs the .NET 8 SDK. Runs `create_secret_stub.ps1`. Builds `CDPIUI.Shared`, `CDPIUI.Core` (x64), `CDPIUI.AddOns` and `CDPIUI.TrayIcon` (x64) in Release. Runs `dotnet test CDPIUI.Tests`. |
| `gui` | Runs `build_portable.ps1` and `check_setup_files.ps1`. Builds the MSI. Uploads `NewSetup/bin/**/*.msi` as the `setup-msi` artifact for 7 days. |
| `tools` | Builds `Update/Update.csproj` (with packages.config restore) and `EditHostFile/EditHostFile.csproj` in Release with `msbuild`. |

The `gui` job has `continue-on-error`. The WinUI build needs the CommunityToolkit Labs feed and
the Windows App SDK tools. The job gives only information until the build is reproducible on
hosted runners.

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
| `STORE_GITHUB_TOKEN`, `STORE_GITLAB_TOKEN` | The Store tokens in `Secret.cs` (`GitHubToken`, `GitLabToken`). |
| `STORE_PROXY_URL`, `STORE_PROXY_KEY` | The Store reverse proxy in `Secret.cs` (`ProxyURL`, `ProxyKey`). |
| `TRUSTED_CERTIFICATES` | The SHA-1 thumbprints for `Secret.TrustedCertificates`, separated by spaces, commas or semicolons. |
| `CATALOG_CERTIFICATE_PFX_BASE64` | The PFX file of the catalog signature certificate, in Base64. The patch step writes it to `RUNNER_TEMP\catalog.pfx` and deletes it after the step. |
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

### Output

The workflow makes the artifact `release-<version>`. It contains these files:

- `<version>_patch.cdpipatch`
- `_portable.zip`
- `Setup.msi`

The workflow does not make a GitHub release.

## Scripts (`scripts/`)

### `build_portable.ps1`

Parameter: `-Configuration` (default `Release`). The script requires `msbuild` and the .NET 8 SDK.
The script does these steps:

1. Run `create_secret_stub.ps1`.
2. Delete the old publish folder `CDPIUI/bin/x64/<Configuration>/net8.0-windows10.0.22621.0/publish`.
3. Publish `CDPIUI/CDPIUI.csproj` to that folder (x64, `win-x64`, framework-dependent).
4. Remove the language folders that contain only `.mui` files, except `en-us` and `ru-ru`. The Windows App SDK copies approximately 90 languages.
5. Publish `CDPIUI.TrayIcon` as a single file to `artifacts/tools/tray`.
6. Build `Update` (x64) and `EditHostFile`.
7. Copy `CDPIUI_TrayIcon.exe`, `Update.exe` and `EditHostFile.exe` into the publish folder.

### `check_setup_files.ps1`

This script has no parameters. It compares two lists without case sensitivity:

- the files in the Release publish folder, without `*.pdb` files;
- the `Source="$(var.SourceDir)\..."` entries in `NewSetup/NewSetupFiles.wxs`.

The script fails if a file is missing from one of the lists.
[NewSetup/README.md](../NewSetup/README.md) gives the file list rule.

### `create_secret_stub.ps1`

This script has no parameters. Git ignores `CDPIUI.Shared/Secrets/Secret.cs`. The script does
these steps:

- If the file does not exist, the script writes a `Secret` class with empty tokens, an empty
  `TrustedCertificates` list and a random `AuthGuid`. With this class, the solution compiles.
- If the file exists and its `AuthGuid` is empty, the script writes a random `AuthGuid` into it.
- The script does not change other values of an existing file.

The pipe rejects all clients when `AuthGuid` is empty. Thus the stub must have a value.

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
- Each build gets a random `AuthGuid`.
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
   4. If the installed version is the same or newer, skip the required patch.
   5. Verify the catalog of the `CDPIUI/` folder of the required patch.
6. Sort the required patches by version, the oldest first. The sequence in `patch_urls` is not important.
7. Write a new `patch.cdpipatch` with one layer for each patch (`layers/0/`, `layers/1/`, ...).
   The main patch is the last layer. Each layer keeps its own `catalog.cat`.
8. Delete the `Patches` folder.

When the Store operation completes without errors, the GUI sends the `BeginApplicationUpdate`
message to the tray (`CDPIUI.Core/Features/ApplicationUpdate.cs`). For an MSI installation, the
file is `patch.msi`. The tray installs it with `msiexec /i ... /qn+`.

### Tray side

`CDPIUI.TrayIcon/Helper/Utils.cs` and `CDPIUI.TrayIcon/Helper/PackageStaging.cs` do these steps:

1. Check the path with `TrustedPaths.IsAllowed`.
2. Copy the file to `<install>\.staging\<GUID>\`. In a per-machine installation, only administrators can write to this folder. Thus nobody can replace the file between the check and the installation.
3. Extract the copy to `verify\`.
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

The verifier runs `Test-FileCatalog` in `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`.
It gives the paths through environment variables.
