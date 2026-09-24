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

Stations play as live streams in every Jellyfin client. They show the station
logo, location, language, codec and bitrate. Listeners can favourite stations
with the normal heart button, and favourites appear in Jellyfin's Favorites view.

## Requirements

* Jellyfin **12.0** or later (plugin ABI 12.0.0.0, .NET 10)
* Outbound HTTPS access from the server to `*.api.radio-browser.info` and to the station streams

## Building and installing

```bash
dotnet publish Jellyfin.Plugin.RadioBrowser/Jellyfin.Plugin.RadioBrowser.csproj -c Release -o publish
```

Copy these three files from `publish` into a folder named `InternetRadio_1.0.4.0`
inside your server's `plugins` directory, then restart Jellyfin:

* `Jellyfin.Plugin.RadioBrowser.dll`
* `meta.json` (name, version and image shown under Dashboard → Plugins)
* `thumb.png` (the plugin image)

Remove older `InternetRadio_*` folders so only one version is loaded.
The default `plugins` location is:

* Linux: `/var/lib/jellyfin/plugins`
* Docker: `/config/plugins`
* Windows: `%ProgramData%\Jellyfin\Server\plugins`

To publish through a plugin repository, package it with
[jprm](https://github.com/oddstr13/jellyfin-plugin-repository-manager) using `build.yaml`.

### Jellyfin 10.11

The channel APIs are the same in 10.11. To build for 10.11:

1. In the `.csproj`, set `TargetFramework` to `net9.0` and use `Jellyfin.Controller`/`Jellyfin.Model` `10.11.x`.
2. In `build.yaml`, set `targetAbi: "10.11.0.0"` and `framework: "net9.0"`.

## Publishing

See [PUBLISHING.md](PUBLISHING.md) to publish the plugin as your own Jellyfin plugin
repository on GitHub, so servers can install and update it from the catalog.

## Configuration

Open *Dashboard → Plugins → Internet Radio*.

* **Favorite stations**: search the directory by name, add stations and put them in order. They appear in the Favorites folder for every listener. Listeners can also mark stations with the heart button for their own Favorites.
* **Local country code**: a two-letter ISO code (DE, NL, US …) that adds a "Local stations" folder.
* **Stations per folder**, **Genres to list**, **Minimum stations per category**: control list sizes.
* **Show station lists as music albums**: station lists open on Jellyfin's music page (track list with Play and Shuffle) instead of the standard folder page. Station logos are not shown there.
* **Hide offline stations** and **Hide HLS stations**.
* **Report plays**: sends the station id to radio-browser.info when a station starts. The directory operator asks every client to do this so popularity rankings stay accurate.
* **API server**: leave empty for automatic discovery. Set it only if you run a self-hosted mirror.

Listing changes appear the next time a folder is opened. Jellyfin also refreshes
channel folders on its own every few hours.

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
* **Question-mark icon in the side menu**: the web client picks menu icons by library
  type, and every channel gets that icon. Plugins cannot change it. The home screen
  tile uses the plugin's own image; if it still shows the icon, run *Dashboard →
  Scheduled Tasks → Refresh Channels*.
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
through `IPluginServiceRegistrator` together with the API client and a hosted
service that reports plays.

The following details come from the Jellyfin 12.0 source and are handled on purpose:

* **Item ids**: Jellyfin keeps a channel item under exactly one parent folder and deletes items that disappear from a folder. Station item ids are therefore `folderId|stationUuid`, so a station can appear in "Germany" and in "Jazz" at the same time.
* **Stream URL choice**: the station's submitted URL is used rather than the directory's resolved snapshot, unless it is a playlist file. Redirecting services such as StreamTheWorld hand out a different server per listener, and the snapshot can point to one that stopped serving the stream.
* **Direct play only**: Jellyfin 12 cannot transcode live audio to HLS (see *Playback and HTTPS*).
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
* **No free-text search for listeners.** Channels cannot receive search input. Use the country, genre and language folders, or ask an administrator to feature a station.
* **Not in the Live TV guide.** Stations do not appear there. Jellyfin's M3U tuner treats every entry as TV and has no radio flag.

## License

GPL-3.0-or-later, like Jellyfin. Station data © radio-browser.info contributors.
