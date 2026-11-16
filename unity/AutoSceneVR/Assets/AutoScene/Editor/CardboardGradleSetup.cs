using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace AutoSceneVR.Editor
{
    // Add the SDK's documented Java dependencies to our exported Gradle project.
    // This changes only this build's generated output, never another Unity installation.
    public class CardboardGradleSetup : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string gradle = Path.Combine(path, "build.gradle");
            string text = File.ReadAllText(gradle);
            if (!text.Contains("play-services-vision")) {
                text += "\ndependencies {\n"
                    + "    implementation 'androidx.appcompat:appcompat:1.6.1'\n"
                    + "    implementation 'com.google.android.gms:play-services-vision:20.1.3'\n"
                    + "    implementation 'com.google.android.material:material:1.12.0'\n"
                    + "    implementation 'com.google.protobuf:protobuf-javalite:3.19.4'\n}\n";
                File.WriteAllText(gradle, text);
            }
            string root = Directory.GetParent(path).FullName;
            string properties = Path.Combine(root, "gradle.properties");
            string props = File.Exists(properties) ? File.ReadAllText(properties) : "";
            if (!props.Contains("android.useAndroidX=")) props += "\nandroid.useAndroidX=true\n";
            if (!props.Contains("android.enableJetifier=")) props += "android.enableJetifier=true\n";
            File.WriteAllText(properties, props);
            Debug.Log("AUTOSCENE_CARDBOARD_GRADLE_READY");
        }
    }
}
