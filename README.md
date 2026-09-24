# Internet Radio for Jellyfin

Browse and play internet radio stations from the community directory
[radio-browser.info](https://www.radio-browser.info) inside Jellyfin.

The plugin adds an **Internet Radio** entry to the main menu and to *My Media*,
next to Live TV. Listeners can open these folders:

| Folder         | Contents                                                   |
|----------------|------------------------------------------------------------|
| Favorites      | Stations chosen in the plugin settings (hidden when empty) |
| Local stations | Stations from a configured country (hidden when not set)   |
| Most played    | Most listened stations today                               |
| Top voted      | Stations with the most votes                               |
| Trending       | Stations gaining listeners                                 |
| By country     | One folder per country                                     |
| By genre       | One folder per popular tag                                 |
| By language    | One folder per language                                    |

Stations play as live streams in the Jellyfin web app and the Jellyfin apps for
Android and iOS (see *Playback and apps*). They show the station logo, location,
language, codec and bitrate. Listeners can favourite stations with the normal
heart button, and favourites appear in Jellyfin's Favorites view.

## Requirements

* Jellyfin **12.0** or later (plugin ABI 12.0.0.0, .NET 10)
* Outbound HTTPS access from the server to `*.api.radio-browser.info` and to the station streams

## Installation

There are two ways to install the plugin:

* **A. From your own plugin repository on GitHub** (recommended). Jellyfin installs
  the plugin from its catalog and shows updates there. GitHub builds each release,
  so you do not need .NET on your computer after the one-time setup.
* **B. Manually.** You build the plugin on your computer and copy the files into
  Jellyfin's plugin folder. Every update is repeated by hand.

Both give the same plugin. Use one of them, not both at once: with two copies,
Jellyfin loads the plugin twice.

### A. From your own GitHub repository

1. **One-time setup:** put the project on GitHub and publish the first release,
   as described in [PUBLISHING.md](PUBLISHING.md). After this, your repository
   contains a `manifest.json` that lists the released versions.
2. **Add the repository to Jellyfin:** *Dashboard → Plugins → Catalog*, open the
   repository settings (gear icon) and add:
   * Name: `Internet Radio`
   * URL: `https://raw.githubusercontent.com/YOUR-USER/YOUR-REPOSITORY/main/manifest.json`
3. **Install:** find *Internet Radio* in the catalog, install it and restart Jellyfin.

**Updating:** publish a new version by pushing a tag (see [PUBLISHING.md](PUBLISHING.md)).
Jellyfin then shows the update under *Dashboard → Plugins*; install it and restart.

### B. Manual installation

1. **Install the .NET 10 SDK** on the computer you build on (the runtime alone is
   not enough). On Windows:

   ```
   winget install Microsoft.DotNet.SDK.10
   ```

   Open a new terminal afterwards and check that `dotnet --list-sdks` shows a
   `10.0.` version.

2. **Build.** In the project folder (the one that contains `build.yaml`):

   ```
   dotnet publish Jellyfin.Plugin.RadioBrowser/Jellyfin.Plugin.RadioBrowser.csproj -c Release -o publish
   ```

   The first build downloads the Jellyfin packages from NuGet, so it needs
   internet access.

3. **Copy.** Stop Jellyfin. In Jellyfin's `plugins` folder, create a folder named
   `InternetRadio_<version>` (for example `InternetRadio_1.0.4.0`) and copy these
   three files from `publish` into it:

   * `Jellyfin.Plugin.RadioBrowser.dll`: the plugin
   * `meta.json`: name, version and details shown under *Dashboard → Plugins*
   * `thumb.png`: the plugin image

   The `plugins` folder is in Jellyfin's data directory:

   | Installation | `plugins` folder |
   |--------------|------------------|
   | Linux package | `/var/lib/jellyfin/plugins` |
   | Docker | `/config/plugins` inside the container, i.e. `plugins` in the host folder mapped to `/config` |
   | Windows | `%ProgramData%\Jellyfin\Server\plugins` |

4. **Add.** Delete any older `InternetRadio_*` folders, so only one version is
   loaded, and start Jellyfin. *Dashboard → Plugins* now lists *Internet Radio*
   with its version and image.

   On the plugin's page the dashboard shows "An error occurred while fetching
   plugin details from the repository". This is expected for a manual
   installation: the dashboard looks the plugin up in your plugin repositories and
   does not find it there. The plugin works normally.

**Updating:** repeat steps 2 to 4 with the new version, replacing the old
`InternetRadio_*` folder.

### Switching from manual to repository installation

Stop Jellyfin, delete the `InternetRadio_*` folder from the `plugins` folder,
start Jellyfin and install the plugin from the catalog (method A). Your settings
are kept: Jellyfin stores them separately in
`plugins/configurations/Jellyfin.Plugin.RadioBrowser.xml`.

### After installing

1. Open *Dashboard → Plugins → Internet Radio*, check the settings and click
   **Save** once. This also starts the refresh task (see *Refreshing stations*).
2. Give users who are not administrators access to the channel (see
   *Who can see Internet Radio*).

### Jellyfin 10.11

This version targets Jellyfin 12. The channel APIs are the same in 10.11, so a
10.11 build should only need these changes (not tested):

1. In the `.csproj`: `TargetFramework` `net9.0`, and `Jellyfin.Controller` /
   `Jellyfin.Model` version `10.11.x`.
2. In `meta.json` and `build.yaml`: `targetAbi` `10.11.0.0` (and in `build.yaml`
   `framework: "net9.0"`).
3. For repository releases: `dotnet-version: '9.0.x'` in `.github/workflows/release.yml`.

## Configuration

Open *Dashboard → Plugins → Internet Radio*.

* **Favorite stations**: search the directory by name, add stations and put them in order. They appear in the Favorites folder for every listener. Listeners can also mark stations with the heart button for their own Favorites.
* **Local country code**: a two-letter ISO code (DE, NL, US …) that adds a "Local stations" folder.
* **Stations per folder**, **Genres to list**, **Minimum stations per category**: control list sizes.
* **Show station lists as music albums**: station lists open on Jellyfin's music page (track list with Play and Shuffle) instead of the standard folder page. Station logos are not shown there.
* **Hide offline stations** and **Hide HLS stations**.
* **Report plays**: sends the station id to radio-browser.info when a station starts. The directory operator asks every client to do this so popularity rankings stay accurate.
* **API server**: leave empty for automatic discovery. Set it only if you run a self-hosted mirror.

Saving the settings starts the task *Refresh Internet Radio stations*, which
applies the changes to all stored stations. Station lists are otherwise fetched
again from radio-browser.info when a folder is opened and its cached copy is older
than a few hours.

## Playback and apps

Stations always play as "direct play" with the station's own link. Jellyfin's
transcoder cannot be used for live radio: in Jellyfin 12 the HLS playlist for an
audio item without a duration points to `Audio/{id}/live.m3u8`, a route that only
exists for videos, and the player fails with "fatal player error".

| App | Works | How |
|-----|-------|-----|
| Jellyfin web (browser) | Yes | The web app asks the server for `Audio/{id}/universal`; Jellyfin fetches the station and passes it on. Browsers never load a plain-HTTP station themselves, so HTTPS sites work too. |
| Jellyfin for Android / iOS | Yes | These apps run the web app, and play music the same way. |
| Jellyfin for Android TV / Google TV | No | Its music player only accepts local files (it drops remote media sources), which excludes every radio stream. This is a limit of that app, not of the plugin. |

The user setting *Force transcoding of remote media sources* must be off,
otherwise Jellyfin transcodes every station and playback fails.

## Who can see Internet Radio

Jellyfin decides per user which channels are visible. Administrators see all.
For other users: *Dashboard → Users → user → Access*, then turn on
*Enable access to all channels* or tick *Internet Radio*.

## Refreshing stations

Jellyfin stores a copy of each station per folder and only updates it when that
folder is listed. The scheduled task **Refresh Internet Radio stations** lists every
stored folder at once. It runs automatically after the plugin settings are saved
and can be started from *Dashboard → Scheduled Tasks*.

## Troubleshooting

* **"An error occurred while fetching plugin details from the repository"** on the
  plugin's page in the dashboard: shown for every plugin that was not installed from
  a plugin repository. It is only a warning; the plugin and its settings work.
* **Question-mark icon before "Internet Radio"** in the top bar or side menu: the
  web client picks these icons by library type, and every channel gets the question
  mark. A plugin cannot change it, but Custom CSS can (see *Radio icon in the web
  interface*).
* **Video filters in station lists** (subtitles, trailers …): the web client shows the
  same filter set on every folder page. Turn on *Show station lists as music albums*
  to use the music page instead.
* **Stations still show a `/RadioBrowser/Relay?` link** (stored by versions
  1.0.1–1.0.3): run *Refresh Internet Radio stations* under *Dashboard → Scheduled Tasks*.
  Old relay links keep working for apps that can reach your server's public address.
* **A user does not see Internet Radio**: see *Who can see Internet Radio*.
* **One station fails, others work**: the station may be offline or use an old
  SHOUTcast server that answers with `ICY 200 OK`, which neither browsers nor
  Jellyfin accept. Pick another stream of the same station.

## Radio icon in the web interface (optional)

Add this under *Dashboard → Branding → Custom CSS code* to show a radio icon
instead of the question mark, and on the *My Media* tile instead of the plugin
image. Reload the page afterwards (Ctrl+F5).

`9a1488f3403fa76268359d9d1dd23c05` is the channel's id. Jellyfin derives it from
the channel name, so it is the same on every server with default settings. Check
it by opening *Internet Radio*: the address bar shows `parentId=<id>`. Replace the
id below if yours differs.

```css
/* Top bar and side menu */
a[href*="9a1488f3403fa76268359d9d1dd23c05"] svg.MuiSvgIcon-root path {
    display: none;
}
a[href*="9a1488f3403fa76268359d9d1dd23c05"] svg.MuiSvgIcon-root {
    background-color: currentColor;
    -webkit-mask: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'%3E%3Cpath d='M3.24 6.15C2.51 6.43 2 7.17 2 8v12c0 1.1.89 2 2 2h16c1.11 0 2-.9 2-2V8c0-1.11-.89-2-2-2H8.3l8.26-3.34L15.88 1zM7 20c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3m13-8h-2v-2h-2v2H4V8h16z'/%3E%3C/svg%3E") center / contain no-repeat;
    mask: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'%3E%3Cpath d='M3.24 6.15C2.51 6.43 2 7.17 2 8v12c0 1.1.89 2 2 2h16c1.11 0 2-.9 2-2V8c0-1.11-.89-2-2-2H8.3l8.26-3.34L15.88 1zM7 20c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3m13-8h-2v-2h-2v2H4V8h16z'/%3E%3C/svg%3E") center / contain no-repeat;
}

/* "My Media (small)" buttons on the home screen */
.homeLibraryButton[href*="9a1488f3403fa76268359d9d1dd23c05"] .homeLibraryIcon.quiz::before {
    content: "\e03e";
}

/* "My Media" tile on the home screen */
.card[data-id="9a1488f3403fa76268359d9d1dd23c05"] .cardImageContainer {
    background-image: none !important;
    background-color: #0b2340;
}
.card[data-id="9a1488f3403fa76268359d9d1dd23c05"] .cardImageContainer canvas {
    display: none !important;
}
.card[data-id="9a1488f3403fa76268359d9d1dd23c05"] .cardImageContainer::before {
    font-family: 'Material Icons';
    content: "\e03e";
    font-size: 6rem;
    line-height: 1;
    color: rgba(255, 255, 255, 0.85);
}
```

Custom CSS applies to all users of the web app and the Android/iOS apps, except
users who turned off server CSS in their display settings.

## Why radio-browser.info

* It is free and open source (server under AGPL-3.0), with no API key. It lists tens of thousands of stations.
* It resolves `.pls`/`.m3u` playlists to direct stream URLs (`url_resolved`).
* It provides codec, bitrate, HLS flag, logo, country, language and tags.
* Anyone can run a mirror.

Yamaha MusicCast was also evaluated. Its "Net Radio" directory is **airable.radio**,
which replaced vTuner in 2019. airable is a commercial B2B service: access requires
an NDA and a licence agreement. The MusicCast local API can only tell a Yamaha
device what to play and does not expose stream URLs. Neither can be used as a
source for this plugin.

## How it works

The plugin implements Jellyfin's `IChannel` interface. Jellyfin lists every
registered channel as its own top-level view beside Live TV. It is registered
through `IPluginServiceRegistrator` together with the API client, a hosted
service that reports plays, and a hosted service that starts the task
*Refresh Internet Radio stations* when the settings are saved.

The following details come from the Jellyfin 12.0 source and are handled on purpose:

* **Item ids**: Jellyfin keeps a channel item under exactly one parent folder and deletes items that disappear from a folder. Station item ids are therefore `folderId|stationUuid`, so a station can appear in "Germany" and in "Jazz" at the same time.
* **Stream URL choice**: the station's submitted URL is used rather than the directory's resolved snapshot, unless it is a playlist file. Redirecting services such as StreamTheWorld hand out a different server per listener, and the snapshot can point to one that stopped serving the stream.
* **Direct play only**: Jellyfin 12 cannot transcode live audio to HLS (see *Playback and apps*).
* **Media source ids**: they are `rb_<uuid>`. Jellyfin drops static media sources whose id parses as a GUID but is not a library item.
* **Audio stream info**: each media source declares its audio stream up front. Without it, Jellyfin would try to ffprobe an endless stream before playback.
* **Error handling**: if the directory cannot be reached, the folder request fails instead of returning an empty list. Jellyfin caches channel listings for several hours, and an empty result would hide stations for that whole time.
* **Cache key**: the channel's cache key includes the listing settings, so saving the configuration invalidates cached folders.
* **Play reporting**: plays are reported from `ISessionManager.PlaybackStart` rather than a play-time media-info callback. Combining both makes Jellyfin show duplicate "versions" of a station.

Following the radio-browser.info usage rules, the client:

* discovers servers through DNS (`all.api.radio-browser.info` plus reverse lookups) instead of hard-coding one;
* uses the servers in random order and moves to the next one when a request fails;
* sends the user agent `Jellyfin-RadioBrowser/<version>`.

## Limitations

* **No "now playing" title.** Jellyfin has no hook for ICY stream metadata.
* **Folder names** follow the server's display language (English, Dutch or German).
* **No free-text search for listeners.** Channels cannot receive search input. Use the country, genre and language folders, or ask an administrator to add a station to the Favorites folder.
* **Not in the Live TV guide.** Stations do not appear there. Jellyfin's M3U tuner treats every entry as TV and has no radio flag.

## License

GPL-3.0-or-later, like Jellyfin. Add the licence text as a `LICENSE` file in the
repository (on GitHub: *Add file → Create new file*, name it `LICENSE`, then
*Choose a license template → GNU General Public License v3.0*).
Station data © radio-browser.info contributors.
