/// A run of assistant text; [preformatted] runs (``` fences or markdown
/// tables) are rendered monospace with horizontal scroll.
class AiMessageSegment {
  const AiMessageSegment(this.text, {this.preformatted = false});
  final String text;
  final bool preformatted;
}

List<AiMessageSegment> splitAiMessage(String content) {
  final out = <AiMessageSegment>[];
  final buf = <String>[];
  var bufPre = false;

  void flush() {
    if (buf.isEmpty) return;
    final text = buf.join('\n');
    if (text.trim().isNotEmpty) {
      out.add(AiMessageSegment(
        bufPre ? text : text.trim(),
        preformatted: bufPre,
      ));
    }
    buf.clear();
  }

  var inFence = false;
  for (final line in content.split('\n')) {
    final trimmed = line.trimLeft();
    if (trimmed.startsWith('```')) {
      flush();
      inFence = !inFence;
      bufPre = inFence;
      continue;
    }
    if (inFence) {
      buf.add(line);
      continue;
    }
    final isTableRow = trimmed.startsWith('|') && trimmed.length > 1;
    if (isTableRow != bufPre) {
      flush();
      bufPre = isTableRow;
    }
    buf.add(line);
  }
  flush();

  return out.isEmpty ? [AiMessageSegment(content)] : out;
}
