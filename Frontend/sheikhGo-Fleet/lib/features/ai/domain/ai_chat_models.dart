class AiChatMessage {
  const AiChatMessage({required this.role, required this.content});

  final String role;
  final String content;

  bool get isUser => role.toLowerCase() == 'user';
  bool get isAssistant => role.toLowerCase() == 'assistant';

  factory AiChatMessage.fromJson(Map<String, dynamic> json) => AiChatMessage(
        role: json['role']?.toString() ?? json['Role']?.toString() ?? '',
        content: json['content']?.toString() ?? json['Content']?.toString() ?? '',
      );
}

class AiChatSession {
  const AiChatSession({
    required this.id,
    required this.title,
    required this.createdAt,
    required this.updatedAt,
    required this.messageCount,
  });

  final String id;
  final String title;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final int messageCount;

  factory AiChatSession.fromJson(Map<String, dynamic> json) => AiChatSession(
        id: json['id']?.toString() ?? json['Id']?.toString() ?? '',
        title: json['title']?.toString() ?? json['Title']?.toString() ?? 'Chat',
        createdAt: DateTime.tryParse(
          json['createdAt']?.toString() ?? json['CreatedAt']?.toString() ?? '',
        ),
        updatedAt: DateTime.tryParse(
          json['updatedAt']?.toString() ?? json['UpdatedAt']?.toString() ?? '',
        ),
        messageCount: (json['messageCount'] as num?)?.toInt() ??
            (json['MessageCount'] as num?)?.toInt() ??
            0,
      );
}

class AiChatTurnResponse {
  const AiChatTurnResponse({
    required this.sessionId,
    required this.answer,
    required this.mode,
    required this.usedLlm,
    required this.provider,
    this.model,
    this.suggestedPrompts = const [],
    this.toolsUsed = const [],
  });

  final String sessionId;
  final String answer;
  final String mode;
  final bool usedLlm;
  final String provider;
  final String? model;
  final List<String> suggestedPrompts;
  final List<String> toolsUsed;

  bool get pendingConfirmation =>
      toolsUsed.any((t) => t.toLowerCase() == 'pending_confirm');

  List<String> get displayTools => toolsUsed
      .where((t) => !const {'llm_chat', 'llm_fallback', 'pending_confirm'}
          .contains(t.toLowerCase()))
      .toList();

  factory AiChatTurnResponse.fromJson(Map<String, dynamic> json) {
    List<String> strings(dynamic v) {
      if (v is! List) return const [];
      return v.map((e) => e.toString()).where((s) => s.isNotEmpty).toList();
    }

    return AiChatTurnResponse(
      sessionId:
          json['sessionId']?.toString() ?? json['SessionId']?.toString() ?? '',
      answer: json['answer']?.toString() ?? json['Answer']?.toString() ?? '',
      mode: json['mode']?.toString() ?? json['Mode']?.toString() ?? 'rules',
      usedLlm: json['usedLlm'] == true || json['UsedLlm'] == true,
      provider:
          json['provider']?.toString() ?? json['Provider']?.toString() ?? 'None',
      model: json['model']?.toString() ?? json['Model']?.toString(),
      suggestedPrompts: strings(
        json['suggestedPrompts'] ?? json['SuggestedPrompts'],
      ),
      toolsUsed: strings(json['toolsUsed'] ?? json['ToolsUsed']),
    );
  }
}

class AiProviderHealth {
  const AiProviderHealth({
    required this.provider,
    this.model,
    this.endpoint,
    required this.configured,
    required this.reachable,
    required this.statusMessage,
  });

  final String provider;
  final String? model;
  final String? endpoint;
  final bool configured;
  final bool reachable;
  final String statusMessage;

  factory AiProviderHealth.fromJson(Map<String, dynamic> json) =>
      AiProviderHealth(
        provider:
            json['provider']?.toString() ?? json['Provider']?.toString() ?? 'None',
        model: json['model']?.toString() ?? json['Model']?.toString(),
        endpoint: json['endpoint']?.toString() ?? json['Endpoint']?.toString(),
        configured: json['configured'] == true || json['Configured'] == true,
        reachable: json['reachable'] == true || json['Reachable'] == true,
        statusMessage: json['statusMessage']?.toString() ??
            json['StatusMessage']?.toString() ??
            '',
      );
}

class AiToolInfo {
  const AiToolInfo({
    required this.name,
    required this.description,
    required this.kind,
    required this.requiresConfirmation,
  });

  final String name;
  final String description;
  final String kind;
  final bool requiresConfirmation;

  factory AiToolInfo.fromJson(Map<String, dynamic> json) => AiToolInfo(
        name: json['name']?.toString() ?? json['Name']?.toString() ?? '',
        description:
            json['description']?.toString() ?? json['Description']?.toString() ?? '',
        kind: json['kind']?.toString() ?? json['Kind']?.toString() ?? 'read',
        requiresConfirmation: json['requiresConfirmation'] == true ||
            json['RequiresConfirmation'] == true,
      );
}

class AiChatBubble {
  const AiChatBubble({
    required this.role,
    required this.content,
    this.mode,
    this.usedLlm = false,
    this.tools = const [],
    this.pendingConfirmation = false,
  });

  final String role;
  final String content;
  final String? mode;
  final bool usedLlm;
  final List<String> tools;
  final bool pendingConfirmation;

  bool get isUser => role == 'user';
}

const defaultAiSuggestions = [
  'How healthy is my fleet today?',
  'Which vehicles are offline?',
  'Show critical GPS alerts',
  'What maintenance is overdue?',
  'Summarize driver risk this week',
];
