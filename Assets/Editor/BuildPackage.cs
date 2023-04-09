using System;
using System.Collections.Generic;
using System.IO;
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
        Idle,
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
    string errorMessage;
    IEnumerator<Task> buildTasks;
    Task currentTask;
    System.Diagnostics.Stopwatch currentTaskTimer = new System.Diagnostics.Stopwatch();
    int previousTimerSeconds;
    PackRequest packTask;
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

        const int displayThreshold = 10000; // 10 seconds
        if (currentTaskTimer.ElapsedMilliseconds > displayThreshold)
        {
            GUILayout.Label($"Time elapsed: {currentTaskTimer.Elapsed.Seconds} (timeout threshold: 1 minute)");
        }
    }

    void Update()
    {
        // Ensure a repaint at least every second to keep timer accurate
        if (currentTaskTimer.Elapsed.Seconds != previousTimerSeconds)
        {
            previousTimerSeconds = currentTaskTimer.Elapsed.Seconds;
            Repaint();
        }

        switch (status)
        {
            case BuildStatus.Starting:
                if (!Directory.Exists(outputPath))
                {
                    EditorUtility.DisplayDialog("Invalid Directory", $"{outputPath} is not a valid directory!", "OK");
                    return;
                }

                EditorApplication.LockReloadAssemblies();
                tempDir = Path.Combine(outputPath, "buildTemp");
                buildTasks = BuildPackage().GetEnumerator();
                SetStatus(BuildStatus.Running);
                break;

            case BuildStatus.Package:
                if (packTask.IsCompleted)
                    SetStatus(BuildStatus.Running); // Continue to the next task
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
                if (currentTask != null && currentTask.IsFaulted)
                {
                    var exception = currentTask.Exception;
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
                if (currentTask != null) // A task is currently running
                {
                    if (!currentTask.IsCompleted)
                    {
                        const int timeout = 60000; // 1 minute timeout limit
                        if (currentTaskTimer.ElapsedMilliseconds > timeout)
                        {
                            errorMessage = $"Timeout threshold reached! Current status: {status}";
                            SetStatus(BuildStatus.Error);
                        }
                        break;
                    }
                    
                    if (currentTask.IsFaulted)
                    {
                        SetStatus(BuildStatus.Error);
                        break;
                    }
                }

                // Either no task is running or the running task has completed
                if (buildTasks != null)
                {
                    if (buildTasks.MoveNext())
                    {
                        currentTask = buildTasks.Current;
                        currentTaskTimer.Restart();
                    }
                    else
                    {
                        // No more tasks available
                        SetStatus(BuildStatus.Complete);
                    }
                }
                break;
        }
    }

    void SetStatus(BuildStatus newStatus)
    {
        status = newStatus;
        Repaint();
    }

    void ResetState()
    {
        status = BuildStatus.Idle;
        buildTasks = null;
        currentTask = null;
        packTask = null;
        outputPath = null;
        tempDir = null;
        errorMessage = null;
        currentTaskTimer.Reset();
        EditorApplication.UnlockReloadAssemblies();
    }

    IEnumerable<Task> BuildPackage()
    {
        // Ensure temporary directory is removed
        if (Directory.Exists(tempDir))
        {
            SetStatus(BuildStatus.DeleteTemp);
            yield return DeleteDirectory(tempDir);
        }

        // Copy files to temporary directory
        SetStatus(BuildStatus.CopyFiles);
        yield return CopyDirectory(Path.GetFullPath(packagePath), tempDir);

        // Remove all folders named "Ignored", along with their contents
        SetStatus(BuildStatus.RemoveIgnored);
        yield return RemoveIgnored(tempDir);

        // Start packaging
        SetStatus(BuildStatus.Package);
        string packOutput = Path.Combine(outputPath, "Builds");
        Debug.Log($"Creating package in {packOutput}");
        packTask = Client.Pack(tempDir, packOutput);
        yield return Task.CompletedTask;

        // Delete temporary directory now that we're done with it
        SetStatus(BuildStatus.DeleteTemp);
        yield return DeleteDirectory(tempDir);

        SetStatus(BuildStatus.Complete);
        yield break;
    }

    Task CopyDirectory(string sourceDir, string targetDir)
    {
        return Task.Run(() => {
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
        });
    }

    Task DeleteDirectory(string directory)
    {
        return Task.Run(() => {
            // Don't attempt to delete a non-existent directory
            if (!Directory.Exists(directory))
                return;

            Debug.Log($"Removing folder {directory}");
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error when deleting, trying again\n{ex}");
                Directory.Delete(directory, true);
            }
        });
    }

    Task RemoveIgnored(string directory)
    {
        return Task.Run(() => {
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
        });
    }
}
