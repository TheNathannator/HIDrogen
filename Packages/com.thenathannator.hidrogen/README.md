# HIDrogen

An add-on for the [Unity InputSystem](https://github.com/Unity-Technologies/InputSystem) package that provides additional input backends, including proper HID support on Linux, and raw Xbox One controller support via GameInput on Windows.

(pronounced "hydrogen" or "hid-rogen")

## Table of Contents

- [Usage](#usage)
- [Configuration](#configuration)
- [Available Backends](#available-backends)
  - [Linux evdev Fallback](#linux-evdev-fallback)
  - [Raw HID Support on Linux](#raw-hid-support-on-linux)
    - [Dependencies](#dependencies)
    - [Configuration](#configuration-1)
  - [Xbox One Controllers on Windows via GameInput](#xbox-one-controllers-on-windows-via-gameinput)
    - [Dependencies](#dependencies-1)
    - [Notes](#notes)
  - [XInput Backend on Windows for Unity 2022.2+](#xinput-backend-on-windows-for-unity-20222)
    - [Notes](#notes-1)
- [License](#license)

## Usage

After installation, this package integrates and operates on its own. No manual initialization or interaction is required.

## Configuration

Some configuration is available through compile-defines. These defines apply to all backends:

- `HIDROGEN_VERBOSE_LOGGING`: Enables verbose logging to help debug issues with devices.

## Available Backends

### Linux evdev Fallback

The evdev backend exposes supported instruments managed by `xpad`, `hid-sony`
(`sony` in sysfs), or `hid-generic` for native Santroller HID devices.
It prefers a usable hidraw device and falls back to
`/dev/input/event*` when raw HID is unavailable, including permission failures.
Instrument layouts must be supplied by a package such as PlasticBand; unrelated
event devices are not added automatically.

- Hidraw and evdev siblings are correlated through their HID parent, not VID/PID,
  so identical controllers can be used independently.
- Backend ownership remains with the selected transport until it is removed.
  Changing permissions does not automatically replace an active evdev device.
- Input nodes are scanned once per second. Every delivered `SYN_REPORT` becomes
  a state event; initial state and `SYN_DROPPED` recovery are queried with ioctls.
- Event nodes need their own read permissions. Desktop-session controller access
  often already provides these, but hidraw denial does not guarantee evdev access.
- The backend is input-only. Raw output/feature reports and instrument-specific
  initialization are not emulated through evdev. Kernel filtering and unsupported
  report fields cannot be undone.
- On Linux, receivers already managed by a kernel driver are left attached
  instead of being claimed by the raw USB Xbox 360 receiver backend.

No additional native dependency beyond the existing libc/libudev support is needed.
Wired XInput subtype metadata is read from the USB descriptors in sysfs without
initializing libusb or claiming a USB interface. These cached descriptors are
normally world-readable; no USB-device access rule is needed.
If descriptor access or parsing fails, the
backend logs a warning and continues without subtype metadata, allowing layouts
to match using other device identifiers.

#### State Contract

Devices use interface name `Evdev` and state format `EVDV`. The packed state is
352 bytes: 96 bytes of Linux `EV_KEY` bits (codes 0-767), followed by 64 native
signed 32-bit `EV_ABS` values indexed by code. Unsupported axes remain zero.
Capabilities include bus, vendor/product IDs, version, kernel driver, XInput
`subType` when available, base64 key/axis capability bitmaps, and raw axis
ranges, fuzz, flat and resolution. Consumers must select mappings by identity
and driver and validate the required capabilities.

Native Santroller devices additionally expose `santrollerProtocol` (1 or 2),
`santrollerFlags`, and their instrument type in `subType`. Legacy `1209:2882`
devices encode the type in the high byte of the input version. Current USB
firmware (`1209:2882` or `1209:2883`) must supply a trailing vendor-specific
descriptor for each native HID gamepad interface:
`09 FF 53 41 4E 54 02 TT CC` (hexadecimal). This contains the descriptor
length/type, `SANT` signature, protocol version 2, instrument type and capability
flags. Only alternate setting zero of the matching HID interface is considered.
It is read from the USB device's cached sysfs `descriptors` attribute, like
xpad subtype metadata; neither USB transfers nor hidraw access are needed.
Interface/profile names and HID report formats are unchanged.
Current firmware without this metadata is warned about and left unmatched.
Other `hid-generic` devices are not probed as evdev instruments.

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

### XInput Backend on Windows for Unity 2022.2+

This backend reintroduces XInput support to Unity 2022.2 and onward; starting from 2022.2 they switched to Windows.Gaming.Input for gamepad support. All existing XInput layouts and commands are supported through this backend, except for gamepads, which will be handled by the native backend already and are ignored.

#### Notes

- This backend will only enable itself in Unity 2022.2 and onward. No special setup is needed for it.
- This backend does not respect the `InputSystem.pollingFrequency` setting currently, it uses a hard-set sleep time of 1 ms.

### Raw USB Support

This backend supports specific kinds of devices via raw USB.

#### Dependencies

This backend depends on `libusb-1.0`.

- On Windows and Mac, this is provided with the package.
- On Linux, it will need to be installed manually. On distributions that use `apt`, the following command should do the trick:

  ```
  sudo apt install libusb-1.0-0
  ```

  - Additionally, for Xbox 360 receiver support, the [Resources/69-hidrogen-x360-receiver.rules](Resources/69-hidrogen-x360-receiver.rules) rules file must be placed in `/etc/udev/rules.d` or `/usr/lib/udev/rules.d/`. 

## License

This project is licensed under the MIT license. See [LICENSE.md](LICENSE.md) for details.
