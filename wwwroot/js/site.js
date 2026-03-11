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

    var data = el.getAttribute("data-polylines");
    if (!data) {
      return;
    }

    var polylines;
    try {
      polylines = JSON.parse(data);
    } catch {
      polylines = [];
    }

    if (!Array.isArray(polylines) || polylines.length === 0) {
      return;
    }

    var allLatLngs = [];

    var map = L.map(el);

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 18,
      attribution: "&copy; OpenStreetMap contributors",
    }).addTo(map);

    polylines.forEach(function (encoded) {
      var coords = decodePolyline(encoded, 5);
      if (!coords.length) {
        return;
      }

      var latlngs = coords.map(function (c) {
        return [c[0], c[1]];
      });

      allLatLngs = allLatLngs.concat(latlngs);

      L.polyline(latlngs, {
        color: "#22c55e", // bright green for strong contrast
        weight: 4,
        opacity: 0.65,
      }).addTo(map);
    });

    if (allLatLngs.length) {
      var bounds = L.latLngBounds(allLatLngs);
      map.fitBounds(bounds, { padding: [20, 20] });
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

