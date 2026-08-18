## v1.3.0: Multichat, Viewer Counts & a Performance Pass

**Multichat arrives, plus a top-to-bottom performance and reliability pass.**

This release adds Kick as a chat source alongside Twitch, a multi-platform viewer count widget, and per-message platform badges — plus a broad round of performance work on rendering, message history, image/emote caching, and audio alerts, along with several correctness and localization fixes.

### What's new?

* **Multichat**: added Kick as a chat source, so Kick and Twitch chats can now be shown together
* **Platform badges**: each chat message now shows a small badge indicating which platform it came from
* **Connection status**: the title bar now shows connection status as colored dots instead of text
* **Viewer count**: added a multi-platform viewer count widget (Kick + Twitch, with YouTube support ready)

### Performance

* **Render loop**: the render timer now pauses itself when there's nothing to redraw, instead of running continuously in the background
* **Chat rendering**: usernames are now cached per message instead of being rebuilt on every single frame
* **Message history**: trimming old messages no longer slows down as chat history or message limits grow
* **Audio alerts**: alert playback now uses native Windows completion callbacks instead of holding a background thread open for the duration of each sound

### Bug Fixes

* **General section**: fixed content overflowing its box, and added a reusable scrollbar indicator

* **Image/emote loading**: a failed download no longer gets stuck — it's retried instead of staying broken for the rest of the session

* **Message timestamps**: fixed message timestamps and expiry using local time instead of UTC

* **Chat rendering**: text layouts are now explicitly freed when a message is removed, instead of relying on garbage collection

* **Diagnostics**: fixed a debug-only diagnostic dump that was unintentionally running in production builds

* **Settings**: a failed settings save is now logged instead of failing silently

* **Localization**: translated remaining Spanish debug/log strings to English

* **Localization**: the audio output device's default label is now properly localized

### Coming Soon

* **Kick Event Panel**: a dedicated event panel for Kick
* **Kick Moderation Panel**: moderation tools and controls for Kick
* **Streamlabs One-Click Login**: simplified one-click authentication and connection with Streamlabs
* **YouTube Multichat**: chat, event, and moderation panels for YouTube
* **YouTube Viewer Count**: viewer count support for YouTube
