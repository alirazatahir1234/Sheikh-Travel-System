import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive/hive.dart';

final darkModeProvider = StateProvider<bool>((ref) => false);

final prefsBoxProvider = Provider<Box>((_) => Hive.box('prefs'));
