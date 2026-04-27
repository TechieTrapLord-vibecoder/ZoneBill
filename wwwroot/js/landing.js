// ═══════════════════════════════════════════════════════
// ZoneBill Landing — Scroll Animations & Interactions
// ═══════════════════════════════════════════════════════

(function () {
  "use strict";

  // ─── Intersection Observer for scroll-triggered animations ───
  var revealElements = document.querySelectorAll(
    ".lp-reveal, .lp-reveal-left, .lp-reveal-right, .lp-reveal-scale",
  );

  if ("IntersectionObserver" in window && revealElements.length > 0) {
    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) {
            entry.target.style.opacity = "";
            entry.target.classList.add("revealed");
            observer.unobserve(entry.target);
          }
        });
      },
      {
        threshold: 0.12,
        rootMargin: "0px 0px -40px 0px",
      },
    );

    revealElements.forEach(function (el) {
      observer.observe(el);
    });
  }

  // ─── Animated number counter ─────────────────────────────────
  var countElements = document.querySelectorAll("[data-count-to]");

  if ("IntersectionObserver" in window && countElements.length > 0) {
    var countObserver = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) {
            animateCount(entry.target);
            countObserver.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.5 },
    );

    countElements.forEach(function (el) {
      countObserver.observe(el);
    });
  }

  function animateCount(el) {
    var target = parseInt(el.getAttribute("data-count-to"), 10);
    var prefix = el.getAttribute("data-count-prefix") || "";
    var suffix = el.getAttribute("data-count-suffix") || "";
    var duration = 1500;
    var startTime = null;

    function step(timestamp) {
      if (!startTime) startTime = timestamp;
      var progress = Math.min((timestamp - startTime) / duration, 1);
      // Ease out cubic
      var eased = 1 - Math.pow(1 - progress, 3);
      var current = Math.floor(eased * target);
      el.textContent = prefix + current.toLocaleString() + suffix;
      if (progress < 1) {
        requestAnimationFrame(step);
      } else {
        el.textContent = prefix + target.toLocaleString() + suffix;
      }
    }
    requestAnimationFrame(step);
  }

  // ─── Navbar scroll shadow ────────────────────────────────────
  var navbar = document.querySelector(".lp-navbar");
  if (navbar) {
    var lastScrollY = 0;
    window.addEventListener(
      "scroll",
      function () {
        var scrollY = window.pageYOffset || document.documentElement.scrollTop;
        if (scrollY > 50) {
          navbar.style.boxShadow = "0 4px 30px rgba(0,0,0,0.4)";
          navbar.style.borderBottomColor = "rgba(6,182,212,0.35)";
        } else {
          navbar.style.boxShadow = "none";
          navbar.style.borderBottomColor = "rgba(6,182,212,0.2)";
        }
        lastScrollY = scrollY;
      },
      { passive: true },
    );
  }

  // ─── Smooth scroll for anchor links ──────────────────────────
  document.querySelectorAll('a[href^="#"]').forEach(function (anchor) {
    anchor.addEventListener("click", function (e) {
      var hash = this.getAttribute("href");
      if (hash === "#") return;
      var target = document.querySelector(hash);
      if (target) {
        e.preventDefault();
        target.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    });
  });

  // ─── Parallax effect for hero orbs on mouse move ─────────────
  var heroSection = document.querySelector(".lp-hero");
  var orbs = document.querySelectorAll(".lp-orb");
  if (heroSection && orbs.length > 0) {
    heroSection.addEventListener(
      "mousemove",
      function (e) {
        var rect = heroSection.getBoundingClientRect();
        var x = (e.clientX - rect.left) / rect.width - 0.5;
        var y = (e.clientY - rect.top) / rect.height - 0.5;

        orbs.forEach(function (orb, index) {
          var speed = (index + 1) * 15;
          orb.style.transform =
            "translate(" + x * speed + "px, " + y * speed + "px)";
        });
      },
      { passive: true },
    );
  }

  // ─── Active nav link highlight on scroll ─────────────────────
  var sections = document.querySelectorAll("section[id]");
  var navLinks = document.querySelectorAll('.lp-nav-link[href^="#"]');

  if (sections.length > 0 && navLinks.length > 0) {
    window.addEventListener(
      "scroll",
      function () {
        var scrollPos = window.pageYOffset + 120;
        sections.forEach(function (section) {
          var top = section.offsetTop;
          var height = section.offsetHeight;
          var id = section.getAttribute("id");
          if (scrollPos >= top && scrollPos < top + height) {
            navLinks.forEach(function (link) {
              link.style.color = "";
              if (link.getAttribute("href") === "#" + id) {
                link.style.color = "#06b6d4";
              }
            });
          }
        });
      },
      { passive: true },
    );
  }
})();
