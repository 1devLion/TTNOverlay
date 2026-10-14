## v1.8.0: Kick Chat, AutoMod Review, User Message History and Quick Replies

**You can now log in with Kick, review the messages held by AutoMod, write to Twitch, Kick or both from the main chat, and look up everything a specific user has said, including every link sent.**

This release brings Kick up to the level of Twitch in the moderation panel (login, sanctions, sending messages), adds an AutoMod tab to review held messages, reworks the Messages tab around specific users, makes links clickable, and fixes Kick chat after Kick retired the connection the app used. It also gives the whole interface a more consistent look.

### What's new?

* **AutoMod review**: a new AutoMod tab in the moderation panel lists the messages Twitch's AutoMod is holding, with the reason (a blocked term, or the category and level), and **Allow** / **Deny** buttons. The tab title shows how many messages are waiting, for example "AutoMod (3)"
* **AutoMod stays in sync**: held messages show up as they arrive, and if another moderator handles one first, you get a notice that it was already handled instead of an error
* **AutoMod in the events view**: a heads-up appears when AutoMod holds a message, so you notice it even with the panel closed
* **Twitch only**: AutoMod is a Twitch feature, so the tab is hidden when the panel is set to Kick only
* **Log in with Kick**: sign in from Settings or the moderation panel. The button follows Kick's own look (gray box in dark mode, white in light mode), and the connected account reads "Connected as ..." the same way for Twitch and Kick
* **Write in the chat**: a message box at the bottom of the main chat sends to Twitch, Kick or both at once (Multichat). It only shows while the title bar is visible, so it never gets in the way with click-through on
* **Kick sanctions**: the Sanctions tab now lists bans and timeouts on Kick, with unban, and clears itself when you change channel
* **Messages by user**: the Messages tab now works on specific users. Search by nick, or turn on **Links** to see every message sent with a link, from everyone or from one user
* **Click a name to see their messages**: clicking a user's name in the main chat opens the Messages tab already filtered by that user
* **Clickable links**: links show in blue and underlined and open with Ctrl + Click, with a hint on hover. Very long links now wrap instead of overflowing the window
* **Animated notices**: copy, ban, mute, delete, AutoMod decisions and similar actions show a small notice that slides up and fades out after 3 seconds, in a plain black/white style that follows the theme. Errors are marked with an X
* **Title bar tooltips**: Settings, Hide bar (click-through) and Close now explain themselves on hover, in all 8 languages
* **Hover everywhere**: tabs, buttons, list rows and text fields in the moderation panel light up under the mouse
* **Smarter Save button**: in Settings, Save stays gray until something changes, and it lights up on hover
* **Consistent look**: every box now has square corners, and "Chat settings" is labeled as Twitch-only, since Kick has no public API for those options
* **Kick chat connection fixed**: Kick retired the connection the app used to read its chat. The app now connects through Kick's new realtime service
* **Moderation panel fix**: reopening the panel after visiting the Messages tab could leave it blank or close the app
* **Missing translations**: the message box texts were missing in German, French, Japanese, Portuguese, Chinese and Russian, and are now added
* **Cleaner exit**: the debug log is deleted when you close the app unless debug mode is on. Chat data is never written to disk
* **Re-login required once**: if your Twitch session was created before AutoMod review was added, or your Kick session before sending messages was added, log out and in again once to get the new permission