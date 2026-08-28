allprojects {
    repositories {
        google()
        mavenCentral()
    }
}

val newBuildDir: Directory =
    rootProject.layout.buildDirectory
        .dir("../../build")
        .get()
rootProject.layout.buildDirectory.value(newBuildDir)

subprojects {
    val newSubprojectBuildDir: Directory = newBuildDir.dir(project.name)
    project.layout.buildDirectory.value(newSubprojectBuildDir)
}
subprojects {
    project.evaluationDependsOn(":app")
}

// Flutter's integration_test plugin declares dynamic AndroidX Test versions
// (e.g. androidx.test:runner:1.2+). That forces Gradle to fetch
// maven-metadata.xml; Maven Central does not host AndroidX Test (404), which
// surfaces as "Failed to list versions for androidx.test:runner". Pin fixed
// versions so resolution does not need metadata listing.
subprojects {
    configurations.configureEach {
        resolutionStrategy {
            force("androidx.test:runner:1.7.0")
            force("androidx.test:rules:1.7.0")
            force("androidx.test.espresso:espresso-core:3.7.0")
            force("androidx.test:core:1.7.0")
        }
    }
}

tasks.register<Delete>("clean") {
    delete(rootProject.layout.buildDirectory)
}
