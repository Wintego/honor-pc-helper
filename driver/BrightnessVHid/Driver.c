/*++
    BrightnessVHid - virtual HID source driver (KMDF + Virtual HID Framework).

    Exposes a Consumer Control HID device with Brightness Increment / Decrement
    usages. When user mode sends IOCTL_BVHID_SEND, the driver injects a real HID
    consumer-control input report via VhfReadReportSubmit. Windows treats this as
    a genuine hardware brightness key: it changes panel brightness AND shows the
    native brightness OSD flyout.

    Root-enumerated. Install via INF + pnputil (see INSTALL.md).
--*/

#include <ntddk.h>
#include <wdf.h>
#include <hidclass.h>
#include <vhf.h>
#include "Public.h"

// ---- HID report descriptor: Consumer Control with Brightness Inc/Dec ----
// Report ID 1, 1 data byte: bit0 = Brightness Increment, bit1 = Brightness Decrement.
#define REPORT_ID_CONSUMER 0x01

static const UCHAR g_ReportDescriptor[] =
{
    0x05, 0x0C,        // Usage Page (Consumer)
    0x09, 0x01,        // Usage (Consumer Control)
    0xA1, 0x01,        // Collection (Application)
    0x85, REPORT_ID_CONSUMER, //   Report ID (1)
    0x15, 0x00,        //   Logical Minimum (0)
    0x25, 0x01,        //   Logical Maximum (1)
    0x75, 0x01,        //   Report Size (1)
    0x95, 0x02,        //   Report Count (2)
    0x09, 0x6F,        //   Usage (Brightness Increment)
    0x09, 0x70,        //   Usage (Brightness Decrement)
    0x81, 0x02,        //   Input (Data,Var,Abs)
    0x75, 0x01,        //   Report Size (1)
    0x95, 0x06,        //   Report Count (6)
    0x81, 0x03,        //   Input (Const,Var,Abs)  ; padding to a full byte
    0xC0               // End Collection
};

typedef struct _DEVICE_CONTEXT {
    VHFHANDLE VhfHandle;
} DEVICE_CONTEXT, *PDEVICE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(DEVICE_CONTEXT, GetDeviceContext)

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD BvhidEvtDeviceAdd;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL BvhidEvtIoDeviceControl;
EVT_WDF_OBJECT_CONTEXT_CLEANUP BvhidDeviceCleanup;

VOID
BvhidDeviceCleanup(_In_ WDFOBJECT Device)
{
    PDEVICE_CONTEXT ctx = GetDeviceContext((WDFDEVICE)Device);
    if (ctx != NULL && ctx->VhfHandle != WDF_NO_HANDLE) {
        VhfDelete(ctx->VhfHandle, TRUE);
        ctx->VhfHandle = NULL;
    }
}

static void BvhidSubmitReport(_In_ PDEVICE_CONTEXT ctx, _In_ UCHAR dataByte)
{
    // Report buffer includes the report ID as the first byte.
    UCHAR report[2];
    HID_XFER_PACKET packet;

    report[0] = REPORT_ID_CONSUMER;
    report[1] = dataByte;

    RtlZeroMemory(&packet, sizeof(packet));
    packet.reportId = REPORT_ID_CONSUMER;
    packet.reportBuffer = report;
    packet.reportBufferLen = sizeof(report);

    (void)VhfReadReportSubmit(ctx->VhfHandle, &packet);
}

VOID
BvhidEvtIoDeviceControl(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request,
    _In_ size_t OutputBufferLength,
    _In_ size_t InputBufferLength,
    _In_ ULONG IoControlCode)
{
    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;
    PDEVICE_CONTEXT ctx = GetDeviceContext(WdfIoQueueGetDevice(Queue));

    UNREFERENCED_PARAMETER(OutputBufferLength);

    if (IoControlCode == IOCTL_BVHID_SEND) {
        PVOID inBuf = NULL;
        size_t inLen = 0;
        if (InputBufferLength >= 1 &&
            NT_SUCCESS(WdfRequestRetrieveInputBuffer(Request, 1, &inBuf, &inLen))) {
            UCHAR dir = *(UCHAR*)inBuf;
            UCHAR pressBit = 0;
            if (dir == BVHID_DIR_UP)   pressBit = 0x01; // bit0 = Brightness Increment
            else if (dir == BVHID_DIR_DOWN) pressBit = 0x02; // bit1 = Brightness Decrement

            if (pressBit != 0 && ctx->VhfHandle != NULL) {
                BvhidSubmitReport(ctx, pressBit); // press
                BvhidSubmitReport(ctx, 0x00);     // release
                status = STATUS_SUCCESS;
            } else {
                status = STATUS_INVALID_PARAMETER;
            }
        } else {
            status = STATUS_BUFFER_TOO_SMALL;
        }
    }

    WdfRequestComplete(Request, status);
}

NTSTATUS
BvhidEvtDeviceAdd(
    _In_ WDFDRIVER Driver,
    _Inout_ PWDFDEVICE_INIT DeviceInit)
{
    NTSTATUS status;
    WDF_OBJECT_ATTRIBUTES attributes;
    WDFDEVICE device;
    PDEVICE_CONTEXT ctx;
    VHF_CONFIG vhfConfig;
    WDF_IO_QUEUE_CONFIG queueConfig;
    WDFQUEUE queue;

    UNREFERENCED_PARAMETER(Driver);

    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, DEVICE_CONTEXT);
    attributes.EvtCleanupCallback = BvhidDeviceCleanup;

    status = WdfDeviceCreate(&DeviceInit, &attributes, &device);
    if (!NT_SUCCESS(status)) {
        return status;
    }
    ctx = GetDeviceContext(device);

    // Default sequential queue for IOCTLs from user mode.
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queueConfig, WdfIoQueueDispatchSequential);
    queueConfig.EvtIoDeviceControl = BvhidEvtIoDeviceControl;
    status = WdfIoQueueCreate(device, &queueConfig, WDF_NO_OBJECT_ATTRIBUTES, &queue);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    // Device interface for user-mode access.
    status = WdfDeviceCreateDeviceInterface(device, &GUID_DEVINTERFACE_BRIGHTNESSVHID, NULL);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    // Create and start the virtual HID source.
    VHF_CONFIG_INIT(&vhfConfig,
                    WdfDeviceWdmGetDeviceObject(device),
                    (USHORT)sizeof(g_ReportDescriptor),
                    (PUCHAR)g_ReportDescriptor);

    status = VhfCreate(&vhfConfig, &ctx->VhfHandle);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    status = VhfStart(ctx->VhfHandle);
    if (!NT_SUCCESS(status)) {
        VhfDelete(ctx->VhfHandle, TRUE);
        ctx->VhfHandle = NULL;
        return status;
    }

    return STATUS_SUCCESS;
}

NTSTATUS
DriverEntry(
    _In_ PDRIVER_OBJECT DriverObject,
    _In_ PUNICODE_STRING RegistryPath)
{
    WDF_DRIVER_CONFIG config;
    WDF_DRIVER_CONFIG_INIT(&config, BvhidEvtDeviceAdd);
    return WdfDriverCreate(DriverObject, RegistryPath,
                           WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
}
