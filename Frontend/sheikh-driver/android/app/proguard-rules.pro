# SheikhGo Fleet — ProGuard / R8 keep rules
-keep class io.flutter.** { *; }
-keep class com.sheikhgo.fleet.** { *; }
-dontwarn com.google.firebase.**
-dontwarn com.google.android.play.core.**
-keepattributes *Annotation*
-keepattributes SourceFile,LineNumberTable
-keep public class * extends java.lang.Exception
