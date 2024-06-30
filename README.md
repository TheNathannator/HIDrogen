# HIDrogen

An add-on for the [Unity InputSystem](https://github.com/Unity-Technologies/InputSystem) package that provides additional input backends, including proper HID support on Linux, and raw Xbox One controller support via GameInput on Windows.

Refer to the inner [package README](Packages/com.thenathannator.hidrogen/README.md) for more details.

(pronounced "hydrogen" or "hid-rogen")

## Installing

Below are instructions for installing the package itself. Note that some backends have their own dependencies which must be installed separately alongside your application; refer to the inner [package README](Packages/com.thenathannator.hidrogen/README.md) for more details.

### From the Releases Page

See the [Unity documentation](https://docs.unity3d.com/Manual/upm-ui-local.html) for full details.

1. Download the .tgz file from the [latest release](https://github.com/TheNathannator/HIDrogen/releases/latest).
2. Open the Unity Package Manager and hit the + button, then pick `Add package from tarball`.
3. Select the downloaded .tgz file in the file prompt.

To update, repeat with the new .tgz.

### From Git

See the [Unity documentation](https://docs.unity3d.com/Manual/upm-git.html) for full details.

#### Via URL

1. Open the Unity Package Manager and hit the + button, then select `Add package from git URL`.
2. Paste in `https://github.com/TheNathannator/HIDrogen.git?path=/Packages/com.thenathannator.hidrogen#v0.3.1` and hit Add.

To update, increment the version number at the end of the URL to the new version number and repeat these steps with the new URL. Alternatively, you can edit the URL listed in your `manifest.json` file as described in the [Via Manifest](#via-manifest) section.

#### Via Manifest

In your Packages > `manifest.json` file, add the following line to your `dependencies`:

```diff
{
  "dependencies": {
+   "com.thenathannator.hidrogen": "https://github.com/TheNathannator/HIDrogen.git?path=/Packages/com.thenathannator.hidrogen#v0.3.1"
  }
}
```

To update, increment the version number at the end of the URL to the new version number. The package manager will automatically pull the new changes upon regaining focus.

#### Via Cloning

1. Clone this repository to somewhere on your system.
2. Go to the Unity Package Manager and hit the + button, then pick `Add package from disk`.
3. Navigate to the `Packages` > `com.thenathannator.hidrogen` folder inside the clone and select the `package.json` file.

To update, pull the latest commits. Unity will detect the changes automatically.

## License

This project is licensed under the MIT license. See [LICENSE.md](LICENSE.md) for details.
