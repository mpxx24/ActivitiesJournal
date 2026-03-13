// Global JavaScript for the site, including activity maps.

(function () {
  // Decode an encoded polyline (Google/Strava format) into [lat, lng] pairs.
  function decodePolyline(str, precision) {
    if (!str) {
      return [];
    }

    var index = 0;
    var lat = 0;
    var lng = 0;
    var coordinates = [];
    var shift;
    var result;
    var byte;
    var factor = Math.pow(10, precision || 5);

    while (index < str.length) {
      shift = 0;
      result = 0;
      do {
        byte = str.charCodeAt(index++) - 63;
        result |= (byte & 0x1f) << shift;
        shift += 5;
      } while (byte >= 0x20);
      var deltaLat = (result & 1) ? ~(result >> 1) : (result >> 1);
      lat += deltaLat;

      shift = 0;
      result = 0;
      do {
        byte = str.charCodeAt(index++) - 63;
        result |= (byte & 0x1f) << shift;
        shift += 5;
      } while (byte >= 0x20);
      var deltaLng = (result & 1) ? ~(result >> 1) : (result >> 1);
      lng += deltaLng;

      coordinates.push([lat / factor, lng / factor]);
    }

    return coordinates;
  }

  function initActivityMaps() {
    if (typeof L === "undefined") {
      return;
    }

    var mapElements = document.querySelectorAll(".activity-map[data-polyline]");
    if (!mapElements.length) {
      return;
    }

    mapElements.forEach(function (el) {
      var encoded = el.getAttribute("data-polyline");
      if (!encoded) {
        return;
      }

      var coords = decodePolyline(encoded, 5);
      if (!coords.length) {
        return;
      }

      var map = L.map(el, {
        zoomControl: false,
      });

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 18,
        attribution: "&copy; OpenStreetMap contributors",
      }).addTo(map);

      var polyline = L.polyline(
        coords.map(function (c) {
          return [c[0], c[1]];
        }),
        {
          color: "#ff6b00",
          weight: 4,
        }
      ).addTo(map);

      map.fitBounds(polyline.getBounds(), { padding: [10, 10] });
    });
  }

  function initHeatmap() {
    if (typeof L === "undefined") {
      return;
    }

    var el = document.getElementById("heatmap-map");
    if (!el) {
      return;
    }

    // Try new metadata format first, fall back to old polylines-only
    var metaData = el.getAttribute("data-activity-meta");
    var mode = el.getAttribute("data-mode") || "all";
    var activities = [];

    if (metaData) {
      try { activities = JSON.parse(metaData); } catch { activities = []; }
    } else {
      // Legacy: plain polylines array
      var plain = el.getAttribute("data-polylines");
      if (plain) {
        try { activities = JSON.parse(plain).map(function(p) { return { p: p, daysAgo: 365, isNew: false }; }); } catch { activities = []; }
      }
    }

    if (!activities.length) return;

    // Age-based colour palette (oldest → newest)
    var AGE_COLORS = ["#6c757d", "#17a2b8", "#28a745", "#ffc107", "#fd7e14"];
    function ageColor(daysAgo) {
      if (daysAgo < 30)  return AGE_COLORS[4];
      if (daysAgo < 90)  return AGE_COLORS[3];
      if (daysAgo < 180) return AGE_COLORS[2];
      if (daysAgo < 365) return AGE_COLORS[1];
      return AGE_COLORS[0];
    }

    var allLatLngs = [];
    var map = L.map(el);

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 18,
      attribution: "&copy; OpenStreetMap contributors",
    }).addTo(map);

    activities.forEach(function (act) {
      var encoded = act.p || act;
      var coords = decodePolyline(encoded, 5);
      if (!coords.length) return;

      var latlngs = coords.map(function (c) { return [c[0], c[1]]; });
      allLatLngs = allLatLngs.concat(latlngs);

      var color, opacity, weight;

      if (mode === "new") {
        // Highlight new routes (last 6 months) in orange, older in faint gray
        if (act.isNew) {
          color = "#fd7e14"; opacity = 0.9; weight = 3;
        } else {
          color = "#495057"; opacity = 0.25; weight = 2;
        }
      } else if (mode === "age") {
        color = ageColor(act.daysAgo || 9999);
        opacity = 0.75; weight = 3;
      } else {
        // Default "all" mode — uniform green
        color = "#22c55e"; opacity = 0.65; weight = 3;
      }

      L.polyline(latlngs, { color: color, weight: weight, opacity: opacity }).addTo(map);
    });

    if (allLatLngs.length) {
      map.fitBounds(L.latLngBounds(allLatLngs), { padding: [20, 20] });
    }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      initActivityMaps();
      initHeatmap();
    });
  } else {
    initActivityMaps();
    initHeatmap();
  }
})();

