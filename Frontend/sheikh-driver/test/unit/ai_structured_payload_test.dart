import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/ai/domain/ai_structured_payload.dart';

void main() {
  test('parses fleet_status_analytics summary and vehicle alerts', () {
    const json = '''
{
  "responseType": "fleet_status_analytics",
  "summary": {
    "total": 4,
    "online": 2,
    "offline": 0,
    "moving": 1,
    "idle": 1,
    "parked": 0
  },
  "vehicles": [
    {
      "registrationNumber": "BTL-482",
      "gpsBucket": "moving",
      "speed": 120,
      "lastUpdate": "2 minutes ago"
    }
  ],
  "acein": { "answer": "Overall status: All vehicles are operational." }
}
''';
    final data = parseAiStructuredPayload(json);
    expect(data, isNotNull);
    expect(data!.fleetStatus?.total, 4);
    expect(data.fleetStatus?.online, 2);
    expect(data.alerts, isNotEmpty);
    expect(data.alerts.first.title.toLowerCase(), contains('speed'));
  });

}
