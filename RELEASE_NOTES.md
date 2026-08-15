## v1.1.0: Updates, Customization & Fixes

**A smoother and easier-to-update TTNOverlay.**

This release adds automatic updates through Velopack, new Viewer Count customization, improved theme support, and several UI and localization fixes.

### What's new?

- **Automatic updates**: TTNOverlay is now packaged and distributed through Velopack, with automatic update checks and installation
- **Release notes & update dialogs**: built-in confirmation, download progress, and release notes for updates
- **Viewer Count text color**: choose a custom RGB color for the Viewer Count text, or restore the theme default
- **Theme-aware Twitch icons**: separate light and dark Twitch icons are now used automatically based on the active theme
- **Debug Mode**: moved from **General** to **About Us**, with a warning about its performance impact
- **Improved scrolling**: centralized scroll state handling across chat, events, moderation, and settings

### Bug Fixes

- **Text overflow**: fixed UI elements overflowing or overlapping when using longer translations
- **Viewer Count size field**: fixed layout issues with the Viewer Count size input
- **Runtime language changes**: connection status text now updates immediately when changing the language
- **Twitch icon**: fixed the icon appearing incorrectly under certain themes

### Distribution

This release introduces the new packaging and automatic update system, making TTNOverlay easier to install and keep up to date.