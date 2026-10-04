## v1.5.0: Message Moderation

**The moderation panel has a new Messages tab to review recent chat and act on it, plus a fix for the startup update prompt.**

This release adds a Messages tab to the moderation panel, where you can see what was written in the last few minutes and delete messages or sanction their authors without leaving the overlay. It also keeps the moderation panel in sync with Twitch, and fixes the overlay appearing over the update prompt at startup.

### What's new?

* **Messages tab**: the moderation panel now lists recent Twitch chat messages for a time window you choose (last 1, 5, 10, 30 or 60 minutes), with a log capped in size so it stays light on memory
* **Actions on a message**: delete the message, mute, ban or warn its author, delete the message and apply the sanction in a single step, or delete all of a user's messages in the selected window
* **Live status**: rows are dimmed and tagged `[deleted]`, `[muted]` or `[banned]` as soon as they are deleted or sanctioned from anywhere (the app, Twitch's web chat, or another moderator)
* **Re-login required to delete messages**: to delete messages, log out and log in with Twitch once more from Settings or the moderation panel, since Twitch requires a new permission. Everything else keeps working without it
* **Chat modes in sync**: slow mode, subscribers-only, emote-only, followers-only and unique chat now update in the panel's checkboxes when someone changes them on Twitch, instead of only being read when the panel opens
* **Banned users list**: the separator between the name and the ban duration is now a centered dot (`·`) instead of a dash

### Bug Fixes

* **Update prompt**: the overlay no longer shows up while the update prompt is still open at startup. It now waits for your answer, and is shown right away if you decline or the update fails
* **Twitch login/logout sync**: logging in or out from the moderation panel while the Settings window is open (or the other way around) is now reflected in both, and no longer restores a session you just closed
* **Unbanned users**: users you unban no longer keep their `[banned]` tag in the Messages tab. The tag is also cleared when they write again or when they no longer appear in the banned list