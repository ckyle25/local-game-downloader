# Local Game Downloader

`Local Game Downloader` is a Playnite desktop plugin for installing portable games from a remote or local manifest file.

The plugin lets you:

- point Playnite at a manifest URL or local `.json` manifest
- browse available games in a searchable catalog
- queue downloads without blocking normal Playnite use
- download archives with progress tracking
- extract archives with progress tracking
- delete the archive after a successful extraction
- import the installed game into Playnite
- fetch Playnite metadata during import
- fall back to the game executable icon when metadata does not provide one

## Requirements

- Playnite Desktop
- 7-Zip

The plugin will try to auto-detect `7z.exe` from:

- `PATH`
- common `Program Files` install locations
- common registry install locations

If auto-detection fails, you can browse to `7z.exe` manually in plugin settings.

## Install

1. Download the latest `.pext` package from the repository releases or from the packaged output.
2. Open the package in Playnite to install it.
3. Restart Playnite if needed.

## First Run

On first launch, the plugin prompts for a manifest source if one is not configured yet.

You can use either:

- an `https://` manifest URL
- a local manifest `.json` file

You can later update this in the plugin settings.

## Manifest Format

The plugin currently expects a manifest shaped like this:

```json
{
  "name": "Catalog Name",
  "downloads": [
    {
      "title": "Example Game.7z",
      "uploadDate": "2026-04-01T12:00:00Z",
      "fileSize": "632.4 MB",
      "uris": [
        "https://example.com/files/Example%20Game.7z"
      ]
    }
  ]
}
```

Notes:

- `title` is used as the archive filename
- the displayed game name is derived from `title`
- the first URI in `uris` is used as the download source

## Usage

After installation, open the plugin from the Playnite left sidebar.

From there you can:

- refresh the manifest
- search the catalog
- queue or install a selected game
- open IGDB or PCGamingWiki search results for a game
- monitor progress in the queue window

When an install finishes successfully, the plugin imports the game into Playnite and attempts to enrich metadata during import.

## Settings

The plugin currently supports:

- `Manifest Source`
- `Default Install Root`
- `7-Zip Path`
- `Delete archive file after successful extraction`

## Development

This project targets:

- `.NET Framework 4.6.2`
- `PlayniteSDK 6.15.0`

Build a release package with:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-package.ps1
```

That script builds the solution and creates a Playnite `.pext` package in `dist/packages`.

## Current Scope

This plugin is currently focused on Playnite Desktop mode. Fullscreen-specific integration is not included in the current release line.
