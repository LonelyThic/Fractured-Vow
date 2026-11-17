using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FracturedVow.EditorTools
{
    /// <summary>
    /// Tools > Fractured Vow > Prepare Release Build
    /// Makes the built game look and behave like a finished product: its name and version, the app icon,
    /// fullscreen borderless by default (resizable window allowed), keeps running in the background,
    /// and both floors in the build in order. Run it once before File > Build Profiles > Build.
    /// </summary>
    public static class ReleaseSetup
    {
        [MenuItem("Tools/Fractured Vow/Prepare Release Build")]
        static void Run() => Prepare(true);

        static void Prepare(bool showReport)
        {
            var log = new List<string>();
            PlayerSettings.productName = "Fractured Vow";
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
                PlayerSettings.companyName = "Fractured Vow";
            if (PlayerSettings.bundleVersion == "0.1" || string.IsNullOrEmpty(PlayerSettings.bundleVersion)) PlayerSettings.bundleVersion = "1.0.0";
            log.Add("Name: " + PlayerSettings.productName + "  v" + PlayerSettings.bundleVersion + "  (" + PlayerSettings.companyName + ")");

            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            log.Add("Display: fullscreen borderless by default; resizable when windowed (players can change it in Video settings).");

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FracturedVow/Resources/FVUI/Icon.png");
            if (icon != null)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath("Assets/FracturedVow/Resources/FVUI/Icon.png");
                if (imp != null && (!imp.isReadable || imp.textureCompression != TextureImporterCompression.Uncompressed))
                {
                    imp.isReadable = true;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.SaveAndReimport();
                }
                PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                log.Add("Icon: set from Resources/FVUI/Icon.png");
            }
            else log.Add("Icon: Resources/FVUI/Icon.png not found, skipped.");

            // both floors in the build, Floor 1 first
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string f1 = FindScene("Floor1_WardensPit"), f2 = FindScene("Floor2");
            var order = new List<EditorBuildSettingsScene>();
            if (f1 != null) order.Add(new EditorBuildSettingsScene(f1, true));
            if (f2 != null) order.Add(new EditorBuildSettingsScene(f2, true));
            foreach (var s in scenes) if (order.All(o => o.path != s.path)) order.Add(s);
            EditorBuildSettings.scenes = order.ToArray();
            log.Add("Scenes in build: " + string.Join(", ", order.Where(s => s.enabled).Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))));

            AssetDatabase.SaveAssets();
            if (showReport) EditorUtility.DisplayDialog("Prepare Release Build", string.Join("\n\n", log) +
                "\n\nNext: File > Build Profiles > Windows > Build.", "OK");
        }

        /// <summary>
        /// Tools > Fractured Vow > Build Web Demo (WebGL)
        /// Builds the browser version for GitHub Pages: product name and icon (Prepare Release Build), Gzip
        /// compression with decompression fallback (GitHub Pages can't send the headers compressed files need),
        /// browser caching, textures capped at 1024 for a smaller download, and a .nojekyll file. Pick the
        /// "docs" folder inside your GitHub repository as the output.
        /// </summary>
        [MenuItem("Tools/Fractured Vow/Build Web Demo (WebGL)")]
        static void BuildWeb()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog("Build Web Demo",
                    "WebGL Build Support isn't installed.\n\nUnity Hub > Installs > the gear next to your Unity 6.3 > Add modules > " +
                    "WebGL Build Support, then restart Unity and run this again.", "OK");
                return;
            }
            string folder = EditorUtility.SaveFolderPanel("Choose the 'docs' folder inside your GitHub repository", "", "docs");
            if (string.IsNullOrEmpty(folder)) return;
            if (System.IO.Directory.Exists(folder) && System.IO.Directory.GetFileSystemEntries(folder).Length > 0 &&
                !System.IO.File.Exists(System.IO.Path.Combine(folder, "index.html")) &&
                !EditorUtility.DisplayDialog("Build Web Demo", folder + "\n\nisn't empty and doesn't look like an older build. Build into it anyway?", "Build", "Cancel"))
                return;

            Prepare(false); // name, version, icon, scenes in order
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;   // GitHub Pages: the page unpacks the files itself
            PlayerSettings.WebGL.dataCaching = true;             // the second visit loads almost instantly
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.runInBackground = true;
            int oldMax = EditorUserBuildSettings.overrideMaxTextureSize;
            EditorUserBuildSettings.overrideMaxTextureSize = 1024; // smaller download; looks the same at web resolution

            var scenes = new List<string>();
            foreach (var sc in EditorBuildSettings.scenes) if (sc.enabled) scenes.Add(sc.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = folder,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            });
            EditorUserBuildSettings.overrideMaxTextureSize = oldMax;

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Build Web Demo", "The build failed (" + report.summary.totalErrors + " errors). See the Console.", "OK");
                return;
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, ".nojekyll"), ""); // GitHub Pages: serve every file as-is

            // GitHub refuses files over 100 MB
            string biggest = ""; long size = 0, total = 0;
            foreach (var f in System.IO.Directory.GetFiles(folder, "*", System.IO.SearchOption.AllDirectories))
            {
                long len = new System.IO.FileInfo(f).Length;
                total += len;
                if (len > size) { size = len; biggest = System.IO.Path.GetFileName(f); }
            }
            string sizes = "Total: " + (total / 1048576f).ToString("0") + " MB.  Largest file: " + biggest + " (" + (size / 1048576f).ToString("0") + " MB).";
            if (size > 100L * 1024 * 1024)
                EditorUtility.DisplayDialog("Build Web Demo", "Built, but " + biggest + " is over GitHub's 100 MB limit.\n\n" + sizes +
                    "\n\nSwitch Compression Format to Brotli (Project Settings > Player > WebGL > Publishing Settings) and build again, " +
                    "or ask Claude to trim the build.", "OK");
            else
                EditorUtility.DisplayDialog("Build Web Demo", "Done! " + sizes + "\n\nNext: commit and push the docs folder with GitHub Desktop.", "OK");
            EditorUtility.RevealInFinder(System.IO.Path.Combine(folder, "index.html"));
        }

        static string FindScene(string nameStart)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.StartsWith("Assets/")) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(p).StartsWith(nameStart)) return p;
            }
            return null;
        }
    }
}
