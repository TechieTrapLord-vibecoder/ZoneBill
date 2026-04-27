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

// ─── ZoneBill Table Pagination + Search ──────────────────────────────────────
(function () {
  var PAGE_SIZE = 5;
  var BTN_BASE =
    "border:1px solid rgba(6,182,212,0.2);background:var(--zb-primary-alt);color:var(--zb-text-muted);font-size:.78rem;padding:3px 10px;border-radius:5px;cursor:pointer;transition:background .15s;";
  var BTN_ACTIVE =
    "border:1px solid rgba(6,182,212,0.35);background:rgba(6,182,212,0.15);color:var(--zb-secondary);font-size:.78rem;padding:3px 10px;border-radius:5px;cursor:pointer;font-weight:600;";
  var INPUT_STYLE =
    "background:var(--zb-primary-alt);border:1px solid rgba(6,182,212,0.2);border-radius:6px;color:var(--zb-text);font-size:.8rem;padding:5px 10px 5px 30px;width:200px;outline:none;";

  function buildTable(tbody, pageSize) {
    var allRows = Array.from(tbody.querySelectorAll("tr"));
    if (allRows.length === 0) return;
    var tableEl = tbody.closest("table");
    var container = tableEl.closest(".table-responsive") || tableEl;
    var card = container.closest(".zb-table-card");

    // Search input
    var searchWrap = document.createElement("div");
    searchWrap.className = "zb-table-search";
    searchWrap.style.cssText = "position:relative;display:inline-block;";
    var icon = document.createElement("i");
    icon.className = "bi bi-search";
    icon.style.cssText =
      "position:absolute;left:9px;top:50%;transform:translateY(-50%);color:var(--zb-text-muted);font-size:.75rem;pointer-events:none;";
    var input = document.createElement("input");
    input.type = "text";
    input.placeholder = "Search\u2026";
    input.style.cssText = INPUT_STYLE;
    searchWrap.appendChild(icon);
    searchWrap.appendChild(input);

    function findHeaderTarget(cardEl, tableContainer) {
      if (!cardEl) return null;

      var directHeader =
        cardEl.querySelector(":scope > .px-4.pt-3") ||
        cardEl.querySelector(":scope > .card-header") ||
        cardEl.querySelector(":scope > .zb-card-header");
      if (directHeader) return directHeader;

      var prev = tableContainer.previousElementSibling;
      while (prev) {
        if (prev.classList.contains("table-responsive")) break;
        if (
          prev.classList.contains("px-4") ||
          prev.classList.contains("card-header") ||
          prev.classList.contains("zb-card-header")
        ) {
          return prev;
        }
        prev = prev.previousElementSibling;
      }

      return null;
    }

    if (card) {
      var hdr = findHeaderTarget(card, container);
      if (hdr) {
        hdr.style.cssText +=
          "display:flex;align-items:center;justify-content:space-between;gap:10px;";
        if (!hdr.querySelector(".zb-table-search")) hdr.appendChild(searchWrap);
      } else {
        var fallbackHeader = document.createElement("div");
        fallbackHeader.className = "px-4 pt-3 pb-2 border-bottom";
        fallbackHeader.style.cssText =
          "display:flex;align-items:center;justify-content:flex-end;";
        fallbackHeader.appendChild(searchWrap);
        container.insertAdjacentElement("beforebegin", fallbackHeader);
      }
    } else {
      container.insertAdjacentElement("beforebegin", searchWrap);
    }

    // Pagination bar
    var bar = document.createElement("div");
    bar.style.cssText =
      "display:flex;align-items:center;justify-content:space-between;padding:8px 4px 4px;";
    container.insertAdjacentElement("afterend", bar);

    var visibleRows = allRows.slice();

    function render(page) {
      var start = (page - 1) * pageSize;
      var end = start + pageSize;
      var total = visibleRows.length;
      allRows.forEach(function (r) {
        r.style.display = "none";
      });
      visibleRows.forEach(function (r, i) {
        r.style.display = i >= start && i < end ? "" : "none";
      });
      bar.innerHTML = "";
      var info = document.createElement("span");
      info.style.cssText = "color:var(--zb-text-muted);font-size:.78rem;";
      info.textContent =
        total === 0
          ? "No results"
          : "Showing " +
            (start + 1) +
            "\u2013" +
            Math.min(end, total) +
            " of " +
            total;
      bar.appendChild(info);
      var totalPages = Math.ceil(total / pageSize);
      if (totalPages <= 1) return;
      var btns = document.createElement("div");
      btns.style.cssText = "display:flex;gap:4px;flex-wrap:wrap;";
      var prev = document.createElement("button");
      prev.type = "button";
      prev.style.cssText = BTN_BASE;
      prev.innerHTML = "&#8249; Prev";
      if (page === 1) {
        prev.disabled = true;
        prev.style.opacity = "0.4";
      }
      prev.addEventListener("click", function () {
        render(page - 1);
      });
      btns.appendChild(prev);
      var s2 = Math.max(1, page - 2),
        e2 = Math.min(totalPages, s2 + 4);
      s2 = Math.max(1, e2 - 4);
      for (var p = s2; p <= e2; p++) {
        (function (pg) {
          var b = document.createElement("button");
          b.type = "button";
          b.style.cssText = pg === page ? BTN_ACTIVE : BTN_BASE;
          b.textContent = pg;
          b.addEventListener("click", function () {
            render(pg);
          });
          btns.appendChild(b);
        })(p);
      }
      var next = document.createElement("button");
      next.type = "button";
      next.style.cssText = BTN_BASE;
      next.innerHTML = "Next &#8250;";
      if (page === totalPages) {
        next.disabled = true;
        next.style.opacity = "0.4";
      }
      next.addEventListener("click", function () {
        render(page + 1);
      });
      btns.appendChild(next);
      bar.appendChild(btns);
    }

    input.addEventListener("input", function () {
      var q = input.value.toLowerCase().trim();
      visibleRows = allRows.filter(function (r) {
        return !q || r.textContent.toLowerCase().includes(q);
      });
      render(1);
    });

    render(1);
  }

  document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll("tbody[data-paginate]").forEach(function (tbody) {
      var size = parseInt(tbody.getAttribute("data-paginate"), 10) || PAGE_SIZE;
      buildTable(tbody, size);
    });
  });
})();
