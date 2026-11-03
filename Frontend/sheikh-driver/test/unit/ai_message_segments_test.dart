import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/ai/presentation/ai_message_segments.dart';

void main() {
  test('plain text is one non-preformatted segment', () {
    final s = splitAiMessage('Hello fleet');
    expect(s, hasLength(1));
    expect(s.single.preformatted, isFalse);
    expect(s.single.text, 'Hello fleet');
  });

  test('markdown table and code fence become preformatted blocks', () {
    const msg = 'Vehicles:\n| Plate | Status |\n|---|---|\n| ABC | Online |\n'
        'Done.\n```\nselect 1\n```';
    final s = splitAiMessage(msg);
    expect(s.map((e) => e.preformatted), [false, true, false, true]);
    expect(s[1].text, contains('| ABC | Online |'));
    expect(s[3].text, 'select 1');
  });

  test('unterminated fence keeps content', () {
    final s = splitAiMessage('```\ncode');
    expect(s.single.preformatted, isTrue);
    expect(s.single.text, 'code');
  });

  test('empty content falls back to raw', () {
    expect(splitAiMessage('').single.text, '');
  });
}
