// Small touches that make the page feel like the game. None of it is needed
// to read the page or download the game: every link works without it.
(function () {
  "use strict";
  document.documentElement.classList.add("js");
  var calm = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  // ---- offer the right download first for the visitor's device
  var base = "https://github.com/Chandirasegaran/current-poche/releases/latest/download/";
  var agent = navigator.userAgent;
  var pick = /Android/i.test(agent) ? ["android", "Download for Android", base + "CurrentPochu-Android.apk"]
    : /Windows/i.test(agent) ? ["windows", "Download for Windows", base + "CurrentPochu-Setup.exe"]
    : /Linux/i.test(agent) ? ["linux", "Download for Linux", base + "CurrentPochu-x86_64.AppImage"]
    : null;
  if (pick) {
    var main = document.getElementById("primary-download");
    main.textContent = pick[1];
    main.href = pick[2];
    document.getElementById("primary-note").innerHTML = 'Free. No sign-up. Or <a href="play/">play in your browser</a>.';
    var card = document.querySelector('[data-os="' + pick[0] + '"]');
    if (card) card.classList.add("yours");
  }

  // ---- show the latest version number, straight from GitHub
  fetch("https://api.github.com/repos/Chandirasegaran/current-poche/releases/latest")
    .then(function (reply) { return reply.ok ? reply.json() : null; })
    .then(function (release) {
      if (!release || !release.tag_name) return;
      var when = new Date(release.published_at).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });
      document.getElementById("release-line").textContent = "Latest: version " + release.tag_name.replace(/^v/, "") + ", released " + when;
    })
    .catch(function () { /* the plain sentence stays */ });

  // ---- slide things into view, and light a lamp for each section reached
  var lamps = Array.prototype.slice.call(document.querySelectorAll(".lamps a"));
  if ("IntersectionObserver" in window) {
    var reveal = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) { entry.target.classList.add("in"); reveal.unobserve(entry.target); }
      });
    }, { threshold: 0.15 });
    document.querySelectorAll(".reveal").forEach(function (element) { reveal.observe(element); });

    var reached = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        lamps.forEach(function (lamp) { if (lamp.dataset.for === entry.target.id) lamp.classList.add("lit"); });
      });
    }, { threshold: 0.25 });
    lamps.forEach(function (lamp) {
      var section = document.getElementById(lamp.dataset.for);
      if (section) reached.observe(section);
    });
  } else {
    document.querySelectorAll(".reveal").forEach(function (element) { element.classList.add("in"); });
  }

  // ---- the dialogue box: townsfolk take turns, typed out like in the game
  var talk = [
    ["lineman", "Lineman Murugesan", "The lines are empty, thambi. Like somebody drank the current with a straw."],
    ["paati", "Paati", "No current, no serial, no cricket. And now I cannot find my glasses either."],
    ["teamaster", "Tea Master Selvam", "No current, no mixie, no fridge. But tea? Tea runs on firewood."],
    ["chairman", "The Chairman", "I have a speech at nine. NINE! How will people see my face in the dark?"],
    ["rani", "Signal Rani", "Three tracks, three engines, and not one driver can see you in the dark."],
    ["watchman", "Watchman Kannan", "Raja Talkies. Closed since the big storm, except for me and the ghost."],
  ];
  var face = document.getElementById("talk-face"), name = document.getElementById("talk-name"), line = document.getElementById("talk-line");
  var turn = 0, typing = null;
  function say() {
    var who = talk[turn % talk.length];
    turn++;
    face.src = "assets/sprites/" + who[0] + ".png";
    face.alt = who[1];
    name.textContent = who[1];
    if (calm) { line.textContent = who[2]; return; }
    var shown = 0;
    clearInterval(typing);
    typing = setInterval(function () {
      shown++;
      line.textContent = who[2].slice(0, shown);
      if (shown >= who[2].length) { clearInterval(typing); setTimeout(say, 3200); }
    }, 28);
  }
  if (face && !calm) setTimeout(function () { turn = 1; say(); }, 4200);

  // ---- chapter select: click a region of the map
  var spots = Array.prototype.slice.call(document.querySelectorAll(".spot"));
  var chapters = Array.prototype.slice.call(document.querySelectorAll(".chapter-list li"));
  function choose(number) {
    spots.forEach(function (spot) { spot.classList.toggle("on", spot.dataset.chapter === number); });
    chapters.forEach(function (chapter) { chapter.classList.toggle("on", chapter.dataset.chapter === number); });
  }
  spots.forEach(function (spot) {
    spot.addEventListener("click", function () { choose(spot.dataset.chapter); touched = true; });
    spot.addEventListener("mouseenter", function () { choose(spot.dataset.chapter); touched = true; });
  });
  var touched = false, showing = 1;
  choose("1");
  if (!calm) setInterval(function () { // tour the chapters until the visitor takes over
    if (touched) return;
    showing = showing % 6 + 1;
    choose(String(showing));
  }, 3600);

  // ---- screenshots: thumbnails swap the big picture; clicking it opens it full size
  var big = document.getElementById("big-shot"), box = document.getElementById("lightbox");
  document.querySelectorAll(".thumbs a").forEach(function (thumb) {
    thumb.addEventListener("click", function (event) {
      event.preventDefault();
      big.src = thumb.href;
      big.alt = thumb.querySelector("img").alt;
      document.querySelectorAll(".thumbs a").forEach(function (other) { other.classList.toggle("on", other === thumb); });
    });
  });
  if (big) big.addEventListener("click", function () { box.querySelector("img").src = big.src; box.querySelector("img").alt = big.alt; box.hidden = false; });
  box.addEventListener("click", function () { box.hidden = true; });
  document.addEventListener("keydown", function (event) { if (event.key === "Escape") box.hidden = true; });

  // ---- minminis drifting behind the whole page
  var canvas = document.getElementById("sparks");
  if (canvas && !calm) {
    var pen = canvas.getContext("2d"), bugs = [], width, height;
    function size() {
      width = canvas.width = window.innerWidth;
      height = canvas.height = window.innerHeight;
    }
    size();
    window.addEventListener("resize", size);
    for (var i = 0; i < 46; i++)
      bugs.push({ x: Math.random(), y: Math.random(), seed: Math.random() * 100, speed: 0.2 + Math.random() * 0.5 });
    (function draw(time) {
      var t = time / 1000;
      pen.clearRect(0, 0, width, height);
      bugs.forEach(function (bug) {
        var x = (bug.x + Math.sin(t * 0.2 * bug.speed + bug.seed) * 0.04) * width;
        var y = ((bug.y - t * 0.006 * bug.speed) % 1 + 1) % 1 * height;
        var glow = 0.35 + 0.65 * Math.abs(Math.sin(t * bug.speed * 1.3 + bug.seed));
        pen.fillStyle = "rgba(200, 255, 102, " + (0.5 * glow) + ")";
        pen.fillRect(Math.round(x), Math.round(y), 3, 3);
        pen.fillStyle = "rgba(200, 255, 102, " + (0.12 * glow) + ")";
        pen.fillRect(Math.round(x) - 3, Math.round(y) - 3, 9, 9);
      });
      requestAnimationFrame(draw);
    })(0);
  }
})();
