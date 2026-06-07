using System;
using System.Collections.Generic;
using Dusk;

namespace Dawn.Editor.ReleaseBuild;

public class ThunderstoreManifest(DuskModInformation modInfo)
{
    public string name { get; private set; } = modInfo.ModName;
    public string version_number { get; private set; } = modInfo.Version;
    public string description { get; private set; } = modInfo.ModDescription;
    public string website_url { get; private set; } = modInfo.WebsiteUrl;
    public List<string> dependencies { get; private set; } = MakeSureDependenciesWorks(modInfo.ExtraDependencies);

    private static List<string> MakeSureDependenciesWorks(List<string> extraDependencies)
    {
        List<string> dependencies = new();
        foreach (string dependency in extraDependencies)
        {
            if (string.IsNullOrEmpty(dependency))
                continue;

            if (dependency.Contains("TeamXiaolan-DawnLib", StringComparison.OrdinalIgnoreCase))
                continue;

            if (dependency.Contains("BepInEx-BepInExPack", StringComparison.OrdinalIgnoreCase))
                continue;

            dependencies.Add(dependency);
        }
        dependencies.Add("BepInEx-BepInExPack-5.4.2100");
        dependencies.Add($"TeamXiaolan-DawnLib-{Dawn.MyPluginInfo.PLUGIN_VERSION}");
        return dependencies;
    }
}