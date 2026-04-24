// MapLibre has no native quadkey support. We register a custom "bing://" protocol
// that reads the tile URL template from a closure variable and substitutes the
// computed quadkey. The tile URL itself never travels through MapLibre's URL
// parsing, avoiding any encoding/https-stripping issues.

let _bingTileTemplate = null; // set by fetchBingSource before the source is added

function tileToQuadkey(x, y, z) {
    let key = '';
    for (let i = z; i > 0; i--) {
        let digit = 0;
        const mask = 1 << (i - 1);
        if (x & mask) digit++;
        if (y & mask) digit += 2;
        key += digit;
    }
    return key;
}

// Called once at startup; safe to call multiple times (MapLibre deduplicates).
function registerBingProtocol() {
    maplibregl.addProtocol('bing', async (params, abortController) => {
        if (!_bingTileTemplate) {
            throw new Error('Bing tile template not loaded');
        }

        // params.url = "bing://{z}/{x}/{y}"; MapLibre fills in the numbers
        const parts = params.url.replace('bing://', '').split('/');
        const z = parseInt(parts[0], 10);
        const x = parseInt(parts[1], 10);
        const y = parseInt(parts[2], 10);
        const tileUrl = _bingTileTemplate.replace('{quadkey}', tileToQuadkey(x, y, z));

        const res = await fetch(tileUrl, { signal: abortController.signal });
        const data = await res.arrayBuffer();
        return { data };
    });
}

// Fetch Bing metadata, store the template, and return a MapLibre source config.
async function fetchBingSource(apiKey) {
    const metaUrl = `https://dev.virtualearth.net/REST/v1/Imagery/Metadata/AerialOSM`
        + `?include=ImageryProviders&uriScheme=https&key=${apiKey}`;
    const res = await fetch(metaUrl);
    const json = await res.json();
    const resource = json.resourceSets?.[0]?.resources?.[0];
    if (!resource) throw new Error('Bing metadata returned no resources');

    // Pick a fixed subdomain, all subdomains serve the same tiles
    _bingTileTemplate = resource.imageUrl
        .replace('{subdomain}', resource.imageUrlSubdomains[0]);

    return {
        type: 'raster',
        tiles: ['bing://{z}/{x}/{y}'],
        tileSize: resource.imageWidth || 256,
        maxzoom: 19,  // Bing advertises zoomMax=21 but tiles are unreliable past 19 in most areas
        attribution: 'Bing Maps © Microsoft'
    };
}

// Called from InspectorPanel.razor via JSRuntime to make the panel draggable
// by its header. Uses pointer events so it works on touch screens too.
window.makePanelDraggable = function (panel, handle) {
    if (!panel || !handle)
    {
        return;
    }

    // Convert from right-anchored CSS to left-anchored so we can move it freely.
    function snapToAbsolute() {
        const r = panel.getBoundingClientRect();
        panel.style.right = '';
        panel.style.left = r.left + 'px';
        panel.style.top  = r.top  + 'px';
    }

    let startX, startY, startLeft, startTop;

    handle.addEventListener('pointerdown', e => {
        // Only drag on the header itself, not buttons inside it
        if (e.target.closest('button'))
        {
            return;
        }

        e.preventDefault();

        snapToAbsolute();
        startX    = e.clientX;
        startY    = e.clientY;
        startLeft = panel.offsetLeft;
        startTop  = panel.offsetTop;

        handle.setPointerCapture(e.pointerId);

        function onMove(e) {
            const left = Math.max(0, Math.min(window.innerWidth  - 60, startLeft + e.clientX - startX));
            const top  = Math.max(0, Math.min(window.innerHeight - 40, startTop  + e.clientY - startY));
            panel.style.left = left + 'px';
            panel.style.top  = top  + 'px';
        }

        function onUp() {
            handle.removeEventListener('pointermove', onMove);
            handle.removeEventListener('pointerup',   onUp);
        }

        handle.addEventListener('pointermove', onMove);
        handle.addEventListener('pointerup',   onUp);
    });
};

window.mapInterop = (() => {
    let map = null;
    let dotnetRef = null;
    let moveEndTimer = null;
    let hoverTimer = null;
    let lastHoveredId = undefined; // sentinel: undefined = never sent, null = sent "nothing hovered"

    registerBingProtocol();

    const sourceCache = {};

    // Builds the "type/id" string the C# click handler expects from a MapLibre feature.
    function elementIdFromFeature(feature) {
        if (!feature?.properties)
        {
            return null;
        }

        const type = feature.properties.type;  // 'node', 'way', 'relation'
        const id   = feature.properties.id;    // numeric string
        if (!type || !id)
        {
            return null;
        }

        return `${type}/${id}`;
    }

    // Drag state
    let dragNodeId = null;
    let dragOriginalPos = null; // [lng, lat] at drag start, used for self-intersection revert
    let dragStartPixel = null; // {x, y} screen-pixel position at mousedown, for distance threshold
    let isDragging = false;
    let isDragInvalid = false; // true when current drag position creates a self-intersecting polygon

    // Active tool name, kept in sync by setActiveTool so cursor handlers can check it.
    let activeTool = 'select';

    // Vector helpers (matching iD's geoVec* conventions).
    function vecSubtract(a, b)
    {
        return [a[0] - b[0], a[1] - b[1]];
    }

    function vecCross(a, b)
    {
        return a[0] * b[1] - a[1] * b[0];
    }

    function vecEqual(a, b, epsilon)
    {
        if (epsilon === undefined)
        {
            return a[0] === b[0] && a[1] === b[1];
        }

        return Math.abs(a[0] - b[0]) <= epsilon && Math.abs(a[1] - b[1]) <= epsilon;
    }

    // Return the intersection point of two line segments, or null if they don't intersect.
    // Ported directly from iD's geoLineIntersection (cross-product method).
    function lineIntersection(a, b)
    {
        const p  = a[0], p2 = a[1];
        const q  = b[0], q2 = b[1];
        const r  = vecSubtract(p2, p);
        const s  = vecSubtract(q2, q);
        const uN = vecCross(vecSubtract(q, p), r);
        const d  = vecCross(r, s);
        if (!uN || !d)
        {
            return null; // parallel or collinear
        }

        const u = uN / d;
        const t = vecCross(vecSubtract(q, p), s) / d;
        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            return [p[0] + t * r[0], p[1] + t * r[1]];
        }
        return null;
    }

    // Returns true if the ring self-intersects when the node at movedCoord is moved.
    // Ported from iD's geoHasSelfIntersections: only tests active segments (those
    // touching the moved node) against inactive segments, with proper endpoint-sharing
    // and epsilon checks to avoid false positives at shared ring vertices.
    // ring | array of [lng, lat] in OSM nodeIds order, `ring[0] === ring[last]`
    // movedCoord | the [lng, lat] of the node being dragged (its new position)
    function ringSelfIntersects(ring, movedCoord)
    {
        const epsilon = 1e-8;
        const actives   = []; // segments that touch the dragged node
        const inactives = []; // all other segments

        const n = ring.length - 1; // number of edges
        for (let i = 0; i < n; i++)
        {
            const seg = [ring[i], ring[i + 1]];
            const p0isActive = vecEqual(ring[i],     movedCoord);
            const p1isActive = vecEqual(ring[i + 1], movedCoord);
            if (p0isActive || p1isActive)
            {
                actives.push(seg);
            }
            else
            {
                inactives.push(seg);
            }
        }

        for (const p of actives)
        {
            for (const q of inactives)
            {
                // Skip if segments share an endpoint (adjacent edges at non-moved vertices).
                if (vecEqual(p[1], q[0])
                    || vecEqual(p[0], q[1])
                    || vecEqual(p[0], q[0])
                    || vecEqual(p[1], q[1]))
                {
                    continue;
                }

                const hit = lineIntersection(p, q);
                if (hit)
                {
                    // Skip hits exactly at an endpoint (floating-point fuzz).
                    if (vecEqual(p[1], hit, epsilon)
                        || vecEqual(p[0], hit, epsilon)
                        || vecEqual(q[1], hit, epsilon)
                        || vecEqual(q[0], hit, epsilon))
                    {
                        continue;
                    }

                    return true;
                }
            }
        }
        return false;
    }


    // Named GeoJSON sources and their initial empty FeatureCollections
    const SOURCES = [
        'osm-nodes', 'osm-ways', 'osm-relations',
        'osm-selected', 'osm-hover', 'osm-vertices',
        'osm-notes', 'osm-gpx',
        'osm-draw-preview',
        'osm-gridify-preview',
        'osm-invalid',
        'osm-snap-segments'
    ];

    const EMPTY_FC = { type: 'FeatureCollection', features: [] };

    function addOsmSources() {
        for (const id of SOURCES) {
            map.addSource(id, { type: 'geojson', data: EMPTY_FC });
        }
    }

    function addOsmLayers() {
        // Ways, area fill first (below lines)
        map.addLayer({
            id: 'layer-ways-fill',
            type: 'fill',
            source: 'osm-ways',
            filter: ['==', ['get', 'area'], 'yes'],
            paint: {
                'fill-color': ['coalesce', ['get', 'fill'], '#aaa'],
                'fill-opacity': 0.2
            }
        });

        // Ways, invisible wide stroke for easier clicking
        map.addLayer({
            id: 'layer-ways-hit',
            type: 'line',
            source: 'osm-ways',
            paint: { 'line-color': 'transparent', 'line-width': 12 }
        });

        // Snap-segments, transparent hit layer — populated during a node drag with one
        // LineString per non-adjacent segment so queryRenderedFeatures gives pixel-exact snap.
        map.addLayer({
            id: 'layer-snap-segments-hit',
            type: 'line',
            source: 'osm-snap-segments',
            paint: { 'line-color': 'transparent', 'line-width': 12 }
        });

        // Ways, black casing behind the colored stroke
        map.addLayer({
            id: 'layer-ways-casing',
            type: 'line',
            source: 'osm-ways',
            paint: {
                'line-color': 'rgba(0, 0, 0, 0.55)',
                'line-width': 4
            }
        });

        // Ways, visible stroke
        map.addLayer({
            id: 'layer-ways',
            type: 'line',
            source: 'osm-ways',
            paint: {
                'line-color': ['coalesce', ['get', 'stroke'], '#555'],
                'line-width': 2
            }
        });

        // Directional arrows for oneway highways and waterways.
        // 'oneway'=1 forward arrow, 'oneway'=-1 reverse arrow.
        map.addLayer({
            id: 'layer-way-arrows',
            type: 'symbol',
            source: 'osm-ways',
            filter: ['!=', ['get', 'oneway'], '0'],
            layout: {
                'symbol-placement': 'line',
                'symbol-spacing': 80,
                'text-field': '▶',
                'text-font': ['Noto Sans Regular'],
                'text-size': 12,
                'text-rotation-alignment': 'map',
                'text-keep-upright': false,
                'text-rotate': ['case', ['==', ['get', 'oneway'], '-1'], 180, 0]
            },
            paint: {
                'text-color': ['coalesce', ['get', 'stroke'], '#555'],
                'text-opacity': 0.9,
                'text-halo-color': 'rgba(0, 0, 0, 0.55)',
                'text-halo-width': 1.5
            }
        });

        // Way name labels, along the line for non-area ways
        map.addLayer({
            id: 'layer-ways-label-line',
            type: 'symbol',
            source: 'osm-ways',
            filter: ['all', ['==', ['get', 'area'], 'no'], ['has', 'tag:name']],
            layout: {
                'symbol-placement': 'line',
                'text-field': ['get', 'tag:name'],
                'text-font': ['Noto Sans Regular'],
                'text-size': 11,
                'text-max-angle': 30,
                'text-padding': 10
            },
            paint: {
                'text-color': '#222',
                'text-halo-color': 'rgba(255, 255, 255, 0.75)',
                'text-halo-width': 2
            }
        });

        // Way name labels, at centroid for area ways
        map.addLayer({
            id: 'layer-ways-label-point',
            type: 'symbol',
            source: 'osm-ways',
            filter: ['all', ['==', ['get', 'area'], 'yes'], ['has', 'tag:name']],
            layout: {
                'symbol-placement': 'point',
                'text-field': ['get', 'tag:name'],
                'text-font': ['Noto Sans Regular'],
                'text-size': 11
            },
            paint: {
                'text-color': '#222',
                'text-halo-color': 'rgba(255, 255, 255, 0.75)',
                'text-halo-width': 2
            }
        });

        // Nodes, circles (only standalone nodes, way vertices are in osm-nodes too
        // but shown smaller; way vertices for selected ways come from osm-vertices)
        map.addLayer({
            id: 'layer-nodes',
            type: 'circle',
            source: 'osm-nodes',
            filter: ['==', ['get', 'show'], 'yes'],
            paint: {
                'circle-radius': 5,
                'circle-color': ['coalesce', ['get', 'fill'], '#fff'],
                'circle-stroke-color': ['coalesce', ['get', 'stroke'], '#555'],
                'circle-stroke-width': 2
            }
        });

        // Hover highlight, thick coloured outline behind the element
        map.addLayer({
            id: 'layer-hover',
            type: 'line',
            source: 'osm-hover',
            paint: { 'line-color': '#4af', 'line-width': 6, 'line-opacity': 0.5 }
        });

        // Hover highlight for nodes (line layer above can't render Point geometry)
        map.addLayer({
            id: 'layer-hover-node',
            type: 'circle',
            source: 'osm-hover',
            filter: ['==', ['geometry-type'], 'Point'],
            paint: {
                'circle-radius': 6,
                'circle-color': ['coalesce', ['get', 'fill'], '#fff'],
                'circle-stroke-color': '#4af',
                'circle-stroke-width': 3
            }
        });

        // Selected highlight, way outline for linear ways (drawn above hover)
        map.addLayer({
            id: 'layer-selected-ways',
            type: 'line',
            source: 'osm-selected',
            filter: ['==', ['geometry-type'], 'LineString'],
            paint: { 'line-color': '#0055ff', 'line-width': 3 }
        });

        // Selected highlight, area fill
        map.addLayer({
            id: 'layer-selected-fill',
            type: 'fill',
            source: 'osm-selected',
            filter: ['==', ['geometry-type'], 'Polygon'],
            paint: { 'fill-color': '#0055ff', 'fill-opacity': 0.1 }
        });

        // Selected highlight, area outline (drawn above fill)
        map.addLayer({
            id: 'layer-selected-polygon-outline',
            type: 'line',
            source: 'osm-selected',
            filter: ['==', ['geometry-type'], 'Polygon'],
            paint: { 'line-color': '#0055ff', 'line-width': 3 }
        });

        // Selected highlight, standalone node
        map.addLayer({
            id: 'layer-selected-nodes',
            type: 'circle',
            source: 'osm-selected',
            filter: ['==', ['geometry-type'], 'Point'],
            paint: {
                'circle-radius': 8,
                'circle-color': '#0055ff',
                'circle-stroke-color': '#fff',
                'circle-stroke-width': 2
            }
        });

        // Way vertex handles (shown when a way is selected), draggable
        map.addLayer({
            id: 'layer-vertices',
            type: 'circle',
            source: 'osm-vertices',
            paint: {
                'circle-radius': 6,
                'circle-color': '#fff',
                'circle-stroke-color': '#0055ff',
                'circle-stroke-width': 2
            }
        });

        // OSM Notes markers
        map.addLayer({
            id: 'layer-notes',
            type: 'circle',
            source: 'osm-notes',
            paint: {
                'circle-radius': 8,
                'circle-color': '#f90',
                'circle-stroke-color': '#fff',
                'circle-stroke-width': 2
            }
        });

        // Invalid polygon highlight, shown during drag when shape would self-intersect
        map.addLayer({
            id: 'layer-invalid-fill',
            type: 'fill',
            source: 'osm-invalid',
            paint: { 'fill-color': '#ff0000', 'fill-opacity': 0.25 }
        });
        map.addLayer({
            id: 'layer-invalid-outline',
            type: 'line',
            source: 'osm-invalid',
            paint: { 'line-color': '#cc0000', 'line-width': 2, 'line-dasharray': [4, 3] }
        });

        // GPX tracks
        map.addLayer({
            id: 'layer-gpx',
            type: 'line',
            source: 'osm-gpx',
            paint: { 'line-color': '#c00', 'line-width': 2 }
        });

        // Gridify preview: orange dashed grid overlay shown while the gridify dialog is open
        map.addLayer({
            id: 'layer-gridify-preview-line',
            type: 'line',
            source: 'osm-gridify-preview',
            paint: {
                'line-color': '#ff6600',
                'line-width': 1.5,
                'line-dasharray': [4, 3]
            }
        });

        // Draw preview, hover highlight — same style as layer-hover, shown on mouseover
        map.addLayer({
            id: 'layer-draw-preview-hover',
            type: 'line',
            source: 'osm-draw-preview',
            filter: ['==', ['geometry-type'], 'LineString'],
            layout: { visibility: 'none' },
            paint: { 'line-color': '#4af', 'line-width': 6, 'line-opacity': 0.5 }
        });

        // Draw preview, dashed rubber-band line while drawing a way
        map.addLayer({
            id: 'layer-draw-preview-line',
            type: 'line',
            source: 'osm-draw-preview',
            filter: ['==', ['geometry-type'], 'LineString'],
            paint: {
                'line-color': '#08f',
                'line-width': 2,
                'line-dasharray': [4, 3]
            }
        });

        // Draw preview, non-terminal vertex dots (small blue)
        map.addLayer({
            id: 'layer-draw-preview-points',
            type: 'circle',
            source: 'osm-draw-preview',
            filter: ['all', ['==', ['geometry-type'], 'Point'], ['!', ['has', 'terminal']]],
            paint: {
                'circle-radius': 4,
                'circle-color': '#08f',
                'circle-stroke-color': '#fff',
                'circle-stroke-width': 2
            }
        });

        // Draw preview, terminal vertex (large green — click here to finish drawing)
        map.addLayer({
            id: 'layer-draw-preview-terminal',
            type: 'circle',
            source: 'osm-draw-preview',
            filter: ['all', ['==', ['geometry-type'], 'Point'], ['has', 'terminal']],
            paint: {
                'circle-radius': 6,
                'circle-color': '#00cc55',
                'circle-stroke-color': '#fff',
                'circle-stroke-width': 2
            }
        });
    }

    // URL hash: #map=zoom/lat/lon (same format as iD).
    // Pass a string to parse instead of window.location.hash.
    function parseHashPosition(str) {
        const match = (str ?? window.location.hash).match(/[#&]map=([0-9.]+)\/(-?[0-9.]+)\/(-?[0-9.]+)/);
        if (!match)
        {
            return null;
        }

        const zoom = parseFloat(match[1]);
        const lat  = parseFloat(match[2]);
        const lon  = parseFloat(match[3]);
        if (isNaN(zoom) || isNaN(lat) || isNaN(lon))
        {
            return null;
        }

        return { zoom, lat, lon };
    }

    function loadSavedPosition() {
        try {
            return parseHashPosition(localStorage.getItem('alidade_last_pos') ?? '');
        } catch {
            return null;
        }
    }

    function updateHash() {
        const center = map.getCenter();
        const zoom   = map.getZoom();
        const hash   = `#map=${zoom.toFixed(2)}/${center.lat.toFixed(6)}/${center.lng.toFixed(6)}`;
        history.replaceState(null, '', hash);
        try { localStorage.setItem('alidade_last_pos', hash); } catch { /* storage unavailable */ }
    }

    function notifyMoveEnd() {
        updateHash();
        if (!dotnetRef)
        {
            return;
        }

        const b = map.getBounds();
        const z = map.getZoom();
        dotnetRef.invokeMethodAsync('OnMapMoveEnd',
            b.getWest(), b.getSouth(), b.getEast(), b.getNorth(), z
        ).catch(console.error);
    }

    function setupDragHandlers() {
        // Minimum pixel movement before a mousedown is promoted to a real drag.
        // Below this threshold the interaction is treated as a click, not a move.
        const DRAG_THRESHOLD_PX = 4;

        // Start drag on regular nodes or way vertex handles
        function startDrag(e) {
            e.preventDefault();
            const feature = e.features && e.features[0];
            if (!feature){
                return;
            }

            // Normalize to string, MapLibre may coerce numeric properties to numbers.
            dragNodeId = feature.properties?.id != null
                ? String(feature.properties.id)
                : null;

            if (!dragNodeId)
            {
                return;
            }

            // Record starting position so we can revert if the drag creates an invalid shape.
            dragOriginalPos = feature.geometry.coordinates.slice();
            dragStartPixel = { x: e.point.x, y: e.point.y };

            // isDragging is intentionally NOT set here, it is activated in mousemove
            // once the cursor has moved far enough, preventing a bare click from
            // triggering OnNodeDragEnd and moving the node.
            map.getCanvas().style.cursor = 'grabbing';
            map.dragPan.disable();

            // Populate snap-segments with all non-adjacent segments for pixel-exact way snap.
            // Adjacent segments (those that include the dragged node) are excluded so that
            // queryRenderedFeatures on layer-snap-segments-hit can only return segments the
            // cursor is physically over, with zero distance math.
            const nodePos = {};
            for (const cache of [sourceCache['osm-nodes'], sourceCache['osm-vertices']]) {
                for (const nf of (cache?.features ?? [])) {
                    const nid = String(nf.properties?.id ?? '');
                    if (nid)
                    {
                        nodePos[nid] = nf.geometry.coordinates;
                    }
                }
            }

            const snapFeatures = [];
            for (const wayFeature of (sourceCache['osm-ways']?.features ?? [])) {
                const wayId = String(wayFeature.properties?.id ?? '');
                if (!wayId)
                {
                    continue;
                }

                const wayNodeIds = (wayFeature.properties?.nodeIds ?? '').split(',').filter(Boolean);
                for (let i = 0; i < wayNodeIds.length - 1; i++) {
                    const nidA = wayNodeIds[i];
                    const nidB = wayNodeIds[i + 1];
                    if (nidA === dragNodeId || nidB === dragNodeId)
                    {
                        continue;
                    }

                    const posA = nodePos[nidA];
                    const posB = nodePos[nidB];
                    if (!posA || !posB)
                    {
                        continue;
                    }

                    snapFeatures.push({
                        type: 'Feature',
                        geometry: { type: 'LineString', coordinates: [posA, posB] },
                        properties: { wayId, nodeA: nidA, nodeB: nidB }
                    });
                }
            }

            const snapSegSrc = map.getSource('osm-snap-segments');
            if (snapSegSrc) {
                const fc = { type: 'FeatureCollection', features: snapFeatures };
                sourceCache['osm-snap-segments'] = fc;
                snapSegSrc.setData(fc);
            }
        }

        map.on('mousedown', 'layer-nodes',    startDrag);
        map.on('mousedown', 'layer-vertices', startDrag);

        // Mousemove, update node/way positions live and check for self-intersecting polygons.
        map.on('mousemove', e => {
            if (!dragNodeId)
            {
                return;
            }

            // Promote a pending mousedown to an active drag once the cursor has moved
            // far enough from the initial click point.
            if (!isDragging)
            {
                if (!dragStartPixel)
                {
                    return;
                }

                const dx = e.point.x - dragStartPixel.x;
                const dy = e.point.y - dragStartPixel.y;
                if (Math.sqrt(dx * dx + dy * dy) < DRAG_THRESHOLD_PX)
                {
                    return;
                }

                isDragging = true;
            }

            const { lat, lng } = e.lngLat;

            // Build a nodeId -> [lng, lat] lookup from all node caches, with the dragged
            // node at the current cursor position.  Done once so way rebuilds are consistent.
            const nodePos = {};
            for (const cache of [sourceCache['osm-nodes'], sourceCache['osm-vertices']])
            {
                if (!cache?.features)
                {
                    continue;
                }

                for (const nf of cache.features)
                {
                    nodePos[String(nf.properties?.id)] = nf.geometry.coordinates;
                }
            }

            nodePos[dragNodeId] = [lng, lat];

            function updateNodeSource(srcId) {
                const src = map.getSource(srcId);
                if (!src)
                {
                    return;
                }

                const data = sourceCache[srcId];
                if (!data?.features)
                {
                    return;
                }

                const updated = {
                    ...data,
                    features: data.features.map(f =>
                        String(f.properties?.id) === dragNodeId
                            ? { ...f, geometry: { type: 'Point', coordinates: [lng, lat] } }
                            : f
                    )
                };
                sourceCache[srcId] = updated;
                src.setData(updated);
            }

            // Rebuild way/polygon coordinates from nodePos in OSM order.
            function updateWaySource(srcId) {
                const src = map.getSource(srcId);
                if (!src)
                {
                    return;
                }

                const data = sourceCache[srcId];
                if (!data?.features)
                {
                    return;
                }

                const updated = {
                    ...data,
                    features: data.features.map(f => {
                        let nodeIds = f.properties?.nodeIds?.split(',') ?? [];

                        // osm-hover and osm-selected may be stale when a node was just
                        // inserted into a way: PushGeoJsonAsync writes osm-ways first, but
                        // PushSelectionAsync writes the selection sources later and may not
                        // have reached osm-hover before the drag started. Fall back to the
                        // corresponding way in osm-ways (which is always fresh) to get the
                        // up-to-date nodeIds list.
                        if (!nodeIds.includes(dragNodeId) && f.geometry?.type !== 'Point') {
                            const wayId = f.properties?.id;
                            const freshWay = sourceCache['osm-ways']?.features?.find(
                                wf => String(wf.properties?.id) === String(wayId)
                            );
                            if (freshWay?.properties?.nodeIds) {
                                nodeIds = freshWay.properties.nodeIds.split(',');
                            }
                        }

                        if (!nodeIds.includes(dragNodeId))
                        {
                            return f;
                        }

                        const newCoords = [];
                        for (const nid of nodeIds) {
                            const pos = nodePos[nid];
                            if (!pos)
                            {
                                return f;
                            }

                            newCoords.push(pos);
                        }

                        const geom = f.geometry;
                        if (geom.type === 'LineString') {
                            return { ...f, geometry: { type: 'LineString', coordinates: newCoords } };
                        }
                        if (geom.type === 'Polygon') {
                            return { ...f, geometry: { type: 'Polygon', coordinates: [newCoords] } };
                        }
                        return f;
                    })
                };
                sourceCache[srcId] = updated;
                src.setData(updated);
            }

            // Check for self-intersection BEFORE updating sources, using the candidate
            // coordinates computed directly from nodePos+nodeIds. This avoids any issue
            // with GeoJSON winding-order normalisation applied by NTS during serialisation
            // (which can make geom.coordinates[0] disagree with the nodeIds order).
            // We only check osm-ways, osm-selected contains the same way features and
            // checking both would produce duplicate results.
            const invalidFeatures = [];
            const waysData = sourceCache['osm-ways'];
            if (waysData?.features) {
                for (const f of waysData.features) {
                    // Only closed polygon features are candidates for self-intersection.
                    if (f.geometry.type !== 'Polygon')
                    {
                        continue;
                    }

                    const nodeIds = f.properties?.nodeIds?.split(',') ?? [];
                    if (!nodeIds.includes(dragNodeId))
                    {
                        continue;
                    }

                    // Build candidate ring in OSM (nodeIds) order from nodePos.
                    const candidateRing = [];
                    let complete = true;
                    for (const nid of nodeIds) {
                        const pos = nodePos[nid];
                        if (!pos) { complete = false; break; }
                        candidateRing.push(pos);
                    }

                    if (!complete)
                    {
                        continue;
                    }

                    if (ringSelfIntersects(candidateRing, nodePos[dragNodeId])) {
                        invalidFeatures.push({
                            ...f,
                            geometry: { type: 'Polygon', coordinates: [candidateRing] }
                        });
                    }
                }
            }

            updateNodeSource('osm-nodes');
            updateNodeSource('osm-vertices');
            updateNodeSource('osm-hover');
            updateNodeSource('osm-selected');
            updateWaySource('osm-ways');
            updateWaySource('osm-selected');
            updateWaySource('osm-hover');

            const invalidSrc = map.getSource('osm-invalid');
            if (invalidSrc) {
                const fc = { type: 'FeatureCollection', features: invalidFeatures };
                sourceCache['osm-invalid'] = fc;
                invalidSrc.setData(fc);
            }

            isDragInvalid = invalidFeatures.length > 0;
        });

        // Mouseup, revert if invalid, commit otherwise.
        map.on('mouseup', e => {
            if (!dragNodeId)
            {
                return;
            }// no pending or active drag

            const wasDragging = isDragging;
            const { lat, lng } = e.lngLat;
            const nodeId = `node/${dragNodeId}`;
            const savedId = dragNodeId;
            const savedOriginalPos = dragOriginalPos;
            const wasInvalid = isDragInvalid;

            isDragging = false;
            isDragInvalid = false;
            dragNodeId = null;
            dragOriginalPos = null;
            dragStartPixel = null;
            const toolCursors = { select: '', drawNode: 'crosshair', drawWay: 'crosshair', drawArea: 'crosshair' };
            map.getCanvas().style.cursor = toolCursors[activeTool] ?? '';
            map.dragPan.enable();

            // Clear snap-segments regardless of drag outcome.
            const snapSegSrc = map.getSource('osm-snap-segments');
            if (snapSegSrc) {
                const emptySnapSeg = { type: 'FeatureCollection', features: [] };
                sourceCache['osm-snap-segments'] = emptySnapSeg;
                snapSegSrc.setData(emptySnapSeg);
            }

            if (!wasDragging) {
                // The mouse never moved past the drag threshold, treat as a click,
                // do not call OnNodeDragEnd so the node position is not changed.
                return;
            }

            // Always clear the invalid highlight.
            const invalidSrc = map.getSource('osm-invalid');
            if (invalidSrc) {
                const empty = { type: 'FeatureCollection', features: [] };
                sourceCache['osm-invalid'] = empty;
                invalidSrc.setData(empty);
            }

            if (wasInvalid) {
                // Revert node sources back to original position.
                const [origLng, origLat] = savedOriginalPos;
                for (const srcId of ['osm-nodes', 'osm-vertices', 'osm-hover']) {
                    const src = map.getSource(srcId);
                    const data = sourceCache[srcId];
                    if (!src || !data?.features)
                    {
                        continue;
                    }

                    const reverted = {
                        ...data,
                        features: data.features.map(f =>
                            String(f.properties?.id) === savedId
                                ? { ...f, geometry: { type: 'Point', coordinates: [origLng, origLat] } }
                                : f
                        )
                    };
                    sourceCache[srcId] = reverted;
                    src.setData(reverted);
                }

                // Rebuild way sources from the reverted node positions.
                const nodePos = {};
                for (const cache of [sourceCache['osm-nodes'], sourceCache['osm-vertices']])
                {
                    if (!cache?.features)
                    {
                        continue;
                    }

                    for (const nf of cache.features)
                    {
                        nodePos[String(nf.properties?.id)] = nf.geometry.coordinates;
                    }
                }

                for (const srcId of ['osm-ways', 'osm-selected', 'osm-hover'])
                {
                    const src = map.getSource(srcId);
                    const data = sourceCache[srcId];
                    if (!src || !data?.features)
                    {
                        continue;
                    }

                    const reverted = {
                        ...data,
                        features: data.features.map(f => {
                            const nodeIds = f.properties?.nodeIds?.split(',') ?? [];
                            if (!nodeIds.includes(savedId))
                            {
                                return f;
                            }

                            const newCoords = nodeIds.map(nid => nodePos[nid]).filter(Boolean);
                            if (newCoords.length < 2)
                            {
                                return f;
                            }

                            const geom = f.geometry;
                            if (geom.type === 'LineString') {
                                return { ...f, geometry: { type: 'LineString', coordinates: newCoords } };
                            }
                            if (geom.type === 'Polygon') {
                                return { ...f, geometry: { type: 'Polygon', coordinates: [newCoords] } };
                            }
                            return f;
                        })
                    };
                    sourceCache[srcId] = reverted;
                    src.setData(reverted);
                }
                return; // do not notify C#
            }

            if (dotnetRef) {
                // Node-to-node merge: pixel hit-test on node/vertex layers.
                const nodeSnapLayers = ['layer-nodes', 'layer-vertices'].filter(id => map.getLayer(id));
                const nodeSnapFeatures = nodeSnapLayers.length > 0
                    ? map.queryRenderedFeatures(e.point, { layers: nodeSnapLayers })
                    : [];
                const snapTarget = nodeSnapFeatures
                    .map(f => elementIdFromFeature(f))
                    .find(id => id && id !== nodeId) ?? null;

                // Node-to-way snap: pixel hit-test on the snap-segments layer.
                // Each entry is a non-adjacent segment pre-populated at drag start, so
                // queryRenderedFeatures returns only segments the cursor is actually over.
                let waySnapTarget = null;
                let waySnapSegmentA = null;
                let waySnapSegmentB = null;
                if (!snapTarget) {
                    const snapSegLayers = ['layer-snap-segments-hit'].filter(id => map.getLayer(id));
                    const snapSegFeatures = snapSegLayers.length > 0
                        ? map.queryRenderedFeatures(e.point, { layers: snapSegLayers })
                        : [];
                    const snapSeg = snapSegFeatures[0] ?? null;
                    if (snapSeg) {
                        waySnapTarget = `way/${snapSeg.properties.wayId}`;
                        waySnapSegmentA = String(snapSeg.properties.nodeA);
                        waySnapSegmentB = String(snapSeg.properties.nodeB);
                    }
                }

                dotnetRef.invokeMethodAsync('OnNodeDragEnd', nodeId, lat, lng, snapTarget, waySnapTarget, waySnapSegmentA, waySnapSegmentB)
                    .catch(console.error);
            }
        });

        // Cursor feedback for clickable/draggable layers.
        // In select mode: show a regular arrow when hovering any element (nodes, vertices, ways).
        // In draw modes: keep the crosshair, never let hover override it.
        const clickableLayers = ['layer-nodes', 'layer-vertices', 'layer-ways-hit'];
        for (const layer of clickableLayers) {
            map.on('mouseenter', layer, () => {
                if (isDragging)
                {
                    return;
                }

                if (activeTool === 'select') {
                    map.getCanvas().style.cursor = 'default';
                }
                // In draw modes the crosshair set by setActiveTool is left unchanged.
            });
            map.on('mouseleave', layer, () => {
                if (isDragging)
                {
                    return;
                }

                if (activeTool === 'select') {
                    map.getCanvas().style.cursor = ''; // revert to MapLibre's default grab
                }
                // In draw modes the crosshair is left unchanged.
            });
        }
    }

    const BING_KEY = 'Auk3J0jR9g1_PVQgdmL95zCOKVOc8g-FGq5Zgb5ik7w1Ri5SRyWILV-kksgbw-Gh';

    async function _initBingDefault() {
        try {
            const source = await fetchBingSource(BING_KEY);
            _applyBackgroundSource(source);
        } catch (e) {
            console.warn('Bing default imagery failed to load:', e);
        }
    }

    function _applyBackgroundSource(sourceConfig) {
        if (map.getLayer('background-imagery-layer')) {
            map.removeLayer('background-imagery-layer');
        }

        if (map.getSource('background-imagery')) {
            map.removeSource('background-imagery');
        }

        if (!sourceConfig)
        {
            return;
        }

        map.addSource('background-imagery', sourceConfig);
        const layers = map.getStyle().layers;
        const insertBeforeId = layers.find(l => l.type !== 'background')?.id;
        map.addLayer({
            id: 'background-imagery-layer',
            type: 'raster',
            source: 'background-imagery'
        }, insertBeforeId);
    }

    // Normalizes a keyboard event into the same canonical combo string as
    // KeyBindingCatalog.NormalizeCombo on the C# side (e.g. "ctrl+z", "delete", "shift+w").
    function normalizeCombo(e) {
        const key = e.key.toLowerCase();
        if (key === 'control' || key === 'shift' || key === 'alt' || key === 'meta')
        {
            return '';
        }

        const parts = [];
        if (e.ctrlKey) parts.push('ctrl');
        if (e.altKey)  parts.push('alt');
        if (e.shiftKey) parts.push('shift');
        parts.push(key);
        return parts.join('+');
    }

    // Combos whose browser/OS defaults we always want to suppress while the editor is active
    // (Ctrl+Z browser undo, Ctrl+S save-dialog, backspace/delete browser navigation, etc.).
    const SUPPRESS_DEFAULTS = new Set([
        'ctrl+z', 'ctrl+y', 'ctrl+shift+z', 'ctrl+s',
        'backspace', 'delete', '/', 'escape'
    ]);

    return {
        initialize(containerId, _styleUrl, ref) {
            dotnetRef = ref;

            // Global keyboard handler: fires for all keydowns outside text inputs so that
            // shortcuts work regardless of which element MapLibre has focused.
            document.addEventListener('keydown', e => {
                const activeEl = document.activeElement;
                const tag = activeEl?.tagName?.toLowerCase();

                if (tag === 'input' || tag === 'textarea' || tag === 'select'
                    || activeEl?.isContentEditable) {
                    return;
                }

                if (!dotnetRef) {
                    return;
                }

                const combo = normalizeCombo(e);
                if (!combo) {
                    return;
                }

                const suppress = SUPPRESS_DEFAULTS.has(combo);
                if (suppress)
                {
                    e.preventDefault();
                }

                dotnetRef.invokeMethodAsync('OnKeyDown', combo)
                    .then(() => console.debug('[keydown] OnKeyDown completed for combo=%s', combo))
                    .catch(err => console.error('[keydown] OnKeyDown failed for combo=%s:', combo, err));
            });

            // Minimal blank style, no vector base tiles. All map content comes
            // from the raster imagery layer and our own OSM GeoJSON layers.
            const blankStyle = {
                version: 8,
                glyphs: '/assets/omt-fonts/{fontstack}/{range}.pbf',
                sources: {},
                layers: [{ id: 'background', type: 'background', paint: { 'background-color': '#e8e0d8' } }]
            };

            const saved = parseHashPosition() ?? loadSavedPosition();
            map = new maplibregl.Map({
                container: containerId,
                style: blankStyle,
                center: saved ? [saved.lon, saved.lat] : [0, 20],
                zoom:   saved ? saved.zoom              : 2,
                attributionControl: false
            });

            map.addControl(new maplibregl.AttributionControl({ compact: true }), 'bottom-right');
            map.addControl(new maplibregl.NavigationControl(), 'top-right');
            map.addControl(new maplibregl.ScaleControl(), 'bottom-left');

            map.on('load', () => {
                addOsmSources();
                addOsmLayers();
                setupDragHandlers();
                notifyMoveEnd();
                // Load Bing as the default background imagery
                _initBingDefault();
            });

            // Debounced moveend, 200 ms
            map.on('moveend', () => {
                clearTimeout(moveEndTimer);
                moveEndTimer = setTimeout(notifyMoveEnd, 200);
            });

            // Click, hit-test OSM feature layers, then notify C#
            map.on('click', e => {
                if (isDragging || !dotnetRef)
                {
                    return;
                }
                // Priority order:
                //  - terminal draw-preview vertex (finish gesture)
                //  - existing vertices
                //  - existing ways
                //  - existing nodes/notes
                //  - in-progress rubber-band line (self-intersection)
                const queryLayers = [
                    'layer-draw-preview-terminal',
                    'layer-vertices', 'layer-ways-hit', 'layer-nodes', 'layer-notes',
                    'layer-draw-preview-line'
                ].filter(id => map.getLayer(id));
                const features = queryLayers.length > 0
                    ? map.queryRenderedFeatures(e.point, { layers: queryLayers })
                    : [];
                let elementId = null;
                if (features.length > 0)
                {
                    const top = features[0];
                    if (top.layer.id === 'layer-draw-preview-terminal')
                    {
                        elementId = 'draw-preview-terminal';
                    }
                    else if (top.layer.id === 'layer-draw-preview-line')
                    {
                        elementId = 'draw-preview-line';
                    }
                    else
                    {
                        elementId = elementIdFromFeature(top);
                    }
                }
                dotnetRef.invokeMethodAsync('OnMapClick',
                    e.lngLat.lat, e.lngLat.lng,
                    e.point.x, e.point.y,
                    elementId,
                    e.originalEvent?.shiftKey ?? false
                ).catch(console.error);
            });

            // Double-click: insert node into way. Prevent MapLibre's default zoom when
            // the click lands on a way feature so the map doesn't jump.
            map.on('dblclick', e => {
                if (!dotnetRef)
                {
                    return;
                }
                const queryLayers = ['layer-ways-hit'].filter(id => map.getLayer(id));
                const features = queryLayers.length > 0
                    ? map.queryRenderedFeatures(e.point, { layers: queryLayers })
                    : [];
                if (features.length === 0)
                {
                    return;
                }
                e.preventDefault(); // suppress MapLibre zoom
                const elementId = elementIdFromFeature(features[0]);
                dotnetRef.invokeMethodAsync('OnMapDblClick',
                    e.lngLat.lat, e.lngLat.lng,
                    e.point.x, e.point.y,
                    elementId
                ).catch(console.error);
            });

            // Hover, debounced 16 ms. Only notifies .NET when the hovered element changes.
            // Also handles draw-preview-line hover locally (no C# round-trip needed).
            map.on('mousemove', e => {
                if (isDragging)
                {
                    return;
                }
                clearTimeout(hoverTimer);
                hoverTimer = setTimeout(() => {
                    // Draw-preview line hover: toggle the highlight layer locally.
                    const previewHoverLayer = map.getLayer('layer-draw-preview-hover');
                    const previewLineLayer  = map.getLayer('layer-draw-preview-line');
                    if (previewHoverLayer && previewLineLayer)
                    {
                        const overLine = map.queryRenderedFeatures(e.point, { layers: ['layer-draw-preview-line'] }).length > 0;
                        map.setLayoutProperty('layer-draw-preview-hover', 'visibility', overLine ? 'visible' : 'none');
                    }

                    if (!dotnetRef)
                    {
                        return;
                    }

                    const queryLayers = ['layer-ways-hit', 'layer-nodes']
                        .filter(id => map.getLayer(id));
                    const features = queryLayers.length > 0
                        ? map.queryRenderedFeatures(e.point, { layers: queryLayers })
                        : [];
                    const elementId = features.length > 0
                        ? elementIdFromFeature(features[0])
                        : null;
                    if (elementId === lastHoveredId)
                    {
                        return;
                    }
                    lastHoveredId = elementId;
                    dotnetRef.invokeMethodAsync('OnHover', elementId).catch(console.error);
                }, 16);
            });

            map.on('mouseleave', () => {
                if (map.getLayer('layer-draw-preview-hover'))
                {
                    map.setLayoutProperty('layer-draw-preview-hover', 'visibility', 'none');
                }
                if (dotnetRef && lastHoveredId !== null) {
                    lastHoveredId = null;
                    dotnetRef.invokeMethodAsync('OnHover', null).catch(console.error);
                }
            });
        },

        setSourceData(sourceId, geojsonString) {
            if (!map)
            {
                return;
            }
            const parsed = JSON.parse(geojsonString);
            sourceCache[sourceId] = parsed;   // keep cache in sync
            if (sourceId === 'osm-hover' || sourceId === 'osm-nodes' || sourceId === 'osm-selected') {
                const pts = parsed.features
                    ?.filter(f => f.geometry?.type === 'Point')
                    .map(f => {
                        const [lng, lat] = f.geometry.coordinates;
                        return `node/${f.properties?.id}@(${lat.toFixed(8)},${lng.toFixed(8)})`;
                    }) ?? [];
            }
            const src = map.getSource(sourceId);
            if (src)
            {
                src.setData(parsed);
            }
        },

        flyTo(lat, lon, zoom) {
            if (!map)
            {
                return;
            }
            map.flyTo({ center: [lon, lat], zoom });
        },

        panTo(lat, lon) {
            if (!map)
            {
                return;
            }
            map.panTo([lon, lat]);
        },

        getBounds() {
            if (!map)
            {
                return null;
            }

            const b = map.getBounds();
            return {
                west: b.getWest(), south: b.getSouth(),
                east: b.getEast(), north: b.getNorth(),
                zoom: map.getZoom()
            };
        },

        project(lat, lon) {
            if (!map)
            {
                return { x: 0, y: 0 };
            }
            const p = map.project([lon, lat]);
            return { x: p.x, y: p.y };
        },

        setActiveTool(toolName) {
            if (!map)
            {
                return;
            }
            activeTool = toolName;
            const cursors = {
                select: '',
                drawNode: 'crosshair',
                drawWay: 'crosshair',
                drawArea: 'crosshair'
            };
            map.getCanvas().style.cursor = cursors[toolName] ?? '';
        },

        setLayerVisibility(layerId, visible) {
            if (!map)
            {
                return;
            }
            map.setLayoutProperty(layerId, 'visibility', visible ? 'visible' : 'none');
        },

        setBackgroundImagery(tiles, tileSize, attribution, tmsScheme, maxZoom) {
            if (!map)
            {
                return;
            }
            _applyBackgroundSource({
                type: 'raster',
                tiles: tiles,
                tileSize: tileSize || 256,
                maxzoom: maxZoom || 19,
                attribution: attribution || '',
                scheme: tmsScheme ? 'tms' : 'xyz'
            });
        },

        async setBackgroundBing() {
            if (!map)
            {
                return;
            }

            try {
                const source = await fetchBingSource(BING_KEY);
                _applyBackgroundSource(source);
            } catch (e) {
                console.warn('Bing imagery failed to load:', e);
            }
        },

        clearBackgroundImagery() {
            if (!map)
            {
                return;
            }

            _applyBackgroundSource(null);
        }
    };
})();
