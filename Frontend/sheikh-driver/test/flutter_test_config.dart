import 'dart:async';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:hive/hive.dart';

/// Tests must not fetch fonts over the network.
/// Login reads the prefs box the same way the app does after Hive.openBox.
Future<void> testExecutable(FutureOr<void> Function() testMain) async {
  TestWidgetsFlutterBinding.ensureInitialized();
  GoogleFonts.config.allowRuntimeFetching = false;
  final prefsDir = Directory.systemTemp.createTempSync('sg-driver-prefs');
  Hive.init(prefsDir.path);
  await Hive.openBox('prefs');
  await testMain();
}
