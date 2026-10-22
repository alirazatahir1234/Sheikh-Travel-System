// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Arabic (`ar`).
class AppLocalizationsAr extends AppLocalizations {
  AppLocalizationsAr([String locale = 'ar']) : super(locale);

  @override
  String get appTitle => 'شيخ جو للتتبع';

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
  String get unlockTitle => 'فتح شيخ جو للتتبع';

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
  String get retryAllFailed => 'إعادة محاولة الكل';

  @override
  String get retryStarted => 'بدأت إعادة المحاولة — تحقق من العناصر المتبقية';

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

  @override
  String get changePassword => 'تغيير كلمة المرور';

  @override
  String get pushNotifications => 'إشعارات الدفع';

  @override
  String get pushNotificationsOn => 'تنبيهات الرحلات ورسائل الإرسال';

  @override
  String get pushNotificationsOff => 'متوقف — لن تستلم تنبيهات الدفع';

  @override
  String get dismissWarning => 'تجاهل التحذير';

  @override
  String get securityStatus => 'حالة الأمان';

  @override
  String get securityStatusSubtitle => 'الجذر، كسر الحماية، المحاكي، التثبيت';

  @override
  String get backgroundGps => 'GPS في الخلفية';

  @override
  String get backgroundGpsSubtitle =>
      'إيقاف التتبع المباشر / فتح الخريطة المباشرة';

  @override
  String get backgroundGpsStopped => 'تم إيقاف GPS في الخلفية';

  @override
  String get activityTimeline => 'الجدول الزمني للنشاط';

  @override
  String get activityTimelineSubtitle => 'عرض سجل الرحلات والحضور';

  @override
  String get attendance => 'الحضور';

  @override
  String get attendanceSubtitle => 'تسجيل الدخول / تسجيل الخروج';

  @override
  String get privacyPolicy => 'سياسة الخصوصية';

  @override
  String get termsOfService => 'شروط الخدمة';

  @override
  String get version => 'الإصدار';

  @override
  String get checkForUpdates => 'التحقق من التحديثات';

  @override
  String get openInBrowser => 'فتح في المتصفح';

  @override
  String get viewHostedVersion => 'عرض النسخة المستضافة';

  @override
  String legalEffectiveDate(String date) {
    return 'ساري من $date';
  }

  @override
  String get termsShort => 'الشروط';

  @override
  String get privacyShort => 'الخصوصية';

  @override
  String get clockedInSuccess => 'تم تسجيل الحضور بنجاح';

  @override
  String get clockedOutSuccess => 'تم تسجيل الانصراف بنجاح';

  @override
  String get noAttendanceRecords => 'لا توجد سجلات حضور';

  @override
  String get checkIn => 'تسجيل حضور';

  @override
  String get checkOut => 'تسجيل انصراف';

  @override
  String get checkInCaps => 'تسجيل حضور';

  @override
  String get checkOutCaps => 'تسجيل انصراف';

  @override
  String get checkedIn => 'تم الحضور';

  @override
  String get notCheckedIn => 'لم يتم الحضور';

  @override
  String get stopTrackingTitle => 'إيقاف GPS في الخلفية؟';

  @override
  String get stopTrackingBody =>
      'سيتوقف مشاركة الموقع المباشر مع الإرسال حتى تعيد التتبع.';

  @override
  String get stop => 'إيقاف';
}
