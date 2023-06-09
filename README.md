# HIDrogen

(pronounced as either "hydrogen" or "hid-rogen")

An add-on for the [Unity InputSystem](https://github.com/Unity-Technologies/InputSystem) package that provides proper HID device support on Linux.

Most device handling features are implemented, including raw input reports, device descriptor retrieval/parsing, and input system IOCTLs. The backend is also pretty robust, with input event double-buffering, some error tolerance, and a fallback device enumeration method.

If there's anything in the native Windows or Mac HID backends that is not implemented here, feel free to contribute!

## Usage

After installation, this package integrates and operates on its own. No manual initialization is required.

### Dependencies

This project relies on the hidraw version of hidapi for input, along with libudev for device connection/disconnection monitoring. By installing this package, your project will become dependent on them as well on Linux. On distributions that use `apt`, the following commands should do the trick:

```
sudo apt install libhidapi-hidraw0
sudo apt install libudev1
```

The user will also need to install some udev rules in order to allow accessing hidraw devices without elevated priveleges:

```
# If you just want specific devices, you can specify them by vendor/product ID with the following.
KERNEL=="hidraw*", ATTRS{idVendor}=="04d8", ATTRS{idProduct}=="003f", TAG+="uaccess"

# If you want *all* hidraw devices, you can omit the attributes and just set the access tag.
KERNEL=="hidraw*", TAG+="uaccess"
```

The rules file should be placed in `/etc/udev/rules.d` or `/usr/lib/udev/rules.d/`, and it must come before `73-seat-late.rules`.

### Configuration

Some configuration is available through compile-defines:

- `HIDROGEN_VERBOSE_LOGGING`: Enables verbose logging to help debug issues with devices.
- `HIDROGEN_FORCE_REPORT_IDS`: By default, report IDs are not provided if a device doesn't use them. This define will make state events always include the report ID byte as the first byte. This behavior is disabled by default for parity with macOS; Windows is technically the outlier here.
- `HIDROGEN_KEEP_NATIVE_DEVICES`: As part of supporting HID devices properly, devices that come from the native backend under the `Linux` interface are automatically disabled. This define will disable removal of those devices. *Be warned that you must be prepared to handle multiple devices with similar (or the same) inputs!*

## Installing

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
2. Paste in `https://github.com/TheNathannator/HIDrogen.git?path=/Packages/com.thenathannator.hidrogen#v0.2.0` and hit Add.

To update, increment the version number at the end of the URL to the new version number and repeat these steps with the new URL. Alternatively, you can edit the URL listed in your `manifest.json` file as described in the [Via Manifest](#via-manifest) section.

#### Via Manifest

In your Packages > `manifest.json` file, add the following line to your `dependencies`:

```diff
{
  "dependencies": {
+   "com.thenathannator.hidrogen": "https://github.com/TheNathannator/HIDrogen.git?path=/Packages/com.thenathannator.hidrogen#v0.2.0"
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
