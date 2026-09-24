# Publishing on GitHub

A Jellyfin plugin repository is one file, `manifest.json`, listing each version
with a download link and an MD5 checksum. This project publishes both through
GitHub: releases hold the zip files, and the workflow in
`.github/workflows/release.yml` builds each release and updates `manifest.json`.

## One-time setup

1. Create an empty **public** repository on github.com, for example
   `jellyfin-plugin-radiobrowser` (no README, no licence, no .gitignore).
2. In the extracted project folder:

   ```
   git init -b main
   git add .
   git commit -m "Internet Radio plugin"
   git remote add origin https://github.com/YOUR-USER/jellyfin-plugin-radiobrowser.git
   git push -u origin main
   ```

3. On GitHub: *Settings → Actions → General → Workflow permissions*: choose
   **Read and write permissions** and save. The workflow needs this to publish
   releases and update `manifest.json`.

## Publishing a version

1. Update the changelog in `Jellyfin.Plugin.RadioBrowser/meta.json` (and
   `build.yaml`), commit and push.
2. Tag and push the tag:

   ```
   git tag v1.0.4
   git push origin v1.0.4
   ```

3. Watch *Actions* on GitHub. After a few minutes there is a release with
   `internet-radio_1.0.4.0.zip`, and `manifest.json` in the repository lists it.

The tag sets the version: the workflow writes it into the DLL and `meta.json`,
so the version numbers in the source files do not need to match.

## Adding the repository to Jellyfin

1. *Dashboard → Plugins → Catalog*, open the repository settings (gear icon),
   add a repository:
   * Name: `Internet Radio`
   * URL: `https://raw.githubusercontent.com/YOUR-USER/jellyfin-plugin-radiobrowser/main/manifest.json`
2. **Stop Jellyfin and delete the manually installed `InternetRadio_*` folder**
   from the plugins directory, then start Jellyfin. Otherwise the plugin would be
   loaded twice. Your settings are kept: they are stored separately in
   `plugins/configurations/Jellyfin.Plugin.RadioBrowser.xml`.
3. Install *Internet Radio* from the catalog and restart Jellyfin.

From then on Jellyfin shows new versions in the catalog and can update the
plugin itself, and the "error while fetching plugin details from the repository"
message no longer appears.
