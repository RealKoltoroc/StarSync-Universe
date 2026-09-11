# Third-party notices

StarSyncUniverse is an unofficial community project. The project source code is prepared for publication under GPL-3.0-or-later, but third-party libraries, community data, trademarks and game-derived/reference content retain their own licenses and terms.

## Microsoft WebView2

StarSyncUniverse uses the Microsoft WebView2 SDK/runtime to host the interactive HTML/JavaScript map surface.

- Project: https://developer.microsoft.com/microsoft-edge/webview2/
- NuGet package: `Microsoft.Web.WebView2`

The WebView2 SDK/runtime is distributed under Microsoft's own license terms and is not relicensed by StarSyncUniverse.

## SCUnpacked Data

Optional identity/enrichment/validation workflows can consume SCUnpacked Data.

- Repository: https://github.com/StarCitizenWiki/scunpacked-data

SCUnpacked content is not authored by StarSyncUniverse. Review the upstream repository and any applicable game-data/content terms before redistributing refreshed datasets. The public StarSyncUniverse tree only includes the packaged project baseline required for standalone operation and removes local machine paths from its provenance metadata.

## Star Citizen Wiki API

Optional location/media enrichment can use the Star Citizen Wiki API.

- API: https://api.star-citizen.wiki/
- Source: https://github.com/StarCitizenWiki/API

Data and media returned by the service remain subject to the upstream project's terms and the rights of their original owners.

## StarCitizen.Tools

StarCitizen.Tools is used as an optional community reference source for descriptions, media and visual calibration references.

- https://starcitizen.tools/

Content retrieved or referenced from StarCitizen.Tools is not relicensed by StarSyncUniverse.

## StarBreaker

StarSyncUniverse can optionally invoke a separately supplied StarBreaker executable to inspect a local Star Citizen `Data.p4k` installation. StarBreaker is not bundled in this prepared public tree.

Add the canonical public StarBreaker repository/project URL here before publication if one is available:

- **[STARBREAKER_REPOSITORY_URL]**

## Cloud Imperium Games / Roberts Space Industries

Star Citizen, related names, logos, game data, visual references and trademarks are property of their respective owners. StarSyncUniverse is not affiliated with, endorsed by or sponsored by Cloud Imperium Games or Roberts Space Industries.

The source-code license in this repository does not grant rights to third-party trademarks, copyrighted game assets or data beyond rights already granted by their respective owners/terms.

## Project visual assets

Files under `src/StarSyncUniverse/Assets/Branding` are StarSyncUniverse project branding.

Files under `Assets/BodyTextures`, `Assets/CommunityBaseline` and related presentation/data folders may contain project-generated or transformed representations derived from public/community/game-data references. They are provided for this fan-project implementation; do not interpret the GPL source license as a blanket relicensing of underlying Star Citizen intellectual property.

## Dependencies

The project also depends on standard .NET runtime/framework components and NuGet dependencies listed in the project files. Each dependency remains under its upstream license. Use `dotnet list package --include-transitive` before a release if a complete dependency inventory is required.
