## v1.3.2: Dialog Positioning & Rendering Fixes
**A small bug-fix release covering dialog centering, a resize crash, and a title bar icon fallback.**
This release fixes update and confirmation dialogs spilling outside a resized overlay, a crash on window resize caused by stale title bar brushes, and a title bar icon that could render as a plain rectangle on some systems.
### Bug Fixes
* **Dialogs**: the update confirmation and download-progress dialogs are now centered on the screen instead of over the overlay's own window, so they no longer spill outside the bounds of a small overlay
* **Startup**: the "update available" dialog now appears before the overlay window is shown, instead of after
* **Resize**: fixed a crash on resize caused by stale title bar connection-dot brushes not being disposed when the render target was recreated
* **Title bar**: fixed the "hide borders" icon rendering as a plain rectangle on some systems by replacing the symbol-font glyph with a vector icon