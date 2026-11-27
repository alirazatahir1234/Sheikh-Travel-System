import { convertShape, shapeAt } from './geofence-editor.component';
import { GeofenceShape } from '../geofence.models';
import { shapeMetrics } from '../geofence-geometry.util';

const SQUARE: GeofenceShape = {
  areaType: 'polygon',
  path: [{ lat: 31.5, lng: 74.3 }, { lat: 31.5, lng: 74.302 }, { lat: 31.498, lng: 74.302 }, { lat: 31.498, lng: 74.3 }]
};

describe('geofence editor shape switching', () => {
  it('keeps the drawing when switching between polygon, rectangle and circle', () => {
    const rect = convertShape(SQUARE, 'rectangle')!;
    expect(rect.areaType).toBe('rectangle');
    expect(rect.path!.length).toBe(4);

    const circle = convertShape(SQUARE, 'circle')!;
    expect(circle.areaType).toBe('circle');
    // Same area as the square.
    expect(Math.abs(shapeMetrics(circle).areaSqMeters - shapeMetrics(SQUARE).areaSqMeters) / shapeMetrics(SQUARE).areaSqMeters).toBeLessThan(0.01);

    const back = convertShape(circle, 'polygon')!;
    expect(back.areaType).toBe('polygon');
    expect(back.path!.length).toBe(16);
  });

  it('starts over for corridors, and leaves an unchanged type alone', () => {
    expect(convertShape(SQUARE, 'corridor')).toBeNull();
    expect(convertShape({ areaType: 'corridor', path: SQUARE.path!.slice(0, 2), corridorWidthMeters: 100 }, 'polygon')).toBeNull();
    expect(convertShape(SQUARE, 'polygon')).toBe(SQUARE);
    expect(convertShape(null, 'circle')).toBeNull();
  });

  it('drops a starting shape at a searched place', () => {
    expect(shapeAt({ lat: 31.5, lng: 74.3 }, 'circle')).toEqual({ areaType: 'circle', center: { lat: 31.5, lng: 74.3 }, radiusMeters: 250 });
    const square = shapeAt({ lat: 31.5, lng: 74.3 }, 'polygon')!;
    expect(square.path!.length).toBe(4);
    expect(shapeMetrics(square).perimeterMeters).toBeGreaterThan(1150);
    expect(shapeMetrics(square).perimeterMeters).toBeLessThan(1250);
    expect(shapeAt({ lat: 31.5, lng: 74.3 }, 'corridor')).toBeNull();
  });
});
