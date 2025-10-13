using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Dawn.Utils;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.EditorWindows;
public class DuskEntityReplacementBaker : EditorWindow
{
    private UnityEngine.Object? source;
    private MonoScript? chosenClass;
    private string outputFolder = "";
    private string authorName = "placeholder";

    private string BaseAssemblyNamePrefix = "com.local.placeholder.DuskReplacementEntities";
    private const string TargetFramework = "netstandard2.1";

    [MenuItem("DawnLib/Dusk/Entity Replacement Baker/Bake Definition")]
    private static void Open()
    {
        GetWindow<DuskEntityReplacementBaker>("Dusk Entity Replacement Baker");
    }

    private void OnGUI()
    {
        source = EditorGUILayout.ObjectField("Source (AI)", source, typeof(MonoScript), false);
        chosenClass = source as MonoScript;

        if (source == null)
            return;

        if (chosenClass == null)
        {
            EditorGUILayout.HelpBox($"No MonoBehaviours found on this Object {source.name}", MessageType.Error);
            return;
        }

        outputFolder = EditorGUILayout.TextField("Output Folder (Assets)", outputFolder);
        authorName = EditorGUILayout.TextField("Author Name", authorName);
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(source == null))
        {
            if (GUILayout.Button("Bake → Build DLL → Import"))
            {
                Bake();
            }
        }
    }

    private void Bake()
    {
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            EditorUtility.DisplayDialog("Dusk", "Please set an Output Folder under your Assets.", "OK");
            return;
        }

        string absOut = Path.GetFullPath(outputFolder);
        string assetsAbs = Path.GetFullPath(Application.dataPath);
        if (!absOut.StartsWith(assetsAbs, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Dusk", "Output Folder must be inside your Assets/ tree so Unity can import the DLL.", "OK");
            return;
        }

        Type? type = chosenClass?.GetClass();
        if (type == null)
        {
            EditorUtility.DisplayDialog("Dusk", "Could not resolve selected MonoScript's class.", "OK");
            return;
        }

        if (!typeof(EnemyAI).IsAssignableFrom(type) && !typeof(GrabbableObject).IsAssignableFrom(type))
        {
            EditorUtility.DisplayDialog("Dusk", "Selected class is not an EnemyAI or a GrabbableObject so it is not valid.", "OK");
            return;
        }

        List<FieldInfo> audioClipsWithTimeFields = new();
        List<FieldInfo> audioFields = new();
        List<FieldInfo> audioSourceFields = new();
        List<PropertyInfo> audioProperties = new();
        List<FieldInfo> audioListFields = new();
        List<FieldInfo> audioArrayFields = new();

        List<FieldInfo> particleSystemFields = new();
        List<PropertyInfo> particleSystemProperties = new();
        List<FieldInfo> particleSystemListFields = new();
        List<FieldInfo> particleSystemArrayFields = new();

        BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (FieldInfo fieldInfo in type.GetFields(bindingFlags))
        {
            if (fieldInfo.FieldType == typeof(AudioClip))
            {
                audioFields.Add(fieldInfo);
            }
            else if (fieldInfo.FieldType == typeof(List<AudioClip>))
            {
                audioListFields.Add(fieldInfo);
            }
            else if (fieldInfo.FieldType == typeof(AudioClip[]))
            {
                audioArrayFields.Add(fieldInfo);
            }
            else if (fieldInfo.FieldType == typeof(AudioSource))
            {
                audioSourceFields.Add(fieldInfo);
            }
            else if (fieldInfo.FieldType == typeof(AudioClipsWithTime))
            {
                audioClipsWithTimeFields.Add(fieldInfo);
            }
            else if (fieldInfo.FieldType == typeof(ParticleSystem))
            {
                particleSystemFields.Add(fieldInfo);
            }
            else if (fieldInfo.FieldType == typeof(List<ParticleSystem>))
            {
                particleSystemListFields.Add(fieldInfo);
            }
            else if (fieldInfo.FieldType == typeof(ParticleSystem[]))
            {
                particleSystemArrayFields.Add(fieldInfo);
            }
        }

        foreach (PropertyInfo propertyInfo in type.GetProperties(bindingFlags))
        {
            if (propertyInfo.PropertyType == typeof(AudioClip) && propertyInfo.CanWrite && propertyInfo.GetIndexParameters().Length == 0)
            {
                audioProperties.Add(propertyInfo);
            }
            else if (propertyInfo.PropertyType == typeof(ParticleSystem) && propertyInfo.CanWrite && propertyInfo.GetIndexParameters().Length == 0)
            {
                particleSystemProperties.Add(propertyInfo);
            }
        }

        if (audioFields.Count == 0 && audioProperties.Count == 0 && audioListFields.Count == 0 && audioArrayFields.Count == 0 && audioSourceFields.Count == 0 && particleSystemFields.Count == 0 && particleSystemProperties.Count == 0 && particleSystemListFields.Count == 0 && particleSystemArrayFields.Count == 0)
        {
            EditorUtility.DisplayDialog("Dusk Entity Replacement", "No audio or particle system fields/properties found", "OK");
            return;
        }

        Assembly srcAsm = type.Assembly;
        string srcAsmKey = Sanitize(srcAsm.GetName().Name, out bool isAssemblyCSharp);
        string baseRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DuskReplacementEntitiesDLL");
        string projRoot = Path.Combine(baseRoot, srcAsmKey);
        string genDir = Path.Combine(projRoot, "Generated");

        BaseAssemblyNamePrefix = BaseAssemblyNamePrefix.Replace("placeholder", authorName);
        string perAsmAssemblyName = $"{BaseAssemblyNamePrefix}.{srcAsmKey}";

        string extraRefName = Path.GetFileNameWithoutExtension(srcAsm.Location);
        string extraRefPath = srcAsm.Location;
        if (isAssemblyCSharp)
        {
            extraRefName = string.Empty;
            extraRefPath = string.Empty;
        }

        EnsureProjectScaffold(projRoot, genDir, perAsmAssemblyName, extraRefName, extraRefPath);
        WriteOrUpdateGeneratedClass(genDir, type, audioClipsWithTimeFields, audioSourceFields, audioFields, audioProperties, audioListFields, audioArrayFields, particleSystemFields, particleSystemListFields, particleSystemArrayFields, particleSystemProperties);

        if (!RunDotnetBuild(projRoot, perAsmAssemblyName, out var builtDllPath, out var stdout))
        {
            UnityEngine.Debug.LogError($"[DuskEntityReplacementBaker] Build failed for {perAsmAssemblyName}.\n{stdout}");
            EditorUtility.DisplayDialog("Dusk", $"dotnet build failed for {perAsmAssemblyName}. See Console for details.", "OK");
            return;
        }

        Directory.CreateDirectory(absOut);
        string targetDll = Path.Combine(absOut, $"{perAsmAssemblyName}.dll");
        File.Copy(builtDllPath, targetDll, overwrite: true);

        EditorUtility.RequestScriptReload();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        EditorUtility.DisplayDialog("Dusk", $"{perAsmAssemblyName}.dll imported. Scripts will reload to finish setup.", "OK");
    }

    private static string Sanitize(string raw, out bool isAssemblyCSharp)
    {
        isAssemblyCSharp = false;
        if (raw == "Assembly-CSharp")
        {
            isAssemblyCSharp = true;
            return raw;
        }

        if (string.IsNullOrEmpty(raw))
        {
            return "Unknown";
        }

        StringBuilder sb = new(raw.Length);
        foreach (char c in raw)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return sb.ToString();
    }

    private static void EnsureProjectScaffold(string projRoot, string genDir, string assemblyName, string extraRef, string pathToExtraRef)
    {
        Directory.CreateDirectory(projRoot);
        Directory.CreateDirectory(genDir);

        string csproj = BuildCsprojText(assemblyName, extraRef, pathToExtraRef);
        File.WriteAllText(Path.Combine(projRoot, "DuskReplacementEntities.csproj"), csproj, Encoding.UTF8);

        string gitIgnore = Path.Combine(projRoot, ".gitignore");
        if (!File.Exists(gitIgnore))
        {
            File.WriteAllText(gitIgnore, "bin/\nobj/\n", Encoding.UTF8);
        }
    }

    private static string BuildCsprojText(string assemblyName, string extraRef, string pathToExtraRef)
    {
        return
$@"<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>{TargetFramework}</TargetFramework>
        <AssemblyName>{assemblyName}</AssemblyName>
        <Description>Dusk per-assembly replacements</Description>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        <LangVersion>latest</LangVersion>
        <Configurations>Debug;Release</Configurations>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <PropertyGroup>
        <RestoreAdditionalProjectSources>
            https://api.nuget.org/v3/index.json;
            https://nuget.bepinex.dev/v3/index.json
        </RestoreAdditionalProjectSources>
    </PropertyGroup>

    <PropertyGroup>
        <DebugSymbols>true</DebugSymbols>
        <DebugType>embedded</DebugType>
        <PathMap>$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)'))=./</PathMap>
    </PropertyGroup>

    <ItemGroup Condition=""'$(TargetFramework.TrimEnd(`0123456789`))' == 'net'"">
        <PackageReference Include=""Microsoft.NETFramework.ReferenceAssemblies"" Version=""1.0.2"" PrivateAssets=""all"" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include=""BepInEx.AssemblyPublicizer.MSBuild"" Version=""0.4.2"" PrivateAssets=""all"" />
        <PackageReference Include=""BepInEx.Analyzers"" Version=""1.*"" PrivateAssets=""all"" />
        <PackageReference Include=""BepInEx.Core"" Version=""5.*"" />
        <PackageReference Include=""BepInEx.PluginInfoProps"" Version=""2.*"" />
        <PackageReference Include=""UnityEngine.Modules"" Version=""2022.3.9"" IncludeAssets=""compile"" PrivateAssets=""all"" />
        <PackageReference Include=""LethalCompany.GameLibs.Steam"" Publicize=""true"" Version=""*-*"" PrivateAssets=""all"" />
        <PackageReference Include=""TeamXiaolan.DawnLib"" Version=""0.2.16"" />
        <PackageReference Include=""TeamXiaolan.DawnLib.DuskMod"" Version=""0.2.16"" />
    </ItemGroup>" +
    (!string.IsNullOrEmpty(extraRef) ?
    $@"
    <ItemGroup>
        <Reference Include=""{extraRef}"" Private=""False""  Publicize=""true""><HintPath>{pathToExtraRef}</HintPath></Reference>
    </ItemGroup>" : "") +
    @"
</Project>";
    }

    private static string WriteOrUpdateGeneratedClass(string GenDir, Type type, List<FieldInfo> audioClipsWithTimeFields, List<FieldInfo> audioSourceFields, List<FieldInfo> audioFields, List<PropertyInfo> audioProperties, List<FieldInfo> audioListFields, List<FieldInfo> audioArrayFields, List<FieldInfo> particleSystemFields, List<FieldInfo> particleSystemListFields, List<FieldInfo> particleSystemArrayFields, List<PropertyInfo> particleSystemProperties)
    {
        string typeName = type.FullName;
        StringBuilder stringBuilder = new(typeName.Length);
        foreach (char character in typeName)
        {
            stringBuilder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        string safeTypeName = stringBuilder.ToString();
        string className = $"DuskEntityReplacementDefinition_{safeTypeName}";
        string fqcn = $"Dusk.{className}";

        StringBuilder sb = new(8192);
        sb.AppendLine("// <auto-generated> via DuskEntityReplacementBaker");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using Dawn.Utils;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("namespace Dusk");
        sb.AppendLine("{");
        if (typeof(EnemyAI).IsAssignableFrom(type))
        {
            sb.AppendLine($@"    [CreateAssetMenu(fileName = ""New Enemy Replacement Definition"", menuName = $""Entity Replacements/Enemy Replacements/{type.FullName}"")]");
            sb.AppendLine($"    public class {className} : DuskEnemyReplacementDefinition<{type.FullName}>");
        }
        else if (typeof(GrabbableObject).IsAssignableFrom(type))
        {
            sb.AppendLine($@"    [CreateAssetMenu(fileName = ""New Item Replacement Definition"", menuName = $""Entity Replacements/Item Replacements/{type.FullName}"")]");
            sb.AppendLine($"    public class {className} : DuskItemReplacementDefinition<{type.FullName}>");
        }
        sb.AppendLine("    {");

        if (audioFields.Count > 0 || audioListFields.Count > 0 || audioArrayFields.Count > 0 || audioSourceFields.Count > 0 || audioProperties.Count > 0)
        {
            sb.AppendLine($"        [Space(10)]");
        }

        foreach (FieldInfo audioClipsWithTimeField in audioClipsWithTimeFields)
        {
            sb.AppendLine($"        public AudioClipsWithTime {audioClipsWithTimeField.Name} = new();");
        }
        foreach (FieldInfo fieldInfo in audioFields)
        {
            sb.AppendLine($"        public AudioClip? {fieldInfo.Name} = null;");
        }
        foreach (PropertyInfo propertyInfo in audioProperties)
        {
            sb.AppendLine($"        public AudioClip? {propertyInfo.Name} = null;");
        }
        foreach (FieldInfo fieldInfo in audioListFields)
        {
            sb.AppendLine($"        public List<AudioClip> {fieldInfo.Name} = new();");
        }
        foreach (FieldInfo fieldInfo in audioArrayFields)
        {
            sb.AppendLine($"        public AudioClip[] {fieldInfo.Name} = System.Array.Empty<AudioClip>();");
        }
        foreach (FieldInfo fieldInfo in audioSourceFields)
        {
            sb.AppendLine($"        public AudioClip? potential{fieldInfo.Name.ToCapitalized()} = null;");
        }

        foreach (FieldInfo fieldInfo in particleSystemFields)
        {
            sb.AppendLine($"        public ParticleSystem? {fieldInfo.Name};"); ;
        }
        foreach (FieldInfo fieldInfo in particleSystemArrayFields)
        {
            sb.AppendLine($"        public ParticleSystem[] {fieldInfo.Name} = System.Array.Empty<ParticleSystem>();");
        }
        foreach (FieldInfo fieldInfo in particleSystemListFields)
        {
            sb.AppendLine($"        public List<ParticleSystem> {fieldInfo.Name} = new();");
        }
        foreach (PropertyInfo propertyInfo in particleSystemProperties)
        {
            sb.AppendLine($"        public ParticleSystem? {propertyInfo.Name} = null;");
        }

        sb.AppendLine();
        sb.AppendLine(@"        [Header(""Experimental (Usually vanilla only): PlayAudioAnimationEvent overrides"")]");
        sb.AppendLine("        public AudioClip? ExperimentalAudioToPlay = null;");
        sb.AppendLine("        public AudioClip? ExperimentalAudioToPlayB = null;");
        sb.AppendLine("        public AudioClip? ExperimentalAudioClip = null;");
        sb.AppendLine("        public AudioClip? ExperimentalAudioClip2 = null;");
        sb.AppendLine("        public AudioClip? ExperimentalAudioClip3 = null;");
        sb.AppendLine("        public AudioClip[] ExperimentalRandomClips  = System.Array.Empty<AudioClip>();");
        sb.AppendLine("        public AudioClip[] ExperimentalRandomClips2 = System.Array.Empty<AudioClip>();");
        sb.AppendLine("        public ParticleSystem? ExperimentalParticle = null;");

        sb.AppendLine();
        sb.AppendLine($"        protected override void Apply({type.FullName} {type.Name})");
        sb.AppendLine("        {");

        foreach (FieldInfo fieldInfo in audioClipsWithTimeFields)
        {
            sb.AppendLine($"            if (this.{fieldInfo.Name}.audioClips.Length > 0)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {type.Name}.{fieldInfo.Name} = this.{fieldInfo.Name};");
            sb.AppendLine($"            }}");
        }
        foreach (FieldInfo fieldInfo in audioArrayFields)
        {
            sb.AppendLine($"            if (this.{fieldInfo.Name}.Length > 0)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {type.Name}.{fieldInfo.Name} = this.{fieldInfo.Name};");
            sb.AppendLine($"            }}");
        }
        foreach (FieldInfo fieldInfo in audioSourceFields)
        {
            sb.AppendLine($"            if ({type.Name}.{fieldInfo.Name}.clip != null && this.potential{fieldInfo.Name.ToCapitalized()} != null)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {type.Name}.{fieldInfo.Name}.clip = this.potential{fieldInfo.Name.ToCapitalized()};");
            sb.AppendLine($"            }}");
        }
        foreach (FieldInfo fieldInfo in audioListFields)
        {
            sb.AppendLine($"            if (this.{fieldInfo.Name}.Count > 0)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {type.Name}.{fieldInfo.Name} = this.{fieldInfo.Name};");
            sb.AppendLine($"            }}");
        }
        foreach (PropertyInfo propertyInfo in audioProperties)
        {
            sb.AppendLine($"            if (this.{propertyInfo.Name} != null)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {type.Name}.{propertyInfo.Name} = this.{propertyInfo.Name};");
            sb.AppendLine($"            }}");
        }
        foreach (FieldInfo fieldInfo in audioFields)
        {
            sb.AppendLine($"            if (this.{fieldInfo.Name} != null)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {type.Name}.{fieldInfo.Name} = this.{fieldInfo.Name};");
            sb.AppendLine($"            }}");
        }

        foreach (FieldInfo fieldInfo in particleSystemArrayFields)
        {
            sb.AppendLine($"            if (this.{fieldInfo.Name}.Length > 0)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                for (int i = 0; i < this.{fieldInfo.Name}.Length; i++)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    ParticleSystem? newParticleSystem = this.{fieldInfo.Name}[i];");
            sb.AppendLine($"                    ParticleSystem targetParticleSystem = {type.Name}.{fieldInfo.Name}[i];");
            sb.AppendLine($"                    if (newParticleSystem == null || targetParticleSystem == null) continue;");
            sb.AppendLine($"                    targetParticleSystem.enabled = false;");
            sb.AppendLine($"                    GameObject newParticle = GameObject.Instantiate(newParticleSystem.gameObject, targetParticleSystem.transform.parent);");
            sb.AppendLine($"                    newParticle.name = targetParticleSystem.gameObject.name;");
            sb.AppendLine($"                    Destroy(targetParticleSystem.gameObject);");
            sb.AppendLine($"                }}");
            sb.AppendLine($"                {type.Name}.{fieldInfo.Name} = this.{fieldInfo.Name};");
            sb.AppendLine($"            }}");
        }
        foreach (FieldInfo fieldInfo in particleSystemListFields)
        {
            sb.AppendLine($"            if (this.{fieldInfo.Name}.Count > 0)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                for (int i = 0; i < this.{fieldInfo.Name}.Count; i++)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    ParticleSystem? newParticleSystem = this.{fieldInfo.Name}[i];");
            sb.AppendLine($"                    ParticleSystem targetParticleSystem = {type.Name}.{fieldInfo.Name}[i];");
            sb.AppendLine($"                    if (newParticleSystem == null || targetParticleSystem == null) continue;");
            sb.AppendLine($"                    GameObject newParticle = GameObject.Instantiate(newParticleSystem.gameObject, targetParticleSystem.transform.parent);");
            sb.AppendLine($"                    newParticle.name = targetParticleSystem.gameObject.name;");
            sb.AppendLine($"                    Destroy(targetParticleSystem.gameObject);");
            sb.AppendLine($"                }}");
            sb.AppendLine($"                {type.Name}.{fieldInfo.Name} = this.{fieldInfo.Name};");
            sb.AppendLine($"            }}");
        }
        foreach (PropertyInfo propertyInfo in particleSystemProperties)
        {
            sb.AppendLine($"            if (this.{propertyInfo.Name} != null)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                ParticleSystem? newParticleSystem = this.{propertyInfo.Name};");
            sb.AppendLine($"                ParticleSystem targetParticleSystem = {type.Name}.{propertyInfo.Name};");
            sb.AppendLine($"                if (newParticleSystem != null && targetParticleSystem != null)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    GameObject newParticle = GameObject.Instantiate(newParticleSystem.gameObject, targetParticleSystem.transform.parent);");
            sb.AppendLine($"                    newParticle.name = targetParticleSystem.gameObject.name;");
            sb.AppendLine($"                    Destroy(targetParticleSystem.gameObject);");
            sb.AppendLine($"                    {type.Name}.{propertyInfo.Name} = this.{propertyInfo.Name};");
            sb.AppendLine($"                }}");
            sb.AppendLine($"            }}");
        }
        foreach (FieldInfo fieldInfo in particleSystemFields)
        {
            sb.AppendLine($"            if (this.{fieldInfo.Name} != null)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                ParticleSystem? newParticleSystem = this.{fieldInfo.Name};");
            sb.AppendLine($"                ParticleSystem targetParticleSystem = {type.Name}.{fieldInfo.Name};");
            sb.AppendLine($"                if (newParticleSystem != null && targetParticleSystem != null)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    GameObject newParticle = GameObject.Instantiate(newParticleSystem.gameObject, targetParticleSystem.transform.parent);");
            sb.AppendLine($"                    newParticle.name = targetParticleSystem.gameObject.name;");
            sb.AppendLine($"                    Destroy(targetParticleSystem.gameObject);");
            sb.AppendLine($"                    {type.Name}.{fieldInfo.Name} = this.{fieldInfo.Name};");
            sb.AppendLine($"                }}");
            sb.AppendLine($"            }}");
        }

        sb.AppendLine();
        sb.AppendLine($"            Animator? animator = {type.Name}.GetComponentInChildren<Animator>(true);");
        sb.AppendLine("            if (animator != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                PlayAudioAnimationEvent? playAudioAnimationEvent = animator.gameObject.GetComponent<PlayAudioAnimationEvent>();");
        sb.AppendLine("                if (playAudioAnimationEvent != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (this.ExperimentalAudioToPlay  != null) playAudioAnimationEvent.audioToPlay.clip  = this.ExperimentalAudioToPlay;");
        sb.AppendLine("                    if (this.ExperimentalAudioToPlayB != null) playAudioAnimationEvent.audioToPlayB.clip = this.ExperimentalAudioToPlayB;");
        sb.AppendLine();
        sb.AppendLine("                    if (this.ExperimentalAudioClip  != null) playAudioAnimationEvent.audioClip  = this.ExperimentalAudioClip;");
        sb.AppendLine("                    if (this.ExperimentalAudioClip2 != null) playAudioAnimationEvent.audioClip2 = this.ExperimentalAudioClip2;");
        sb.AppendLine("                    if (this.ExperimentalAudioClip3 != null) playAudioAnimationEvent.audioClip3 = this.ExperimentalAudioClip3;");
        sb.AppendLine();
        sb.AppendLine("                    if (this.ExperimentalRandomClips.Length > 0 && playAudioAnimationEvent.randomClips.Length > 0)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        playAudioAnimationEvent.randomClips = this.ExperimentalRandomClips;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    if (this.ExperimentalRandomClips2.Length > 0 && playAudioAnimationEvent.randomClips2.Length > 0)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        playAudioAnimationEvent.randomClips2 = this.ExperimentalRandomClips2;");
        sb.AppendLine("                    }");
        sb.AppendLine();
        sb.AppendLine("                    if (this.ExperimentalParticle != null && playAudioAnimationEvent.particle != null)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        GameObject newGameObject = GameObject.Instantiate(this.ExperimentalParticle.gameObject, playAudioAnimationEvent.particle.transform.parent);");
        sb.AppendLine("                        newGameObject.name = playAudioAnimationEvent.particle.gameObject.name;");
        sb.AppendLine("                        Destroy(playAudioAnimationEvent.particle.gameObject);");
        sb.AppendLine("                        playAudioAnimationEvent.particle = newGameObject.GetComponent<ParticleSystem>();");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        string generatedFile = Path.Combine(GenDir, $"{className}.cs");
        File.WriteAllText(generatedFile, sb.ToString(), Encoding.UTF8);
        return fqcn;
    }

    private static bool RunDotnetBuild(string workingDir, string assemblyName, out string builtDllPath, out string output)
    {
        builtDllPath = Path.Combine(workingDir, "bin", "Release", TargetFramework, $"{assemblyName}.dll");

        ProcessStartInfo processStartInfo = new("dotnet", "build -c Release")
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        Process proc = new()
        {
            StartInfo = processStartInfo
        };

        StringBuilder stringBuilder = new();
        try
        {
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stringBuilder.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stringBuilder.AppendLine(e.Data); };

            if (!proc.Start())
            {
                output = "Failed to start dotnet.";
                return false;
            }
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            output = stringBuilder.ToString();
            if (proc.ExitCode != 0)
            {
                return false;
            }
            return File.Exists(builtDllPath);
        }
        catch (Exception ex)
        {
            output = $"Exception running dotnet: {ex}";
            return false;
        }
        finally
        {
            proc.Dispose();
        }
    }
}
