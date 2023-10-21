using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

public static class BuildPackage
{
    private const string PackagePath = "Packages/com.thenathannator.hidrogen";
    private const string OutputDirectory = "Builds/";

    private const string ProgressTitle = "Building Package";

    private static readonly string TempDirectory = Path.Combine(OutputDirectory, "buildTemp");

    [MenuItem("HIDrogen/Build Package", isValidateFunction: false, priority: 2100)]
    public static void Build()
    {
        try
        {
            // Ensure temporary directory is removed
            if (Directory.Exists(TempDirectory))
            {
                EditorUtility.DisplayProgressBar(ProgressTitle, "Deleting left-over temp directory", 0f);
                DeleteDirectory(TempDirectory);
            }

            // Copy files to temporary directory
            EditorUtility.DisplayProgressBar(ProgressTitle, "Copying files to temp directory", 0.05f);
            CopyDirectory(PackagePath, TempDirectory);

            // Remove all folders named "Ignored", along with their contents
            EditorUtility.DisplayProgressBar(ProgressTitle, "Removing ignored files", 0.30f);
            RemoveIgnored(TempDirectory);

            // Start packaging
            EditorUtility.DisplayProgressBar(ProgressTitle, "Creating package", 0.70f);
            var packTask = Client.Pack(TempDirectory, OutputDirectory);
            while (!packTask.IsCompleted)
            {
                Thread.Sleep(100);
            }

            // Display result
            string message;
            switch (packTask.Status)
            {
                case StatusCode.Success:
                    message = $"Successfully packed!\nTarball has been output to: {packTask.Result.tarballPath}";
                    Debug.Log(message);
                    break;

                case StatusCode.Failure:
                    message = $"Failed to pack!\nMessage: {packTask.Error?.message ?? "(Not given)"}\nError code: {packTask.Error?.errorCode ?? ErrorCode.Unknown}";
                    Debug.LogError(message);
                    break;

                default:
                    message = $"Unhandled status code: {packTask.Status}";
                    Debug.LogError(message);
                    break;
            }
            EditorUtility.DisplayDialog("Build Result", message, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error when building package!");
            Debug.LogException(ex);
        }
        finally
        {
            try
            {
                EditorUtility.DisplayProgressBar(ProgressTitle, "Removing temp directory", 0.95f);
                DeleteDirectory(TempDirectory);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error when deleting temp directory!");
                Debug.LogException(ex);
            }

            EditorUtility.ClearProgressBar();
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        // Don't attempt if source directory doesn't exist
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException("Could not find source folder!");

        // Ensure target directory exists
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        // Copy everything from the source directory to the target directory
        var dirInfo = new DirectoryInfo(sourceDir);
        sourceDir = dirInfo.FullName;
        foreach (var fileInfo in dirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories))
        {
            string path = fileInfo.FullName;
            string dir = fileInfo.Directory.FullName;
            string newDir;
            if (dir.Length > sourceDir.Length)
            {
                string shortDir = dir.Substring(sourceDir.Length + 1);
                newDir = Path.Combine(targetDir, shortDir);
                if (!Directory.Exists(newDir))
                    Directory.CreateDirectory(newDir);
            }
            else
            {
                newDir = targetDir;
            }

            string newPath = Path.Combine(newDir, fileInfo.Name);
            File.Copy(path, newPath, true);
        }
    }

    private static void DeleteDirectory(string directory)
    {
        // Don't attempt to delete a non-existent directory
        if (!Directory.Exists(directory))
            return;

        try
        {
            Directory.Delete(directory, true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error when deleting '{directory}', trying again\n{ex}");
            Debug.LogException(ex);
            Directory.Delete(directory, true);
        }
    }

    private static void RemoveIgnored(string directory)
    {
        foreach (string path in Directory.EnumerateDirectories(directory, "Ignored", SearchOption.AllDirectories))
        {
            DeleteDirectory(path);

            // Remove corresponding .meta file
            string metaPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".meta";
            File.Delete(metaPath);
        }
    }
}