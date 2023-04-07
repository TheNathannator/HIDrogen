# HIDrogen

(pronounced as either "hydrogen" or "hid-rogen")

An add-on for the [Unity InputSystem](https://github.com/Unity-Technologies/InputSystem) package that provides proper HID device support on Linux.

## Installing

### From the Releases Page

1. Download the .tgz file from the [latest release](https://github.com/TheNathannator/HIDrogen/releases/latest).
2. Open the Unity Package Manager and hit the + button, then pick `Add package from tarball`.
3. Select the downloaded .tgz file in the file prompt.

To update, repeat with the new .tgz.

### From Git via URL

1. Open the Unity Package Manager and hit the + button, then select `Add package from git URL`.
2. Paste in `https://github.com/TheNathannator/HIDrogen.git?path=/Packages/com.thenathannator.hidrogen` and hit Add.

To update, just repeat these steps with the same URL, and the package manager will automatically update from the latest Git commit.

### From Git via Cloning

1. Clone this repository to somewhere on your system.
2. Go to the Unity Package Manager and hit the + button, then pick `Add package from disk`.
3. Navigate to the `Packages` > `com.thenathannator.hidrogen` folder inside the clone and select the `package.json` file.

To update, pull the latest commits. Unity will detect the changes automatically.

### From Git via Manifest

In your Packages > `manifest.json` file, add the following line to your `dependencies`:

```diff
{
  "dependencies": {
+   "com.thenathannator.hidrogen": "https://github.com/TheNathannator/HIDrogen.git?path=/Packages/com.thenathannator.hidrogen"
  }
}
```

To update, go into Packages > `package-lock.json` and remove the `hash` field from the package listing, along with the comma on the preceding field:

```diff
{
  "dependencies": {
    "com.thenathannator.hidrogen": {
      "version": "https://github.com/TheNathannator/HIDrogen.git?path=/Packages/com.thenathannator.hidrogen",
      "depth": 0,
      "source": "git",
      "dependencies": {
        ...
-     }, // It is *important* that you remove the comma here! The package manager will error out otherwise
-     "hash": "506eab2dff57d2d3436fec840dcc85a12d4f6062"
+     }
    }
  }
}
```

Unity will automatically restore from the latest Git commit upon regaining focus.

## License

This project is licensed under the MIT license. See [LICENSE.md](LICENSE.md) for details.
