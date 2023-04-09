# HIDrogen

(pronounced as either "hydrogen" or "hid-rogen")

An add-on for the [Unity InputSystem](https://github.com/Unity-Technologies/InputSystem) package that provides proper HID device support on Linux.

Most device handling features are implemented, including raw input reports, device descriptor retrieval/parsing, and input system IOCTLs. The backend is also pretty robust, with input event double-buffering, some error tolerance, and a fallback device enumeration method.

If there's anything in the native Windows or Mac HID backends that is not implemented here, feel free to contribute!

## Dependencies

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

## Notes

As part of supporting HID devices properly, devices that come from the native backend under the `Linux` interface are automatically disabled. This is only being mentioned for transparency's sake, there should be little to no consequences to it.

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
