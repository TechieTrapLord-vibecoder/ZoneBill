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

// ─── Global Toast Helper ────────────────────────────────────────────────────
(function () {
  function escapeHtml(value) {
    return String(value || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function ensureContainer() {
    var container = document.getElementById("zbToastContainer");
    if (container) return container;

    container = document.createElement("div");
    container.id = "zbToastContainer";
    container.className = "toast-container position-fixed top-0 end-0 p-3";
    container.style.zIndex = "1095";
    document.body.appendChild(container);
    return container;
  }

  function getToastTheme(type) {
    switch ((type || "info").toLowerCase()) {
      case "success":
        return {
          icon: "bi-check-circle-fill",
          accent: "#22c55e",
          title: "Success",
        };
      case "warning":
        return {
          icon: "bi-exclamation-triangle-fill",
          accent: "#f97316",
          title: "Warning",
        };
      case "error":
      case "danger":
        return { icon: "bi-x-octagon-fill", accent: "#ef4444", title: "Error" };
      default:
        return {
          icon: "bi-info-circle-fill",
          accent: "#06b6d4",
          title: "Info",
        };
    }
  }

  globalThis.ZoneBillToast = {
    show: function (message, options) {
      options = options || {};
      var theme = getToastTheme(options.type);
      var container = ensureContainer();
      var toast = document.createElement("div");
      toast.className = "toast zb-toast";
      toast.setAttribute("role", "alert");
      toast.setAttribute("aria-live", "assertive");
      toast.setAttribute("aria-atomic", "true");
      toast.innerHTML =
        '<div class="toast-header zb-toast-header" style="border-left:3px solid ' +
        theme.accent +
        ';">' +
        '<i class="bi ' +
        theme.icon +
        ' me-2" style="color:' +
        theme.accent +
        ';"></i>' +
        '<strong class="me-auto">' +
        escapeHtml(options.title || theme.title) +
        "</strong>" +
        '<button type="button" class="btn-close btn-close-white ms-2 mb-1" data-bs-dismiss="toast" aria-label="Close"></button>' +
        "</div>" +
        '<div class="toast-body zb-toast-body">' +
        escapeHtml(message) +
        "</div>";

      container.appendChild(toast);

      var instance = bootstrap.Toast.getOrCreateInstance(toast, {
        autohide: options.autohide !== false,
        delay: options.delay || 4500,
      });

      toast.addEventListener(
        "hidden.bs.toast",
        function () {
          toast.remove();
        },
        { once: true },
      );

      instance.show();
    },
  };
})();

// ─── Shared Notification Bell Helpers ───────────────────────────────────────
(function () {
  function escapeNotificationHtml(value) {
    var normalized = String(value || "");
    return normalized
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function severityColor(severity) {
    switch ((severity || "info").toLowerCase()) {
      case "danger":
      case "error":
        return "#ef4444";
      case "warning":
        return "#f97316";
      case "success":
        return "#22c55e";
      default:
        return "#06b6d4";
    }
  }

  function updateBadge(element, count) {
    if (!element) return;
    if (count > 0) {
      element.textContent = count > 99 ? "99+" : String(count);
      element.style.display = "";
    } else {
      element.style.display = "none";
    }
  }

  function updateSidebarIndicator(elementId, isVisible) {
    if (!elementId) return;
    const element = document.getElementById(elementId);
    if (!element) return;
    element.style.display = isVisible ? "inline-flex" : "none";
  }

  function notificationMatchesIndicator(item, indicator) {
    if (!item || !indicator) return false;

    const itemKey = String(item.key || "").toLowerCase();
    const itemLink = String(item.link || "").toLowerCase();
    const itemCount = Number(item.count || 0);

    if (itemCount <= 0) return false;

    const keyMatch = Array.isArray(indicator.keys)
      ? indicator.keys.some(function (key) {
          return itemKey === String(key || "").toLowerCase();
        })
      : false;

    const linkMatch = Array.isArray(indicator.links)
      ? indicator.links.some(function (link) {
          return itemLink.startsWith(String(link || "").toLowerCase());
        })
      : false;

    return keyMatch || linkMatch;
  }

  function updateSidebarIndicators(indicators, items) {
    if (!Array.isArray(indicators) || indicators.length === 0) return;

    const safeItems = Array.isArray(items) ? items : [];
    indicators.forEach(function (indicator) {
      const isVisible = safeItems.some(function (item) {
        return notificationMatchesIndicator(item, indicator);
      });

      updateSidebarIndicator(indicator.elementId, isVisible);
    });
  }

  function renderItems(container, items, emptyMessage) {
    if (!container) return;

    if (!items || items.length === 0) {
      container.innerHTML =
        '<div class="zb-notif-empty">' +
        '<i class="bi bi-check-circle-fill me-2" style="color:#22c55e;"></i>' +
        escapeNotificationHtml(emptyMessage) +
        "</div>";
      return;
    }

    var html = "";
    items.forEach(function (item) {
      var color = severityColor(item.severity || item.type);
      html +=
        '<a href="' +
        escapeNotificationHtml(item.link) +
        '" class="dropdown-item zb-notif-item">' +
        '<span class="zb-notif-item-icon" style="color:' +
        color +
        ';"><i class="bi ' +
        escapeNotificationHtml(item.icon || "bi-bell") +
        '"></i></span>' +
        '<span class="zb-notif-item-copy">' +
        '<span class="zb-notif-item-title">' +
        escapeNotificationHtml(item.title || item.label || "Notification") +
        "</span>" +
        '<span class="zb-notif-item-message">' +
        escapeNotificationHtml(item.message || item.label || "") +
        "</span>" +
        "</span>" +
        '<span class="zb-notif-item-count">' +
        escapeNotificationHtml(item.count || "") +
        "</span>" +
        "</a>";
    });

    container.innerHTML = html;
  }

  globalThis.ZoneBillNotifications = {
    initBusinessBell: function (config) {
      if (!config?.url) return;

      const loadBusinessNotifications = function (shouldToastOnError) {
        fetch(config.url)
          .then(function (response) {
            if (!response.ok) throw new Error("Failed to load notifications.");
            return response.json();
          })
          .then(function (data) {
            updateBadge(
              document.getElementById(config.badgeId),
              data.count || 0,
            );
            updateSidebarIndicators(config.sidebarIndicators, data.items || []);
            renderItems(
              document.getElementById(config.itemsId),
              data.items || [],
              "All clear — no active alerts.",
            );
          })
          .catch(function () {
            updateSidebarIndicators(config.sidebarIndicators, []);
            renderItems(
              document.getElementById(config.itemsId),
              [],
              "Unable to load notifications right now.",
            );
            if (shouldToastOnError && globalThis.ZoneBillToast) {
              globalThis.ZoneBillToast.show(
                "Notifications could not be loaded.",
                {
                  type: "warning",
                  title: "Notification Center",
                },
              );
            }
          });
      };

      loadBusinessNotifications(true);

      if (config.refreshMs && Number(config.refreshMs) > 0) {
        window.setInterval(function () {
          loadBusinessNotifications(false);
        }, Number(config.refreshMs));
      }
    },

    initSuperAdminBell: function (config) {
      if (!config?.countUrl || !config.itemsUrl) return;

      fetch(config.countUrl)
        .then(function (response) {
          if (!response.ok) throw new Error("Failed to load alert count.");
          return response.json();
        })
        .then(function (data) {
          updateBadge(document.getElementById(config.badgeId), data.count || 0);
        })
        .catch(function () {
          updateBadge(document.getElementById(config.badgeId), 0);
        });

      fetch(config.itemsUrl)
        .then(function (response) {
          if (!response.ok) throw new Error("Failed to load alert items.");
          return response.json();
        })
        .then(function (data) {
          renderItems(
            document.getElementById(config.itemsId),
            data.items || [],
            "All clear — no active alerts.",
          );
        })
        .catch(function () {
          renderItems(
            document.getElementById(config.itemsId),
            [],
            "Unable to load alerts right now.",
          );
        });
    },
  };
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
