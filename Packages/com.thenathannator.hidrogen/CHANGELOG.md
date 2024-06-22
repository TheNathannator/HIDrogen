# Changelog

All notable changes to HIDrogen will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Dates are relative to UTC.

## [0.3.1] - 2024/06/22

### Fixed

- The hidapi backend should no longer consistently fail with an interrupted syscall error.
- The warning message for manual hidapi enumeration is no longer logged unconditionally on exit.
- A couple wait handle "leaks" have been fixed (I forgot to dispose them up-front, they would get disposed late due to having to be disposed by the GC).

## [0.3.0] - 2024/06/18

### Added

- A new backend has been added for the GameInput API on Windows. This backend allows creating custom layouts and receiving/sending raw inputs/outputs for Xbox One devices.
  - To note: as of the time of writing, the GameInput function that is responsible for sending raw output reports is not currently implemented. All output commands will silently fail until it gets implemented on their end. (Only the `E_NOTIMPL` HRESULT is treated as success, all others will be logged and generate a proper failure code.)
  - Gamepads reported through GameInput are ignored, no duplicate devices will occur between GameInput and XInput, for example.

### Changed

- Some memory allocations that occur in the core backend update loop have been eliminated. All memory allocations that come from HIDrogen should now only happen during device connection/disconnection.
- Shimming of the native input system's devices on Linux has been changed so that no `InputDevice` instances will be created for them whatsoever.
  - Before, the device instance would exist for at least one frame before getting removed by the shim.
  - As a side-effect, this unfortunately causes an exception to be thrown/logged internally in the input system. I haven't evaluated whether or not this causes problems, but it *should* be fine, as the exception is also caught internally.

### Fixed

- A workaround has been implemented to retrieve the version number for Bluetooth HID devices on Linux.
- Calling `InputSystem.RemoveDevice` with a HIDrogen device will now also remove the backend device that feeds events to it.
- Other device removal issues have been fixed.

### Removed

- The `HIDROGEN_KEEP_NATIVE_DEVICES` define has been removed, as I can't think of any actual genuine use cases for it. All it will end up doing is creating duplicate device instances, and the native instance usually has broken controls.

## [0.2.0] - 2023/05/01

### Fixed

- Re-added using directive that's needed for actual Unity player builds.
- Fixed HID usage check always failing for hidapi versions below v0.10.1.
- Fixed Linux shim device removals always being logged, even if the device wasn't actually removed.
- Fixed hidapi handles not being disposed immediately when failing to add devices to the input system.
- Fixed device read buffer size including the report ID prepend count.

### Added

- Added device path to device open failure log ([GH-2](https://github.com/TheNathannator/HIDrogen/pull/2)).
- Added additional logging for device change events for the `Linux` and `HID` interfaces.
- Added `HIDROGEN_KEEP_NATIVE_DEVICES` compile define to allow for keeping the native-backend versions of devices instead of removing them.

### Changed

- Removed explicit newlines in most log messages, to keep things on a single line in log files.
- Device removal is now handled by queuing a removal event instead of removing the device itself immediately.
  - I hoped this would allow them to show up as disconnected instead of just disappearing entirely, but it appears that's reserved for native devices only.
- **The report ID byte is no longer enforced by default.** The input system on Mac does not include the report ID in its HID support, so this was done to make it easier to account by only needing to include the report ID on Windows. The `HIDROGEN_FORCE_REPORT_IDS` compile define has been added if you wish to always have the report ID.

## [0.1.7] - 2023/04/27

### Fixed

- Hopefully fixed shimmed devices not always being removed, by searching through the device list for them on initializiation.

### Added

- A ton of verbose logging has been added, which can be enabled by defining `HIDROGEN_VERBOSE_LOGGING`.

## [0.1.6] - 2023/04/24

### Fixed

- HID output commands now work correctly ([GH-1](https://github.com/TheNathannator/HIDrogen/pull/1)).

## [0.1.5] - 2023/04/20

### Fixed

- Report ID detection should hopefully work correctly for all devices now.
- Report ID detection also now adjusts the bit offsets and report lengths provided in the `capabilities` info to compensate for the additional byte being added to the input report data.
- The library name used for imports has been changed, it should now load without requiring `libhidapi-dev` to be installed.

## [0.1.4] - 2023/04/15

### Changed

- Device removal handling is now completed on the main thread instead of the read thread, in order to avoid issues with device change calbacks using APIs that are only available on the main thread.
  - This should hopefully be the last threading issue involving InputSystem methods, all code paths that call them are now done on the main thread and whatever thread handles exiting/assembly reloads.
- Device removal is now properly detected and no longer relies on the error counting mechanism. This also means that errors are no longer logged when disconnecting a device.
- Error logging for hidapi calls will now display error numbers/names alongside the message retrieved from hidapi.

## [0.1.3] - 2023/04/15

### Fixed

- The format of the device description's `version` property now matches how it is formatted on Windows. Instead of a point-separated version number, it is just the raw binary-coded decimal value turned into a base-10 string.

## [0.1.2] - 2023/04/13

### Fixed

- The Windows #if directive for hidapi wide-string conversion has been fixed.
- The package will no longer cause builds to fail on platforms that are not handled in the hidapi wide-string conversion method.
- Added missing Linux #if directives around udev device monitoring to fix other compilation errors on non-Linux platforms.

## [0.1.1] - 2023/04/10

### Changed

- Devices with the `SDL` interface are no longer automatically removed, as they do not seem to coincide with hidapi devices.

## [0.1.0] - 2023/04/09

### Added

- Devices with the `Linux` or `SDL` interface types will automatically be removed from the input system.
- Devices reported by hidapi will be added to the input system, with information that is almost (if not exactly) identical to what is reported on Windows. This includes descriptor information being included in the device's `capabilities` string.
  - Only devices with usages listed as supported by the input system will be added. To support additional usages, replace the value of `UnityEngine.InputSystem.HID.HIDSupport.supportedHIDUsages` with a new list of all the desired usages to support.
  - libudev is used to detect device connections and disconnections, after which hidapi will be re-enumerated. The handling for libudev is error-tolerant, and will fall back to just periodically re-enumerating hidapi if enough errors happen.
- Input reports are polled on a background thread, and come in as soon as they're available.
  - This polling is also error-tolerant, and will only trigger removal of the device if enough consecutive errors occur.
- Input events are buffered separately from the input system's event buffer, since it is not fully thread-safe. This custom buffer is flushed to the input system's before every input system update (i.e. before every frame), and timestamp information is preserved.
- Most HID device commands are handled. Currently, this includes:
  - Output reports (`HIDO`)
  - Binary descriptor size (`HIDS`)
  - Binary descriptor (`HIDD`)
  - Pre-parsed string descriptor (`HIDP`)
