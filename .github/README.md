# CI and releases

Workflows are in `.github/workflows`, the scripts they run in `.github/scripts`. All jobs run on
`windows-latest`. Building locally is described in [CONTRIBUTING.md](CONTRIBUTING.md).

## CI (`workflows/ci.yml`)

Runs on pushes to `main`, `dev` and `fix/**` and on pull requests, with read-only `contents`
permission. A newer run for the same ref cancels the running one.

| Job | Steps |
|-----|-------|
| `libraries` | .NET 8 SDK, `create_secret_stub.ps1`, Release build of `CDPIUI.Shared`, `CDPIUI.Core` (x64), `CDPIUI.AddOns`, `CDPIUI.TrayIcon` (x64), then `dotnet test CDPIUI.Tests`. |
| `gui` | `build_portable.ps1`, `check_setup_files.ps1`, MSI build, uploads `NewSetup/bin/**/*.msi` as the `setup-msi` artifact (kept 7 days). Marked `continue-on-error`: the WinUI build needs the CommunityToolkit Labs feed and Windows App SDK tooling and is informative until it is reproducible on hosted runners. |
| `tools` | `msbuild` Release builds of `Update/Update.csproj` (with packages.config restore) and `EditHostFile/EditHostFile.csproj`. |

## Release files (`workflows/build_and_patch.yml`)

Started manually (`workflow_dispatch`).

| Input | Default | Meaning |
|-------|---------|---------|
| `version` | empty | Version written to `requirements.json`. Empty: the first `FileVersion` in `CDPIUI/CDPIUI.csproj`. Must match `^\d+(\.\d+){1,3}$`. |
| `previous_repository` | `Storik4pro/cdpiui` | Repository whose release holds the previous `_portable.zip`. |
| `previous_tag` | empty | Release tag of the previous `_portable.zip`. Empty: latest release. `none`: no comparison, the patch contains every file. |
| `patch_urls` | empty | Patches that must be installed first, separated by spaces; written to `patch_urls`. |

| Secret | Use |
|--------|-----|
| `CATALOG_CERTIFICATE_PFX_BASE64` | Base64 PFX with the catalog signing certificate. Written to `RUNNER_TEMP\catalog.pfx` for the step and deleted afterwards. |
| `CATALOG_CERTIFICATE_PASSWORD` | Password of that PFX. |

Without the certificate the workflow still runs, but the catalogs are unsigned and the
application rejects the patch.

Steps: build the portable layout, check the installer file list, build the MSI, resolve the
version, download the previous `_portable.zip` with `gh release download`, run `make_patch.ps1`,
then build the portable archive: the publish folder is copied, `Update.exe` is renamed to
`_Update.exe`, a `catalog.cat` (version 2.0) of the whole folder is created, signed when the
certificate is given, and added to the folder before zipping.

Output: the artifact `release-<version>` containing

- `<version>_patch.cdpipatch`
- `_portable.zip`
- `Setup.msi`

The workflow does not create a GitHub release; the files are uploaded by hand.

## Scripts (`scripts/`)

### `build_portable.ps1`

Parameter: `-Configuration` (default `Release`). Requires `msbuild` and the .NET 8 SDK.

1. Runs `create_secret_stub.ps1`.
2. Deletes and publishes `CDPIUI/CDPIUI.csproj` (x64, `win-x64`, framework-dependent) to
   `CDPIUI/bin/x64/<Configuration>/net8.0-windows10.0.22621.0/publish`.
3. Removes language folders other than `en-us` and `ru-ru` that contain only `.mui` files
   (the Windows App SDK copies about 90 languages).
4. Publishes `CDPIUI.TrayIcon` as a single file to `artifacts/tools/tray`.
5. Builds `Update` (x64) and `EditHostFile`.
6. Copies `CDPIUI_TrayIcon.exe`, `Update.exe` and `EditHostFile.exe` into the publish folder.

### `check_setup_files.ps1`

No parameters. Compares the Release publish folder (without `*.pdb`) with the
`Source="$(var.SourceDir)\..."` entries of `NewSetup/NewSetupFiles.wxs`, case-insensitive, and
fails if a file is missing on either side. See [NewSetup/README.md](../NewSetup/README.md).

### `create_secret_stub.ps1`

No parameters. If `CDPIUI.Shared/Secrets/Secret.cs` does not exist (it is ignored by git), writes
a `Secret` class with empty tokens and an empty `TrustedCertificates` list so the solution
compiles. An existing file is never overwritten.

### `make_patch.ps1`

Requires PowerShell 7 (Windows PowerShell 5.1 may write `\` in ZIP entry names, which the updater
does not read as folders).

| Parameter | Required | Meaning |
|-----------|----------|---------|
| `-CurrentDir` | yes | Publish folder of the new build. |
| `-PreviousZip` | no | Previous `_portable.zip`. Empty: every file goes into the patch. |
| `-Version` | yes | `requirements.json` version, `1.2` to `1.2.3.4`. |
| `-PatchUrls` | no | `requirements.json` `patch_urls`. |
| `-OutPatch` | no | Output file, default `patch.cdpipatch`. |
| `-CertificatePath`, `-CertificatePassword` | no | PFX used to sign the catalog. |
| `-TimestampServer` | no | Passed to `Set-AuthenticodeSignature`. Not used by the workflow. |

A file goes into the patch when it is missing from the previous build or its SHA-256 differs.
`catalog.cat` of the current folder is ignored. `Update.exe` is stored and compared as
`_Update.exe`. The script fails if nothing changed, if signing fails, or if `Test-FileCatalog`
does not report `Valid` for the new catalog.

## `.cdpipatch` format

A ZIP archive:

```
requirements.json     {"patch_urls": [...], "version": "x.y.z.w"}
CDPIUI/catalog.cat    signed catalog (version 2.0) of every other file in CDPIUI/
CDPIUI/...            files that differ from the previous build, at their installed paths
```

`requirements.json` is UTF-8 without BOM (`PatchRequirementsModel` in
`CDPIUI.Core/Store/Application/PatchRequirementsModel.cs`).

## How the application applies a patch

**GUI side** (`CDPIUI.Core/Store/Application/ApplicationUpdateService.cs`). For a single-file
(non-MSI) build the Store places the patch at `<Store items>/<application id>/patch.cdpipatch`.
`GetPatchReadyToInstall` then:

1. extracts it to a `Patches` folder next to it;
2. reads `requirements.json`; an empty `version` is an error;
3. rejects the patch if the running version is equal or newer;
4. verifies `CDPIUI/catalog.cat` against `CDPIUI/`;
5. for each entry of `patch_urls` whose URL path has more than two segments, reads the version
   from the second-to-last segment (the release tag in a GitHub download URL), skips it if the
   running version is equal or newer, otherwise downloads it, extracts its `CDPIUI/` folder and
   verifies its catalog. URLs with fewer segments are skipped;
6. writes a new `patch.cdpipatch` with one layer per patch: `layers/0/`, `layers/1/`, ... with the
   required patches in the reverse order of `patch_urls`, and the main patch as the last layer.
   Each layer keeps its own `catalog.cat`.

When the Store operation finishes without error, the GUI asks the tray to start the update
(`CDPIUI.Core/Features/ApplicationUpdate.cs`, message `BeginApplicationUpdate`). For an MSI build
the file is `patch.msi`, which the tray installs with `msiexec /i ... /qn+` instead.

**Tray side** (`CDPIUI.TrayIcon/Helper/Utils.cs`, `CDPIUI.TrayIcon/Helper/PackageStaging.cs`):

1. checks the path with `TrustedPaths.IsAllowed`;
2. copies the file to `<install>\.staging\<GUID>\` (writable only by administrators in a
   per-machine installation), so it cannot be replaced between the check and the installation;
3. extracts it to `verify\` and checks the catalog of every `layers/*` folder, or of `CDPIUI/`
   when there are no layers; any failure stops the update and shows `UpdateFailure`;
4. starts `Update.exe --directory-to-zip "<staged file>" --destination-directory "<install>"`
   (see [Update/README.md](../Update/README.md)).

`.staging` is deleted on every tray start.

**Catalog check** (`CDPIUI.Shared/Security/CatalogVerifier.cs`): the catalog must decode as a CMS
signed message with at least one signer; every signer certificate's SHA-1 thumbprint must be in
`Secret.TrustedCertificates` and its signature must be valid. The chain is not built (the
certificates are pinned and self-signed). The files are then compared with
`Test-FileCatalog ... -FilesToSkip catalog.cat` in `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`,
which must print `Valid` within 2 minutes. Paths are passed through environment variables.
