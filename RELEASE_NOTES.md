## v1.3.1: Release Notes Dialog Crash Fix

**A small patch release fixing a crash introduced in the "what's new" dialog.**

### Bug Fixes

* **Release notes dialog**: fixed a crash (NullReferenceException) that could occur when closing the "what's new" dialog shown after an update — closing it could call `DestroyWindow` a second time on an already-destroyed window handle

* **Release notes dialog**: replaced the text-based scroll hint with the shared scrollbar indicator, matching the rest of the app