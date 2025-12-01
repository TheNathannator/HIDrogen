using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;

using Debug = UnityEngine.Debug;

public class DirectoryBuildPropsScope : IDisposable
{
    private readonly string _path;

    public DirectoryBuildPropsScope(string directory, string coreModulePath)
    {
        _path = Path.Combine(directory, "Directory.Build.props");
        File.WriteAllText(_path,
            $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">

                <PropertyGroup>
                    <DefineConstants>$(DefineConstants);UNITY_STANDALONE</DefineConstants>
                </PropertyGroup>

                <ItemGroup>
                    <Reference Include=""UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"">
                        <SpecificVersion>False</SpecificVersion>
                        <HintPath>{coreModulePath.Replace('/', '\\')}</HintPath>
                    </Reference>
                </ItemGroup>

            </Project>
            "
        );
    }

    public void Dispose()
    {
        File.Delete(_path);
    }
}

public static class BuildExternalModules
{
    private const string PackagePath = "Packages/com.thenathannator.hidrogen/HIDrogen";
    private const string OutputPluginsPath = PackagePath + "/Plugins";

    private const string ProgressTitle = "Building external modules";

    [MenuItem("HIDrogen/Build External Modules", isValidateFunction: false, priority: 2200)]
    public static void Build()
    {
        try
        {
            // Find UnityEngine.CoreModule so it can be referenced by projects being built
            string[] precompiled = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.UnityEngine);
            string coreModulePath = precompiled.First((p) => p.EndsWith("UnityEngine.CoreModule.dll"));

            BuildProject("Submodules/SharpGameInput/SharpGameInput.v0/Source/SharpGameInput.v0.csproj", coreModulePath, 0.1f);
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Error while building external modules!", ex.ToString(), "OK");
            Debug.LogError("Error while building external modules!");
            Debug.LogException(ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void BuildProject(string projectPath, string coreModulePath, float progress)
    {
        string projectDirectory = Path.GetDirectoryName(projectPath);
        string projectName = Path.GetFileNameWithoutExtension(projectPath);

        using (new DirectoryBuildPropsScope(projectDirectory, coreModulePath))
        {
            string outputDirectory = RunBuildProjectCommand(projectPath, progress);
            File.Copy(Path.Combine(outputDirectory, projectName + ".dll"), OutputPluginsPath + "/" + projectName + ".dll", overwrite: true);
            File.Copy(Path.Combine(outputDirectory, projectName + ".pdb"), OutputPluginsPath + "/" + projectName + ".pdb", overwrite: true);
            Debug.Log("Successfully built " + projectName);
        }
    }

    private static string RunBuildProjectCommand(string projectFile, float progress)
    {
        string assemblyName = Path.GetFileNameWithoutExtension(projectFile);

        // Fire up `dotnet` to publish the project
        var output = RunCommand("dotnet",
            $@"build ""{projectFile}"" /nologo /p:Configuration=Release /p:TargetFramework=netstandard2.0 /p:GenerateFullPaths=true /consoleloggerparameters:NoSummary",
            ProgressTitle, $"Building project {assemblyName}", progress
        );

        string assemblySearch = $"{assemblyName} -> ";

        string outputPath = "";
        string line;
        while (!output.EndOfStream && (line = output.ReadLine()) != null)
        {
            int index = line.IndexOf(assemblySearch);
            if (index < 0)
                continue;

            var path = line.Substring(index + assemblySearch.Length);
            if (path.EndsWith(".dll"))
            {
                // Built assembly path
                outputPath = Path.GetDirectoryName(path).ToString();
            }
            else
            {
                // This is the path to the `publish` folder
                outputPath = path;
            }
        }

        return outputPath;
    }

    private static StreamReader RunCommand(string command, string args, string progMsg, string progInfo, float progress)
    {
        using (var process = Process.Start(new ProcessStartInfo()
        {
            FileName = command,
            Arguments = args,
            UseShellExecute = false, // Must be false to redirect input/output/error
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        }))
        {
            while (!process.HasExited)
            {
                if (EditorUtility.DisplayCancelableProgressBar(progMsg, progInfo, progress))
                {
                    process.Kill();
                    throw new Exception($"Command was cancelled!");
                }

                Thread.Sleep(100);
            }

            // Bail out on error
            var output = process.StandardOutput;
            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error) || process.ExitCode != 0)
                throw new Exception($"Error when running command! Exit code: {process.ExitCode}, command output:\n{output.ReadToEnd()}{error}");

            return output;
        }
    }
}