plugins {
    id("com.android.library")
    id("org.jetbrains.kotlin.android") version "2.1.0"
}

android {
    namespace = "io.colorgarden.mdl.runtime"
    compileSdk = 36
    buildToolsVersion = "36.0.0"

    defaultConfig {
        minSdk = 24
        targetSdk = 36
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        buildConfig = true
    }
}

dependencies {
    // Arc framework — Android backend
    implementation("com.github.Anuken.Arc:backends:backend-android:master-SNAPSHOT")
    implementation("com.github.Anuken.Arc:natives-android:master-SNAPSHOT")
    implementation("com.github.Anuken.Arc:natives-freetype-android:master-SNAPSHOT")

    // Runtime DEX generation (for loading mod jars at runtime)
    implementation("com.jakewharton.android.repackaged:dalvik-dx:9.0.0_r3")

    // AndroidX
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
}
