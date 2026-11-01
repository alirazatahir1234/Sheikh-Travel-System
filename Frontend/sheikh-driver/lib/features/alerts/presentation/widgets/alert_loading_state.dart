import 'package:flutter/material.dart';

/// Skeleton placeholders matching Fleet Alert chips + cards (Image A).
class AlertLoadingState extends StatelessWidget {
  const AlertLoadingState({
    super.key,
    this.count = 4,
    this.showChrome = true,
  });

  final int count;
  final bool showChrome;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
      children: [
        if (showChrome) ...[
          const _Bone(width: double.infinity, height: 46, radius: 14),
          const SizedBox(height: 12),
          const SizedBox(
            height: 40,
            child: Row(
              children: [
                _Bone(width: 108, height: 36, radius: 20),
                SizedBox(width: 8),
                _Bone(width: 112, height: 36, radius: 20),
                SizedBox(width: 8),
                _Bone(width: 88, height: 36, radius: 20),
              ],
            ),
          ),
          const SizedBox(height: 14),
        ],
        for (var i = 0; i < count; i++) const _SkeletonCard(),
      ],
    );
  }
}

class _SkeletonCard extends StatefulWidget {
  const _SkeletonCard();

  @override
  State<_SkeletonCard> createState() => _SkeletonCardState();
}

class _SkeletonCardState extends State<_SkeletonCard>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1100),
    )..repeat(reverse: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final t = 0.45 + (_controller.value * 0.35);
        return Opacity(opacity: t, child: child);
      },
      child: const Padding(
        padding: EdgeInsets.only(bottom: 12),
        child: SizedBox(
          height: 112,
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.all(Radius.circular(18)),
              boxShadow: [
                BoxShadow(
                  color: Color(0x0A000000),
                  blurRadius: 8,
                  offset: Offset(0, 2),
                ),
              ],
            ),
            child: Row(
              children: [
                SizedBox(
                  width: 6,
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      color: Color(0xFFE2E8F0),
                      borderRadius: BorderRadius.only(
                        topLeft: Radius.circular(18),
                        bottomLeft: Radius.circular(18),
                      ),
                    ),
                  ),
                ),
                Expanded(
                  child: Padding(
                    padding: EdgeInsets.fromLTRB(12, 14, 12, 14),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        _Bone(width: 38, height: 38, radius: 19),
                        SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: _Bone(
                                      width: double.infinity,
                                      height: 14,
                                    ),
                                  ),
                                  SizedBox(width: 12),
                                  _Bone(width: 72, height: 10),
                                ],
                              ),
                              SizedBox(height: 10),
                              _Bone(width: double.infinity, height: 12),
                              SizedBox(height: 6),
                              _Bone(width: 160, height: 12),
                              Spacer(),
                              Row(
                                children: [
                                  _Bone(width: 88, height: 24, radius: 12),
                                  SizedBox(width: 8),
                                  _Bone(width: 110, height: 24, radius: 12),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _Bone extends StatelessWidget {
  const _Bone({
    required this.width,
    required this.height,
    this.radius = 6,
  });

  final double width;
  final double height;
  final double radius;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: width == double.infinity ? null : width,
      height: height,
      constraints: width == double.infinity
          ? const BoxConstraints(minWidth: 40)
          : null,
      decoration: BoxDecoration(
        color: const Color(0xFFE2E8F0),
        borderRadius: BorderRadius.circular(radius),
      ),
    );
  }
}
