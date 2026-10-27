/** Friendly, widely recognized emojis for SheikhGo AI Assistant UI copy. */
export const AI_ASSISTANT_UI = {
  wave: '👋',
  smile: '😊',
  thumbsUp: '👍',
  chat: '💬',
  search: '🔍',
  fleet: '🚗',
  idea: '💡'
} as const;

/** Swap decorative / niche emojis in assistant text for common friendly ones. */
const ASSISTANT_EMOJI_REPLACEMENTS: Readonly<Record<string, string>> = {
  '🤖': AI_ASSISTANT_UI.smile,
  '🧠': AI_ASSISTANT_UI.smile,
  '✨': AI_ASSISTANT_UI.thumbsUp,
  '🌟': AI_ASSISTANT_UI.smile,
  '🚀': AI_ASSISTANT_UI.thumbsUp,
  '🎯': AI_ASSISTANT_UI.thumbsUp,
  '⚡': AI_ASSISTANT_UI.thumbsUp,
  '🎉': AI_ASSISTANT_UI.thumbsUp,
  '🛠️': AI_ASSISTANT_UI.thumbsUp,
  '🛠': AI_ASSISTANT_UI.thumbsUp,
  '🔧': AI_ASSISTANT_UI.thumbsUp,
  '📊': AI_ASSISTANT_UI.idea,
  '✅': AI_ASSISTANT_UI.thumbsUp,
  '❌': '👎',
  '⚠️': '⚠️',
  '🙌': AI_ASSISTANT_UI.thumbsUp,
  '🙂': AI_ASSISTANT_UI.smile
};

export function polishAssistantEmojis(text: string | null | undefined): string {
  const raw = text ?? '';
  if (!raw) {
    return '';
  }
  let out = raw;
  for (const [from, to] of Object.entries(ASSISTANT_EMOJI_REPLACEMENTS)) {
    if (out.includes(from)) {
      out = out.split(from).join(to);
    }
  }
  return out;
}
