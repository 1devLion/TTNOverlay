<div align="center">
  <img src="docs/img/logo.webp" width="96"/>

  # TTNOverlay

  Live chat, event alerts, viewer count, and in-app moderation. Drawn as a transparent, click-through window right on top of whatever you're playing. Chat works with no login at all. Sign in only for viewer count, badges, and moderation.


</div>

<div align="center">
  <img src="docs/img/screenshots/chat_finished.jpg" width="400"/>
  <img src="docs/img/screenshots/events_finished.jpg" width="400"/>
  <br/><br/>
  <img src="docs/img/screenshots/modpanel_finished.jpg" width="400"/>
  <img src="docs/img/screenshots/visualalert_finished.jpg" width="400"/>
</div>

## Stack

- .NET 10, Win32 (P/Invoke) + **Vortice.Direct2D1** for a layered, GPU-drawn window.
- Twitch **IRC over WebSocket** for chat (anonymous, no login required)
- Twitch **Helix API** + user OAuth for viewer count, badges, and moderation
- **Streamlabs Socket API** (optional) for donations, follows, hosts, and merch
- Cloudflare Worker as OAuth token broker (keeps the Twitch client secret off the client)

## Features

- **Chat**: Twitch emotes, badges, and third-party emotes from BTTV, FFZ, and 7TV
- **Event alerts**: subs, resubs, raids, and announcements straight from IRC. Donations, follows, hosts, and merch from Streamlabs if you connect it. Events reported by both sources get merged, not shown twice
- **In-app moderation**: timeout, ban, warn, and unban without ever tabbing out of your game (needs a moderator or broadcaster login)
- **Viewer count & badges**: shown once you sign in through the Twitch Helix API
- **Sound & flash alerts**: every event type gets its own color and can trigger a sound or a screen flash
- **Custom alert colors**: pick any RGB for each visual alert
- **Custom event colors**: color the event box per event type *and* per source (Streamlabs vs IRC)
- **Custom event GIFs**: swap in your own GIF for any event type, right from Settings
- **Custom alert sounds**: default presets included, or bring your own
- **Streamlabs integration**: unlock its alert box events (donations, custom messages, GIFs) via Widget Token + Socket API Token. One-click login coming soon
- **Dark/Light theme**, switchable live
- Multi-language support **(EN/ES/PT/DE/FR/JA/ZH/RU)**

---

### Get it

A portable `.exe` available to download in Releases or, if you want to build it yourself (Needs the .NET 10 SDK): 

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Structure

```
Overlay/            native windows: main overlay, settings, moderation, rendering, controls
Twitch/             IRC client, Helix API, OAuth
Streamlabs/         Socket API client + event mapping
Services/           settings, moderation, theming, localization, caching, audio, logging
Models/             chat message and color models
Native/             Win32 interop (hotkeys)
```

MIT licensed. If you like it, [buy me a coffee on Ko-fi](https://ko-fi.com/1devlion/donate) ☕