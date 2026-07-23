import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/ai/domain/ai_chat_models.dart';

void main() {
  test('AiChatTurnResponse parses gateway payload and pending confirm', () {
    final r = AiChatTurnResponse.fromJson({
      'sessionId': '11111111-1111-1111-1111-111111111111',
      'answer': 'Ready to assign driver 5 to booking 12.',
      'mode': 'tools_only',
      'usedLlm': false,
      'provider': 'Ollama',
      'model': 'mistral',
      'suggestedPrompts': ['Show offline vehicles'],
      'toolsUsed': ['AssignDriver', 'pending_confirm', 'llm_fallback'],
    });

    expect(r.sessionId, contains('1111'));
    expect(r.pendingConfirmation, isTrue);
    expect(r.displayTools, ['AssignDriver']);
    expect(r.suggestedPrompts, hasLength(1));
  });

  test('AiProviderHealth parses status strip fields', () {
    final h = AiProviderHealth.fromJson({
      'provider': 'Ollama',
      'model': 'mistral',
      'endpoint': 'http://127.0.0.1:11434',
      'configured': true,
      'reachable': false,
      'statusMessage': 'Cannot reach Ollama.',
    });
    expect(h.configured, isTrue);
    expect(h.reachable, isFalse);
  });

  test('AiChatSession and message parse history payloads', () {
    final s = AiChatSession.fromJson({
      'id': 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
      'title': 'Fleet health',
      'createdAt': '2026-07-21T10:00:00Z',
      'updatedAt': '2026-07-21T10:05:00Z',
      'messageCount': 4,
    });
    final m = AiChatMessage.fromJson({'role': 'assistant', 'content': 'OK'});
    expect(s.messageCount, 4);
    expect(m.isAssistant, isTrue);
  });
}
