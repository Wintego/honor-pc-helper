// Public.h - shared definitions between the BrightnessVHid driver and user-mode app.
#pragma once

#include <initguid.h>

// Device interface GUID exposed by the driver for user-mode control.
// {A3F8E2C1-4B6D-4E9A-9C2F-1D7B8E5A6C30}
DEFINE_GUID(GUID_DEVINTERFACE_BRIGHTNESSVHID,
    0xA3F8E2C1, 0x4B6D, 0x4E9A, 0x9C, 0x2F, 0x1D, 0x7B, 0x8E, 0x5A, 0x6C, 0x30);

// IOCTL: inject a brightness consumer keypress.
// Input buffer: 1 byte. 1 = Brightness Up, 2 = Brightness Down.
// The driver submits a "press" report followed by a "release" report.
#define IOCTL_BVHID_SEND \
    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_WRITE_ACCESS)

#define BVHID_DIR_UP    ((unsigned char)1)
#define BVHID_DIR_DOWN  ((unsigned char)2)
