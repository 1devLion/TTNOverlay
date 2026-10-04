/* Runs in <head> before the stylesheet so the page never flashes the wrong theme.
   Same logic as detectTheme() in main.js. */
(function () {
  var theme = null;
  try { theme = localStorage.getItem("ttno-theme"); } catch (e) {}
  if (theme !== "light" && theme !== "dark") {
    theme = window.matchMedia && window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
  }
  if (theme === "light") document.documentElement.classList.add("light");
})();
