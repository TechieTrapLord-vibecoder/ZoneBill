// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ─── Sidebar scroll position persistence ─────────────────────────────────────
(function () {
  var STORAGE_KEY = "zb_sidebar_scroll";
  var sidebar = document.getElementById("zbSidebar");
  if (!sidebar) return;

  // The actual scrollable element is .zb-nav (has overflow-y:auto), not .offcanvas-body
  var nav = sidebar.querySelector(".zb-nav");
  if (!nav) return;

  // Restore saved position on load
  var saved = sessionStorage.getItem(STORAGE_KEY);
  if (saved) {
    nav.scrollTop = parseInt(saved, 10);
  }

  // Save on ANY page exit — catches all navigation types
  window.addEventListener("beforeunload", function () {
    sessionStorage.setItem(STORAGE_KEY, nav.scrollTop);
  });
})();
