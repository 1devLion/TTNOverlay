(function () {
  "use strict";

  /* =========================================================================
     EDIT THIS: point it at your GitHub repo once it's up.
     Format: "your-username/TTNOverlay"
     ========================================================================= */
  var GITHUB_REPO = "1devLion/TTNOverlay";

  var REPO_LINKS = {
    source: "https://github.com/" + GITHUB_REPO,
    releases: "https://github.com/" + GITHUB_REPO + "/releases",
    release: "https://github.com/" + GITHUB_REPO + "/releases/latest",
    license: "https://github.com/" + GITHUB_REPO + "/blob/main/LICENSE",
  };

  document.querySelectorAll("[data-repo-link]").forEach(function (el) {
    var key = el.getAttribute("data-repo-link");
    if (REPO_LINKS[key]) el.setAttribute("href", REPO_LINKS[key]);
  });

  /* ---------------- i18n ---------------- */
  var SUPPORTED = Object.keys(window.LANG_META || { en: 1 });
  var DEFAULT_LANG = "en";

  function detectLang() {
    var saved = null;
    try { saved = localStorage.getItem("ttno-lang"); } catch (e) {}
    if (saved && SUPPORTED.indexOf(saved) !== -1) return saved;
    return DEFAULT_LANG;
  }

  function applyLang(lang) {
    var dict = window.I18N[lang] || window.I18N[DEFAULT_LANG];
    var fallback = window.I18N[DEFAULT_LANG];

    document.documentElement.setAttribute("lang", lang);

    document.querySelectorAll("[data-i18n]").forEach(function (el) {
      var key = el.getAttribute("data-i18n");
      var value = dict[key] || fallback[key];
      if (value == null) return;
      el.textContent = value;
    });

    var metaDesc = document.querySelector('meta[name="description"]');
    if (metaDesc && dict.meta_description) metaDesc.setAttribute("content", dict.meta_description);

    var themeToggleLabel = document.getElementById("theme-toggle-label");
    if (themeToggleLabel) {
      var isLight = document.documentElement.classList.contains("light");
      themeToggleLabel.textContent = isLight
        ? (dict.theme_toggle_label_light || fallback.theme_toggle_label_light)
        : (dict.theme_toggle_label || fallback.theme_toggle_label);
    }

    try { localStorage.setItem("ttno-lang", lang); } catch (e) {}
  }

  function buildLangSwitch() {
    var select = document.getElementById("lang-select");
    if (!select) return;
    SUPPORTED.forEach(function (code) {
      var opt = document.createElement("option");
      opt.value = code;
      opt.textContent = window.LANG_META[code].name;
      select.appendChild(opt);
    });
    select.value = detectLang();
    select.addEventListener("change", function () {
      applyLang(select.value);
    });
  }

  buildLangSwitch();
  applyLang(detectLang());

  /* ---------------- theme ---------------- */
  function detectTheme() {
    var saved = null;
    try { saved = localStorage.getItem("ttno-theme"); } catch (e) {}
    if (saved === "light" || saved === "dark") return saved;
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
  }

  function applyTheme(theme) {
    document.documentElement.classList.toggle("light", theme === "light");
    var btn = document.getElementById("theme-toggle");
    if (btn) btn.setAttribute("aria-pressed", theme === "light" ? "true" : "false");
    try { localStorage.setItem("ttno-theme", theme); } catch (e) {}

    document.querySelectorAll("[data-theme-src-dark]").forEach(function (img) {
      var src = theme === "light"
        ? img.getAttribute("data-theme-src-light")
        : img.getAttribute("data-theme-src-dark");
      if (src && img.getAttribute("src") !== src) img.setAttribute("src", src);
    });

    var select = document.getElementById("lang-select");
    applyLang(select ? select.value : detectLang());
  }

  applyTheme(detectTheme());

  var themeToggle = document.getElementById("theme-toggle");
  if (themeToggle) {
    themeToggle.addEventListener("click", function () {
      var isLight = document.documentElement.classList.contains("light");
      applyTheme(isLight ? "dark" : "light");
    });
  }

  var navToggle = document.getElementById("nav-toggle");
  var mobileNav = document.getElementById("site-nav-mobile");
  if (navToggle && mobileNav) {
    navToggle.addEventListener("click", function () {
      var open = mobileNav.classList.toggle("open");
      navToggle.setAttribute("aria-expanded", open ? "true" : "false");
    });
    mobileNav.querySelectorAll("a").forEach(function (a) {
      a.addEventListener("click", function () {
        mobileNav.classList.remove("open");
        navToggle.setAttribute("aria-expanded", "false");
      });
    });
  }

  document.querySelectorAll(".shot-media img, .feature-media img, .hero-shot img").forEach(function (img) {
    img.addEventListener("error", function () {
      img.style.display = "none";
      var placeholder = img.parentElement.querySelector(
        ".shot-placeholder, .feature-media-placeholder, .hero-shot-placeholder"
      );
      if (placeholder) placeholder.classList.add("is-visible");
    }, { once: true });
  });

  /* ---------------- copy-to-clipboard buttons ---------------- */
  function fallbackCopy(text) {
    var ta = document.createElement("textarea");
    ta.value = text;
    ta.setAttribute("readonly", "");
    ta.style.position = "absolute";
    ta.style.left = "-9999px";
    document.body.appendChild(ta);
    ta.select();
    try { document.execCommand("copy"); } catch (e) {}
    document.body.removeChild(ta);
  }

  document.querySelectorAll("[data-copy-btn]").forEach(function (btn) {
    var wrap = btn.closest(".code-block-wrap");
    var codeEl = wrap ? wrap.querySelector("code") : null;
    if (!codeEl) return;

    var textSlot = btn.querySelector(".copy-btn-text");
    var resetTimer = null;

    btn.addEventListener("click", function () {
      var text = codeEl.getAttribute("data-copy-text") || codeEl.textContent;

      function showCopied() {
        var lang = document.documentElement.getAttribute("lang") || DEFAULT_LANG;
        var dict = window.I18N[lang] || window.I18N[DEFAULT_LANG];
        btn.classList.add("is-copied");
        if (textSlot) textSlot.textContent = dict.copy_button_copied || window.I18N[DEFAULT_LANG].copy_button_copied;
        clearTimeout(resetTimer);
        resetTimer = setTimeout(function () {
          btn.classList.remove("is-copied");
          if (textSlot) textSlot.textContent = dict.copy_button_label || window.I18N[DEFAULT_LANG].copy_button_label;
        }, 1600);
      }

      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(showCopied, function () {
          fallbackCopy(text);
          showCopied();
        });
      } else {
        fallbackCopy(text);
        showCopied();
      }
    });
  });

  var yearSlot = document.querySelector("[data-year]");
  if (yearSlot) yearSlot.textContent = String(new Date().getFullYear());
})();