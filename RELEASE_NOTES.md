## v1.6.0: AutoMod Review

**The moderation panel has a new AutoMod tab to allow or reject messages held by AutoMod, with a pending counter and multi-moderator sync.**

This release adds an AutoMod tab to the moderation panel, where you can review messages that AutoMod or blocked terms are holding, and allow or reject them without leaving the overlay. It also keeps the queue in sync across moderators, shows a pending counter and a chat alert, and adds the new permission Twitch requires for AutoMod actions.

### What's new?

* **AutoMod tab**: each held message appears with its time, author, text, reason (category and level, or "blocked term"), and the **Allow** and **Reject** buttons
* **Covers AutoMod and blocked terms**: the tab lists both messages held by AutoMod and messages caught by blocked terms
* **Pending counter**: the tab shows `AutoMod (3)` while there are pending messages, so you can see at a glance if anything needs review
* **Chat alert**: when the queue goes from empty to non-empty, a notice appears in chat, useful if the moderation panel is closed
* **Multi-moderator sync**: if another moderator resolves a message, or it expires, it disappears from your list automatically
* **Works for any moderator**: the AutoMod tab is available to any moderator, not only the channel owner. The queue holds up to 50 messages
* **Localized status and errors**: status and error messages are available in all 8 supported languages
* **Responsive tabs**: with three tabs, the tab bar wraps to a second row on narrow overlays so it does not get cut off
* **Re-login required for AutoMod actions**: Twitch requires the new `moderator:manage:automod` permission. Log out and log in with Twitch once more from Settings or the moderation panel. Everything else keeps working without it
