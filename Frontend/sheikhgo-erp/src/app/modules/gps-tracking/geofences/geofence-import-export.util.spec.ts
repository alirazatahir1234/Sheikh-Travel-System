import {
  MAX_IMPORT_ITEMS,
  detectFormat,
  parseCsvRows,
  parseGeofenceFile,
  toCsv,
  toGeoJson,
  toKml
} from './geofence-import-export.util';
import { ExportableGeofence } from './geofence-import-export.util';

const square = JSON.stringify({
  type: 'Feature',
  properties: {},
  geometry: { type: 'Polygon', coordinates: [[[74.3, 31.5], [74.31, 31.5], [74.31, 31.51], [74.3, 31.51], [74.3, 31.5]]] }
});

const SAVED: ExportableGeofence[] = [
  { name: 'Pasrur Head Office', type: 'Office', color: '#0f766e', areaType: 'polygon', centerLat: 31.505, centerLng: 74.305, radiusMeters: 0, geoJson: square, groupName: 'Depots' },
  { name: 'Awan Villas, Gate 2', type: 'Customer', color: '#2563eb', areaType: 'circle', centerLat: 32.1, centerLng: 74.2, radiusMeters: 250, address: 'Gate "2", Sialkot' },
  {
    name: 'GT Road', type: 'Route corridor', color: '#7c3aed', areaType: 'corridor', centerLat: 31.55, centerLng: 74.35, radiusMeters: 0,
    corridorWidthMeters: 200, geoJson: JSON.stringify({ type: 'LineString', coordinates: [[74.3, 31.5], [74.4, 31.6]] })
  }
];

describe('geofence import and export', () => {
  it('detects the format from the name, then from the content', () => {
    expect(detectFormat('zones.KML', '')).toBe('kml');
    expect(detectFormat('zones.geojson', '')).toBe('geojson');
    expect(detectFormat('zones.csv', '')).toBe('csv');
    expect(detectFormat('zones', '  {"type":"FeatureCollection"}')).toBe('geojson');
    expect(detectFormat('zones', '<kml>')).toBe('kml');
    expect(detectFormat('zones', 'plain words')).toBeNull();
  });

  for (const [format, write] of [['geojson', toGeoJson], ['kml', toKml], ['csv', toCsv]] as const) {
    it(`round-trips circles, polygons and corridors through ${format}`, () => {
      const text = write(SAVED);
      const parsed = parseGeofenceFile(`export.${format}`, text);
      expect(parsed.error).toBeNull();
      const payloads = parsed.items.map(i => i.payload!);
      expect(parsed.items.every(i => !i.error)).toBeTrue();
      expect(payloads.map(p => p.name)).toEqual(['Pasrur Head Office', 'Awan Villas, Gate 2', 'GT Road']);
      expect(payloads.map(p => p.areaType)).toEqual(['polygon', 'circle', 'corridor']);
      expect(payloads[1].radiusMeters).toBe(250);
      expect(payloads[1].centerLat).toBeCloseTo(32.1, 6);
      expect(payloads[2].corridorWidthMeters).toBe(200);
      // Imports apply to all vehicles and start without alerts.
      expect(payloads.every(p => p.appliesTo === 'all' && !p.alertOnEnter && !p.alertOnExit && p.isActive)).toBeTrue();
    });
  }

  it('turns plain GeoJSON points and lines into circles and corridors with defaults, and warns', () => {
    const text = JSON.stringify({
      type: 'FeatureCollection',
      features: [
        { type: 'Feature', properties: { name: 'Pump' }, geometry: { type: 'Point', coordinates: [74.3, 31.5] } },
        { type: 'Feature', properties: { name: 'Route', width: 300 }, geometry: { type: 'LineString', coordinates: [[74.3, 31.5], [74.4, 31.5]] } },
        { type: 'Feature', properties: {}, geometry: { type: 'Polygon', coordinates: [[[74.3, 31.5], [74.31, 31.5], [74.31, 31.51], [74.3, 31.5]]] } },
        { type: 'Feature', properties: { name: 'Broken' }, geometry: { type: 'Polygon', coordinates: [[[74.3, 31.5]]] } }
      ]
    });
    const { items, error } = parseGeofenceFile('mixed.json', text);
    expect(error).toBeNull();
    expect(items[0].payload!.areaType).toBe('circle');
    expect(items[0].payload!.radiusMeters).toBe(100);
    expect(items[0].warnings.join(' ')).toContain('No radius');
    expect(items[1].payload!.corridorWidthMeters).toBe(300);
    expect(items[2].payload!.name).toBe('Feature 3');
    expect(items[2].warnings.join(' ')).toContain('No name');
    expect(items[3].payload).toBeNull();
    expect(items[3].error).toContain('No usable shape');
  });

  it('renames repeated names within one file and refuses oversized files', () => {
    const point = (name: string) => ({ type: 'Feature', properties: { name }, geometry: { type: 'Point', coordinates: [74.3, 31.5] } });
    const dup = parseGeofenceFile('d.geojson', JSON.stringify({ type: 'FeatureCollection', features: [point('Depot'), point('depot')] }));
    expect(dup.items[1].payload!.name).toBe('depot (2)');

    const many = Array.from({ length: MAX_IMPORT_ITEMS + 1 }, (_, i) => point(`Zone ${i}`));
    const big = parseGeofenceFile('big.geojson', JSON.stringify({ type: 'FeatureCollection', features: many }));
    expect(big.error).toContain(`at most ${MAX_IMPORT_ITEMS}`);
  });

  it('reads KML placemarks with polygons, lines and points', () => {
    const kml = `<?xml version="1.0"?><kml xmlns="http://www.opengis.net/kml/2.2"><Document>
      <Placemark><name>Yard</name><Polygon><outerBoundaryIs><LinearRing><coordinates>
        74.3,31.5,0 74.31,31.5,0 74.31,31.51,0 74.3,31.5,0
      </coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
      <Placemark><name>Lane</name><LineString><coordinates>74.3,31.5 74.4,31.6</coordinates></LineString></Placemark>
      <Placemark><name>Gate</name><ExtendedData><Data name="radiusMeters"><value>80</value></Data></ExtendedData>
        <Point><coordinates>74.35,31.55,0</coordinates></Point></Placemark>
    </Document></kml>`;
    const { items, error } = parseGeofenceFile('x.kml', kml);
    expect(error).toBeNull();
    expect(items.map(i => i.payload!.areaType)).toEqual(['polygon', 'corridor', 'circle']);
    expect(items[2].payload!.radiusMeters).toBe(80);
    expect(parseGeofenceFile('bad.kml', '<kml><Placemark>').error).toContain('not valid XML');
  });

  it('parses CSV with quotes, commas and new lines inside fields', () => {
    expect(parseCsvRows('a,"b, c","d ""e"""\r\n1,"two\nlines",3\n')).toEqual([
      ['a', 'b, c', 'd "e"'],
      ['1', 'two\nlines', '3']
    ]);
    const csv = toCsv(SAVED);
    expect(csv.split('\r\n')[0]).toBe('name,type,shape,center_lat,center_lng,radius_m,width_m,points,address,color,group');
    expect(csv).toContain('"Awan Villas, Gate 2"');
    expect(csv).toContain('"Gate ""2"", Sialkot"');
  });

  it('escapes names in KML', () => {
    const kml = toKml([{ ...SAVED[1], name: 'A & B <depot>' }]);
    expect(kml).toContain('<name>A &amp; B &lt;depot&gt;</name>');
  });
});
