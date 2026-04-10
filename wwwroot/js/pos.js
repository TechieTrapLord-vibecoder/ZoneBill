(function () {
  function updateTimers() {
    const timers = document.querySelectorAll(".pos-timer");
    const now = new Date();

    timers.forEach((timer) => {
      const raw = timer.getAttribute("data-start-time");
      if (!raw) {
        return;
      }

      const start = new Date(raw);
      const diff = Math.max(0, Math.floor((now - start) / 1000));
      const hours = String(Math.floor(diff / 3600)).padStart(2, "0");
      const minutes = String(Math.floor((diff % 3600) / 60)).padStart(2, "0");
      const seconds = String(diff % 60).padStart(2, "0");
      timer.textContent = `${hours}:${minutes}:${seconds}`;
    });
  }

  updateTimers();
  setInterval(updateTimers, 1000);

  // POS auto-refresh: poll every 10 seconds for live space status updates
  function pollSpaceStatus() {
    fetch("/POS/GetSpaceStatus")
      .then(function (r) {
        return r.ok ? r.json() : null;
      })
      .then(function (spaces) {
        if (!spaces) return;
        spaces.forEach(function (s) {
          var col = document.querySelector(
            '[data-space-id="' + s.spaceId + '"]',
          );
          if (!col) return;
          var card = col.querySelector(".card");
          if (!card) return;

          // Update card border
          card.classList.remove("border-danger", "border-success");
          card.classList.add(
            s.status === "Occupied" ? "border-danger" : "border-success",
          );

          // Update card header background and status text
          var header = card.querySelector(".card-header");
          if (header) {
            header.classList.remove("bg-danger", "bg-success");
            header.classList.add(
              s.status === "Occupied" ? "bg-danger" : "bg-success",
            );
            var statusSpan = header.querySelector("span");
            if (statusSpan) statusSpan.textContent = s.status;
          }

          // Update order count badge inside the booking # paragraph
          var bookingPara = card.querySelector(".card-body p.mb-2");
          if (bookingPara) {
            var badge = bookingPara.querySelector(".badge");
            if (s.orderItemCount > 0) {
              if (badge) {
                badge.textContent = s.orderItemCount + " items ordered";
              } else {
                badge = document.createElement("span");
                badge.className = "badge bg-warning text-dark ms-2";
                badge.textContent = s.orderItemCount + " items ordered";
                bookingPara.appendChild(badge);
              }
            } else if (badge) {
              badge.remove();
            }
          }

          // Update timer's data-start-time so the 1s loop picks up the correct start
          var timer = card.querySelector(".pos-timer");
          if (timer && s.startTime) {
            timer.setAttribute("data-start-time", s.startTime);
          }
        });
      })
      .catch(function () {
        // network error — silently ignore, will retry on next interval
      });
  }

  setInterval(pollSpaceStatus, 10000);
})();
