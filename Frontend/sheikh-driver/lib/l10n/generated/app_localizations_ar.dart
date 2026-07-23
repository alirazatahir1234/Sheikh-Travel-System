// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Arabic (`ar`).
class AppLocalizationsAr extends AppLocalizations {
  AppLocalizationsAr([String locale = 'ar']) : super(locale);

  @override
  String get appTitle => 'شيخ جو فليت';

  @override
  String get settings => 'الإعدادات';

  @override
  String get account => 'الحساب';

  @override
  String get appearance => 'المظهر';

  @override
  String get security => 'الأمان';

  @override
  String get activity => 'النشاط';

  @override
  String get appSection => 'التطبيق';

  @override
  String get darkMode => 'الوضع الداكن';

  @override
  String get darkModeSubtitle => 'التبديل إلى المظهر الداكن';

  @override
  String get language => 'اللغة';

  @override
  String get languageSubtitle => 'لغة عرض التطبيق';

  @override
  String get languageEnglish => 'الإنجليزية';

  @override
  String get languageArabic => 'العربية';

  @override
  String get biometricLock => 'قفل بيومتري';

  @override
  String get biometricLockSubtitle =>
      'يتطلب Face ID / بصمة الإصبع لفتح التطبيق';

  @override
  String get biometricUnavailable =>
      'القياسات الحيوية غير متاحة على هذا الجهاز';

  @override
  String get biometricEnableFailed => 'تعذر تفعيل القفل البيومتري';

  @override
  String get unlockTitle => 'فتح شيخ جو فليت';

  @override
  String get unlockSubtitle => 'قم بالمصادقة للمتابعة';

  @override
  String get unlockAction => 'فتح';

  @override
  String get unlockSignOut => 'تسجيل الخروج';

  @override
  String get offlineQueue => 'قائمة دون اتصال';

  @override
  String get offlineQueueSubtitle => 'المزامنة المعلقة والتعارضات';

  @override
  String get offlineBannerOffline => 'أنت غير متصل';

  @override
  String offlineBannerQueued(int count) {
    return '$count في الانتظار';
  }

  @override
  String offlineBannerPending(int count) {
    return '$count إجراء بانتظار المزامنة';
  }

  @override
  String get sync => 'مزامنة';

  @override
  String get syncNow => 'مزامنة الآن';

  @override
  String syncedItems(int count) {
    return 'تمت مزامنة $count عنصر';
  }

  @override
  String get online => 'متصل';

  @override
  String get offline => 'غير متصل';

  @override
  String get offlineOnlineHint => 'تتم مزامنة الإجراءات تلقائياً عند الاتصال';

  @override
  String get offlineOfflineHint => 'تُحفظ الإجراءات محلياً حتى يعود الاتصال';

  @override
  String pendingSection(int count) {
    return 'قيد الانتظار ($count)';
  }

  @override
  String get nothingPending => 'لا يوجد شيء بانتظار المزامنة.';

  @override
  String get conflictsSection => 'تعارضات (تحتاج مراجعة)';

  @override
  String get conflictsHint =>
      'رفضها الخادم بعد إعادة الاتصال. احذفها بعد التحقق من حالة النظام.';

  @override
  String get retry => 'إعادة المحاولة';

  @override
  String get signOut => 'تسجيل الخروج';

  @override
  String get signOutConfirmTitle => 'تسجيل الخروج';

  @override
  String get signOutConfirmBody => 'هل أنت متأكد أنك تريد تسجيل الخروج؟';

  @override
  String get cancel => 'إلغاء';

  @override
  String environmentLabel(String env) {
    return 'البيئة: $env';
  }
}
