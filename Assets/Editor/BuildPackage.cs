using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public class BuildPackageWindow : EditorWindow
{
    [MenuItem("Window/HIDrogen/Build Package", isValidateFunction: false, priority: 2100)]
    public static void Init()
    {
        GetWindow<BuildPackageWindow>("Build Package", focus: true);
    }

    enum BuildStatus
    {
        Idle = 0,
        Starting,
        Running,
        CopyFiles,
        RemoveIgnored,
        Package,
        DeleteTemp,
        Complete,
        Error
    }

    const string packagePath = "Packages/com.thenathannator.hidrogen";
    string outputPath_GUI;
    string outputPath;
    string tempDir;
    Task buildTask;
    PackRequest packTask;
    EventWaitHandle packCompleted = new EventWaitHandle(false, EventResetMode.AutoReset);
    BuildStatus status;

    void OnGUI()
    {
        outputPath_GUI = EditorGUILayout.TextField("Output Path", outputPath_GUI);
        if (GUILayout.Button("Build") && status == BuildStatus.Idle)
        {
            outputPath = outputPath_GUI;
            status = BuildStatus.Starting;
        }

        switch (status)
        {
            case BuildStatus.Starting:
                GUILayout.Label("Starting build...");
                break;
            case BuildStatus.Running:
                GUILayout.Label("Building...");
                break;
            case BuildStatus.DeleteTemp:
                GUILayout.Label("Deleting temp folder...");
                break;
            case BuildStatus.CopyFiles:
                GUILayout.Label("Copying package files to temp directory...");
                break;
            case BuildStatus.RemoveIgnored:
                GUILayout.Label("Removing \"Ignored\" folders...");
                break;
            case BuildStatus.Package:
                GUILayout.Label("Creating package...");
                break;
            case BuildStatus.Complete:
                GUILayout.Label("Success!");
                break;
            case BuildStatus.Error:
                GUILayout.Label("Error!");
                break;
            default:
                break;
        }
    }

    void Update()
    {
        switch (status)
        {
            case BuildStatus.Starting:
                EditorApplication.LockReloadAssemblies();
                status = BuildStatus.Running;
                tempDir = Path.Combine(outputPath, "buildTemp");
                buildTask = BuildPackage();
                break;

            case BuildStatus.Package:
                if (packTask == null)
                {
                    string packOutput = Path.Combine(outputPath, "Builds");
                    Debug.Log($"Creating package in {packOutput}");
                    packTask = Client.Pack(tempDir, packOutput);
                }

                if (packTask.IsCompleted)
                    packCompleted.Set();
                break;

            case BuildStatus.Complete:
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
                ResetState();
                break;

            case BuildStatus.Error:
                string errorMessage = null;
                if (buildTask.IsFaulted)
                {
                    // The build task failed, get its exception message
                    var exception = buildTask.Exception;
                    // Only display inner exception if only one occured
                    errorMessage = exception.InnerExceptions.Count < 2
                        ? $"An error occured!\n{exception.InnerException}"
                        : $"An error occured!\n{exception}";
                }
                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = "An unknown error occured!"; // Default message if none was provided

                Debug.LogError(errorMessage);
                EditorUtility.DisplayDialog("Build Error", errorMessage, "OK");
                ResetState();
                break;

            default:
                break;
        }
    }

    void ResetState()
    {
        status = BuildStatus.Idle;
        packTask = null;
        buildTask = null;
        outputPath = null;
        tempDir = null;
        EditorApplication.UnlockReloadAssemblies();
    }

    Task BuildPackage()
    {
        return Task.Run(() => {
            BuildStatus finalStatus = BuildStatus.Error;
            try
            {
                // Don't attempt if output path doesn't exist
                if (!Directory.Exists(outputPath))
                    throw new DirectoryNotFoundException("Output path must be a directory!");

                // Ensure temporary directory is removed
                if (Directory.Exists(tempDir))
                {
                    status = BuildStatus.DeleteTemp;
                    DeleteDirectory(tempDir);
                }

                // Copy files to temporary directory
                status = BuildStatus.CopyFiles;
                CopyDirectory(Path.GetFullPath(packagePath), tempDir);

                // Remove all folders named "Ignored", along with their contents
                status = BuildStatus.RemoveIgnored;
                RemoveIgnored(tempDir);

                // Start packaging and wait for it to complete
                status = BuildStatus.Package;
                packCompleted.WaitOne();
                finalStatus = BuildStatus.Complete;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while building: {ex}");
                finalStatus = BuildStatus.Error;
                throw;
            }
            finally
            {
                // Remove temporary directory
                status = BuildStatus.DeleteTemp;
                DeleteDirectory(tempDir);

                // Set final status
                status = finalStatus;
            }
        });
    }

    void CopyDirectory(string sourceDir, string targetDir)
    {
        Debug.Log($"Copying files from {sourceDir} to temp directory {targetDir}");
        // Don't attempt if source directory doesn't exist
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException("Could not find source folder!");

        // Ensure target directory exists
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Copy everything from the source directory to the target directory
        var dirInfo = new DirectoryInfo(sourceDir);
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
                {
                    Debug.Log($"Creating directory {newDir}");
                    Directory.CreateDirectory(newDir);
                }
            }
            else
            {
                newDir = targetDir;
            }

            string newPath = Path.Combine(newDir, fileInfo.Name);
            Debug.Log($"Copying from {path} to {newPath}");
            File.Copy(path, newPath, true);
        }
    }

    void DeleteDirectory(string directory)
    {
        // Don't attempt to delete a non-existent directory
        if (!Directory.Exists(directory))
            return;

        Debug.Log($"Removing folder {directory}");
        Directory.Delete(directory, true);
    }

    void RemoveIgnored(string directory)
    {
        Debug.Log($"Removing \"Ignored\" folders from {directory}");
        foreach (string path in Directory.EnumerateDirectories(directory, "Ignored", SearchOption.AllDirectories))
        {
            Debug.Log($"Deleting directory {path}");
            Directory.Delete(path, true);
            string noEndSeparator = path.TrimEnd(Path.DirectorySeparatorChar);
            string metaPath = noEndSeparator + ".meta";
            Debug.Log($"Deleting file {metaPath}");
            File.Delete(metaPath);
        }
    }
}
