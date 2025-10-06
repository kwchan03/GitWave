// OpenTapBootstrap.cs
using OpenTap;
using System.IO;

public static class OpenTapBootstrap
{
    private static bool _initialized;

    /// <summary>
    /// Initialize OpenTAP plugin discovery so TestPlan.Load / TapSerializer can resolve step types.
    /// Call this once before using TestPlan or TapSerializer.
    /// </summary>
    public static void Init(string? openTapRoot = null, params string[] extraPluginDirs)
    {
        if (_initialized) return;

        // 1) Add the folder where OpenTap.dll resides (NuGet copies it to your bin folder)
        var bin = Path.GetDirectoryName(typeof(TestPlan).Assembly.Location);
        if (!string.IsNullOrEmpty(bin))
            PluginManager.DirectoriesToSearch.Add(bin);

        // 2) Common NuGet payload layout: a "Packages" subfolder next to your bin
        var pkgs = Path.Combine(bin ?? "", "Packages");
        if (Directory.Exists(pkgs))
            PluginManager.DirectoriesToSearch.Add(pkgs);

        // 3) (Optional) If you know a full OpenTAP installation root, add it + its Packages
        if (!string.IsNullOrWhiteSpace(openTapRoot) && Directory.Exists(openTapRoot))
        {
            PluginManager.DirectoriesToSearch.Add(openTapRoot);
            var installPkgs = Path.Combine(openTapRoot, "Packages");
            if (Directory.Exists(installPkgs))
                PluginManager.DirectoriesToSearch.Add(installPkgs);
        }

        // 4) (Optional) Any extra plugin folders you ship
        if (extraPluginDirs != null)
        {
            foreach (var dir in extraPluginDirs)
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    PluginManager.DirectoriesToSearch.Add(dir);
        }

        // 5) Initialize + scan
        PluginManager.Initialize();
        PluginManager.SearchAsync().Wait();

        _initialized = true;
    }
}
