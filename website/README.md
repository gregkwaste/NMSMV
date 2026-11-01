# NMSMV showcase website

A static Astro landing page for No Man's Model Viewer. The site is isolated from the C# solution and builds to `dist/`.

## Configure links and GitHub Pages

The Download button fetches the latest published GitHub release tag on page load.
To display tested game compatibility, add a plain line to that release's notes:

```text
Game compatibility: Worlds Part II / build 123456
```

Replace the example with the version you actually tested. The compatibility line
is omitted when this metadata is absent; it does not infer compatibility from tags.

Edit `site.config.mjs` to change the repository, release, issue, public-site URL, or Pages base path. The current values target:

- Repository: `https://github.com/gregkwaste/NMSMV`
- Published URL: `https://gregkwaste.github.io/NMSMV/`
- Base path: `/NMSMV/`

If the site is deployed at a custom domain or a user site root, set `basePath` to `/`.

## Add feature clips

Put short, compressed MP4 clips in `public/videos/` with these names:

- `scene-explorer.mp4`
- `materials-lighting.mp4`
- `animation-playback.mp4`

The visual placeholders automatically switch to the real clips when those files are present. Muted clips can be played and paused from the circular control.

## Local development (Windows PowerShell)

```powershell
Set-Location C:\Projects\NMSMV\website
npm install
npm run dev
```

Open the local address Astro prints, normally `http://localhost:4321/NMSMV/`.

To test the production build locally:

```powershell
npm run build
npm run preview
```

Open the printed preview address and include `/NMSMV/` if it is not already present.

## GitHub Pages deployment

The build output is in `dist/` and already uses the `/NMSMV/` asset base. A GitHub Pages workflow can run `npm ci` and `npm run build` with `website` as its working directory, then upload `website/dist` as the Pages artifact.
