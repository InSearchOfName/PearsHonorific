# Pear Honorific

A [Dalamud](https://dalamud.dev) plugin for FFXIV that shows the song playing in [Pear Desktop](https://github.com/pear-devs/pear-desktop) (YouTube Music) as your [Honorific](https://github.com/Caraxi/Honorific) title. Inspired by [SpotifyHonorific](https://valiice.github.io/SpotifyHonorific/).

## Installation

Pear Honorific is distributed through a custom Dalamud plugin repository:

1. In-game, open `/xlsettings` and go to the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this URL, click the **+** button, then click the save button:

   ```
   https://github.com/InSearchOfName/PearsHonorific/releases/latest/download/repo.json
   ```

3. Open `/xlplugins`, search for **Pear Honorific** under *All Plugins*, and click **Install**.

Updates are delivered through the same repository whenever a new release is published.

## Setup

1. Install the **Honorific** plugin in-game.
2. In Pear Desktop, enable the built-in **API Server** plugin (Plugins menu). It listens on port `26538` by default.
3. Install Pear Honorific (see [Installation](#installation)) and open `/pearhonorific`.
4. If you changed Pear's API Server port, type it into the **Port** field (it defaults to `26538`; "Reset to default" restores it).
5. Pear shows an Allow/Deny prompt the first time. Allow it, and play something.

## Usage

- `/pearhonorific` opens the settings, `/pearhonorific toggle` enables or disables the plugin.
- The title template uses [Scriban](https://github.com/scriban/scriban). Variables: `song.title`, `song.artist`, `song.album`, `song.duration`, `song.url`, `context.secs_elapsed`.
  - Cycle text: `{{ if context.secs_elapsed % 20 < 10 }}{{ song.title }}{{ else }}{{ song.artist }}{{ end }}`
- Honorific titles are limited to 32 characters. Longer output is truncated, or (with "Scroll titles" on) scrolls left one character at a time like a train station board, with an adjustable speed. Note: Lightless Sync can't update titles as fast as you may set, so scrolling can look choppy to other players.
- Options: prefix, color, glow (optionally a two-color wave or pulse gradient, like Honorific's own), rainbow mode, clear when paused, clear while AFK.
  - The glow options (especially the gradient) are not fully tested yet and may not look right.

## How it works

The plugin connects to Pear's API Server (`POST /auth/{id}` for a token, then the `/api/v1/ws` WebSocket for now-playing events) and sends the rendered title to Honorific through Dalamud IPC (`Honorific.SetCharacterTitle` / `Honorific.ClearCharacterTitle`). No Pear-side plugin is needed.

## Building

Requires the .NET 10 SDK and XIVLauncher/Dalamud installed (or `DALAMUD_HOME` set). Run `dotnet build -c Release`. To test in-game, add the output DLL under `/xlsettings` → Experimental → Dev Plugin Locations.

## License

MIT. See [LICENSE.md](LICENSE.md).
