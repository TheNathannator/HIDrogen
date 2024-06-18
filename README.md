# HIDrogen

(pronounced as either "hydrogen" or "hid-rogen")

An add-on for the [Unity InputSystem](https://github.com/Unity-Technologies/InputSystem) package that provides additional input backends.

## Table of Contents

- [Installing](#installing)
  - [From the Releases Page](#from-the-releases-page)
  - [From Git](#from-git)
    - [Via URL](#via-url)
    - [Via Manifest](#via-manifest)
    - [Via Cloning](#via-cloning)
- [Usage](#usage)
- [Configuration](#configuration)
- [Available Backends](#available-backends)
  - [Raw HID Support on Linux](#raw-hid-support-on-linux)
    - [Dependencies](#dependencies)
    - [Configuration](#configuration-1)
  - [Xbox One Controllers on Windows via GameInput](#xbox-one-controllers-on-windows-via-gameinput)
    - [Dependencies](#dependencies-1)
    - [Notes](#notes)
- [License](#license)

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

## Usage

After installation, this package integrates and operates on its own. No manual initialization is required.

## Configuration

Some configuration is available through compile-defines. These defines apply to all backends:

- `HIDROGEN_VERBOSE_LOGGING`: Enables verbose logging to help debug issues with devices.

## Available Backends

### Raw HID Support on Linux

This backend provides raw HID support on Linux through the `hidraw` kernel driver. Raw input/output reports and device descriptor retrieval are implemented, which allows the built-in HID functionality of the input system to work as expected, including custom device layouts.

#### Dependencies

This backend relies on the hidraw version of `hidapi` for input, along with `libudev` for device connection/disconnection monitoring. By installing this package, your project will become dependent on them as well on Linux. On distributions that use `apt`, the following command should do the trick:

```
sudo apt install libhidapi-hidraw0 libudev1
```

The user will also need to install some udev rules in order to allow accessing hidraw devices without elevated priveleges:

```
# If you just want specific devices, you can specify them by vendor/product ID with the following.
KERNEL=="hidraw*", ATTRS{idVendor}=="04d8", ATTRS{idProduct}=="003f", TAG+="uaccess"

# If you want *all* hidraw devices, you can omit the attributes and just set the access tag.
KERNEL=="hidraw*", TAG+="uaccess"

# There does not appear to be any way to filter by usage/usage page through ATTRS alone.
# If it is possible, it requires udev rule knowledge I do not possess at the time of writing.
```

The rules file should be placed in `/etc/udev/rules.d` or `/usr/lib/udev/rules.d/`, and it must come before `73-seat-late.rules`.

#### Configuration

Some configuration is available through compile-defines:

- `HIDROGEN_FORCE_REPORT_IDS`: By default, report IDs are not provided if a device doesn't use them. This define will make state events always include the report ID byte as the first byte. This behavior is disabled by default, since that's how hidapi/hidraw provides inputs.

### Xbox One Controllers on Windows via GameInput

This backend provides raw Xbox One controller support through the [GameInput API](https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/input/overviews/input-overview) on Windows. Raw inputs and outputs are supported, allowing similar custom device layout support as with HID devices.

#### Dependencies

This backend depends on GameInput being installed on the user's system. Although it is installed by default on recent versions of Windows, there is a redistributable that is recommended to be used in an installer bundle to provide compatibility with older versions. Refer to the [GameInput documentation](https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/input/overviews/input-nuget) for full details.

#### Notes

- As of the time of writing, the GameInput function that is responsible for sending raw output reports is not currently implemented. All output commands will silently fail until it gets implemented. (Only the `E_NOTIMPL` HRESULT is treated as success, all others will be logged and generate a proper failure code.)
- Gamepads reported through GameInput are ignored, to ensure no duplicate devices will occur between GameInput and XInput.

## License

This project is licensed under the MIT license. See [LICENSE.md](LICENSE.md) for details.
