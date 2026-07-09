// HamMap front-end: renders the Hungarian amateur-radio distribution on OSM.

const map = L.map('map').setView([47.16, 19.5], 7);

L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  maxZoom: 19,
  attribution: '&copy; OpenStreetMap contributors'
}).addTo(map);

// Scale circle radius by count (sqrt so area ~ count), clamped for readability.
function radiusFor(count, max) {
  const minR = 4, maxR = 34;
  const r = Math.sqrt(count / max) * maxR;
  return Math.max(minR, r);
}

// Blue → red ramp based on share of the maximum city.
function colorFor(count, max) {
  const t = Math.min(1, count / max);
  const hue = (1 - t) * 210; // 210 (blue) → 0 (red)
  return `hsl(${hue}, 80%, 45%)`;
}

fetch('/api/distribution')
  .then(r => r.json())
  .then(data => {
    const cities = data.cities;
    const max = cities.length ? cities[0].count : 1; // list is sorted desc

    // --- Graduated circles layer ---
    const circles = L.layerGroup();
    for (const c of cities) {
      L.circleMarker([c.lat, c.lon], {
        radius: radiusFor(c.count, max),
        color: colorFor(c.count, max),
        fillColor: colorFor(c.count, max),
        fillOpacity: 0.6,
        weight: 1
      })
        .bindPopup(`<b>${c.city}</b><br>${c.count} licensee${c.count === 1 ? '' : 's'}`)
        .bindTooltip(`${c.city}: ${c.count}`)
        .addTo(circles);
    }

    // --- Heatmap layer ---
    const heatPoints = cities.map(c => [c.lat, c.lon, c.count]);
    const heat = L.heatLayer(heatPoints, {
      radius: 25, blur: 18, maxZoom: 11, max: max
    });

    circles.addTo(map);

    L.control.layers(null, {
      'Graduated circles': circles,
      'Heatmap': heat
    }, { collapsed: false }).addTo(map);

    // --- Stats panel ---
    document.getElementById('stats').innerHTML = `
      Total records: <b>${data.totalEntries.toLocaleString()}</b><br>
      Hungarian: <b>${data.hungarianEntries.toLocaleString()}</b><br>
      Mapped: <b>${data.mapped.toLocaleString()}</b> in
      <b>${cities.length.toLocaleString()}</b> towns<br>
      Unresolved: <b>${data.unresolved.toLocaleString()}</b>`;

    document.getElementById('note').textContent =
      'Coordinates: GeoNames (CC-BY). Data: NMHH call sign book.';

    // --- Legend ---
    const buckets = [max, Math.round(max / 2), Math.round(max / 5), Math.max(1, Math.round(max / 20))];
    const seen = new Set();
    let html = '<b>Licensees / town</b><br>';
    for (const v of buckets) {
      if (seen.has(v)) continue;
      seen.add(v);
      html += `<i style="background:${colorFor(v, max)}"></i>${v}<br>`;
    }
    document.getElementById('legend').innerHTML = html;
  })
  .catch(err => {
    document.getElementById('stats').textContent = 'Failed to load data: ' + err;
  });
