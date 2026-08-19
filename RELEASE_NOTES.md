## v1.3.3: Dialog Crash Fix
**A small bug-fix release addressing a random crash affecting dialog windows.**
This release fixes an intermittent crash that could hit any dialog window (update prompts, confirmations, the color picker, GIF preview) while it was open and waiting for user input.
### Bug Fixes
* **Dialogs**: fixed an intermittent crash where dialog windows (update confirmation, download progress, color picker, GIF preview) could be garbage-collected while still open, causing the app to terminate