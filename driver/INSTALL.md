# BrightnessVHid - виртуальный HID-драйвер яркости

Назначение: выдаёт системе настоящую HID consumer-команду яркости
(Brightness Increment/Decrement, 0x6F/0x70). Windows воспринимает её как
аппаратную клавишу яркости - меняет яркость И показывает нативный OSD.
Приложение HonorPCHelper на жест левого края шлёт драйверу IOCTL.

## Что нужно один раз

1. EWDK (Enterprise WDK) - бесплатный автономный тулчейн (без Visual Studio).
   Скачать ISO "Enterprise WDK" для своей версии Windows, смонтировать.

2. Включить test-signing (самоподписанный драйвер иначе не загрузится на x64):
   ```
   bcdedit /set testsigning on
   ```
   Перезагрузиться. На рабочем столе появится водяной знак "Test Mode" - это норма.

## Сборка

1. Открыть среду сборки: запустить из смонтированного EWDK `LaunchBuildEnv.cmd`.
2. В этом окне:
   ```
   cd /d D:\Desktop\Code\HonorPCHelper\driver
   build.cmd
   ```
   Результат: `driver\BrightnessVHid\x64\Release\BrightnessVHid\` (BrightnessVHid.sys, .inf, .cat).

## Подпись + установка

В том же окне EWDK, от администратора:
```
sign_and_install.cmd      REM создаёт самоподписанный сертификат, доверяет ему, подписывает .sys/.cat
install_device.cmd        REM ставит root-устройство (devcon)
```

Проверка: в Диспетчере устройств появится "Brightness Virtual HID" (System devices)
и новое "HID-compliant consumer control device".

## Проверка работы

В приложении HonorPCHelper включена интеграция: на жест яркости оно шлёт IOCTL драйверу.
Должен появиться нативный OSD и меняться яркость. Если драйвер не найден - приложение
автоматически откатывается на смену яркости через WMI (без OSD).

## Удаление

```
devcon remove root\BrightnessVHid
pnputil /delete-driver oemXX.inf /uninstall   REM oemXX - имя из pnputil /enum-drivers
bcdedit /set testsigning off                   REM если test-signing больше не нужен
```

## Примечания

- Драйвер автономен и не зависит от Honor PC Manager.
- Файлы сертификата (bvhid.pfx/bvhid.cer) создаются в папке driver - это тестовый
  сертификат только для вашей машины, не распространять.
