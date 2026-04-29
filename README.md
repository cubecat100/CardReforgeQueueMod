# CardReforgeQueueMod

Card reforge queue DLL mod for `Slay the Spire 2`.

## Project Structure

- `Scripts/`
  - Mod initialization
- `Patches/`
  - Harmony patch entry points
- `mod_manifest.json`
  - STS2 mod metadata

## Build

```powershell
dotnet build .\CardReforgeQueueMod.sln
```

By default, the project references `sts2.dll` at:

```text
References\sts2.dll
```

You can also place `sts2.dll` in the project root or pass `Sts2AssemblyPath` explicitly.

To build without copying to the game mods folder:

```powershell
dotnet build .\CardReforgeQueueMod.sln -p:SkipModDeployment=true
```
