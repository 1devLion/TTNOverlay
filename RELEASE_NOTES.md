## v1.7.0: Platform Icons and Automatic Re-login

**The title bar now shows the Twitch and Kick logos next to their connection status, and the app asks you to log in again by itself when a new feature needs new Twitch permissions.**

This release replaces the "T" and "K" letters in the main bar with the real platform logos, fixes the login notice in the moderation panel (it did not wrap on narrow overlays and showed up twice), and adds a check on startup that detects when your Twitch session is missing a permission the app now needs.

### What's new?

* **Platform logos in the title bar**: the Twitch and Kick logos replace the "T" and "K" letters, each followed by its colored connection status dot
* **Theme-aware Twitch logo**: the Twitch logo switches between its light and dark versions with dark/light mode. Kick always uses its colored logo, which reads well on both themes
* **Automatic re-login on new permissions**: on startup the app checks that your Twitch session includes every permission the current version needs. If any is missing, it logs you out and shows a notice with a **Log in** button, instead of failing later with a permission error
* **No false logouts**: if Twitch cannot be reached when the check runs, nothing happens and it is retried on the next launch
* **Future-proof**: new features that need extra Twitch permissions trigger the same prompt automatically, with no manual steps
* **Responsive login notice**: the "log in with the moderator account" message in the moderation panel now wraps to the available width and reflows when you resize the overlay, in all three tabs
* **No more duplicated message**: in the Messages and AutoMod tabs, the login notice no longer appears twice
* **Localized prompt**: the new re-login notice is available in all 8 supported languages
* **Re-login required once**: if your current Twitch session was created before a permission was added, you will see the new prompt the first time you open this version. Log in again and everything keeps working as before