# SheikhGo Fleet — ProGuard / R8 keep rules
-keep class io.flutter.** { *; }
-keep class com.sheikhgo.fleet.** { *; }
-dontwarn com.google.firebase.**
# Flutter embedding references Play Core split-install APIs; unused without deferred components.
-dontwarn com.google.android.play.core.**
# Analytics ADID is disabled in the manifest; Measurement may still reference these classes.
-dontwarn com.google.android.gms.ads.identifier.**
-keepattributes *Annotation*
-keepattributes SourceFile,LineNumberTable
-keep public class * extends java.lang.Exception
