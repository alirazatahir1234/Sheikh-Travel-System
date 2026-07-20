# SheikhGo Driver — ProGuard / R8 keep rules
-keep class io.flutter.** { *; }
-keep class com.sheikhgo.driver.** { *; }
-dontwarn com.google.firebase.**
-keepattributes *Annotation*
-keepattributes SourceFile,LineNumberTable
-keep public class * extends java.lang.Exception
