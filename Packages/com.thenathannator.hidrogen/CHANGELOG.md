# Changelog

All notable changes to HIDrogen will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Dates are relative to UTC.

## Unreleased

### Changed

- Devices with the `SDL` interface are no longer automatically removed, as they do not seem to coincide with hidapi devices.

## [0.1.0] - 2023/09/04

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
