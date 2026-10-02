# hygeia

hygeia 0.1.0-alpha.1 控制本机两路风扇，并显示温度。发布者是 Cong Dev。

## 支持情况

- 机型：COLORFIRE MEOW R15 24，锐龙 7 8845HS 与 RTX 4070 Laptop。主板风扇协议是 Clevo AcpiBridge。其他机型不在这版范围内。
- 系统：64 位 Windows 11。本机需要已经安装 AcpiBridge 驱动。程序不加载 WinRing0。
- 功能：读取两路风扇的转速、占空比和散热器温度。自动模式按温度条件调速，条件可以增加和删除。手动模式用两根滑条。温度达到 95°C 时两路风扇临时拉到 100%，降到 90°C 以下后回到当前模式。关闭窗口后程序留在托盘里继续调速。从托盘退出，或温度全部读失败时，风扇交回主板固件。可以勾选开机时在后台运行。界面语言为简体中文或 English。窗口里可以查看计算机型号、Windows 版本、处理器、显卡、BIOS、网卡、声卡、蓝牙，以及 AcpiBridge 驱动是否在运行。
- CPU 大号温度：读到有效核心温度时显示核心温度，否则显示风扇数据里的 CPU 散热器温度。GPU 优先使用 nvidia-smi 的核心温度，读不到时使用 GPU 散热器温度。
- 两路风扇都设为 100% 占空比时，转速可以不同。右侧风扇在满功率下大约有百分之几的起伏，这是风扇自己的转速，不是故障。

设置写在程序目录的 `data\settings.json`。安装到其他盘时，程序文件和设置都留在所选文件夹里。

## 使用方法

离线版：解压 `hygeia-0.1.0-alpha.1-portable.zip`，运行里面的 `hygeia.exe`。

安装包：运行 `hygeia-0.1.0-alpha.1-setup.exe`。安装时可以选择路径和语言。默认路径是 `C:\Program Files\hygeia`。安装语言会作为第一次打开时的界面语言。

调速：选择自动，按温度条件调节；选择手动，拖动 CPU 和 GPU 滑条。大约一秒后写入。

退出：使用托盘菜单里的退出。只关闭窗口时，程序继续按当前模式调速。

开机启动：在界面中勾选“开机时在后台运行”。任务管理器的启动应用里，发布者显示 Cong Dev。

# hygeia

hygeia 0.1.0-alpha.1 controls the two fans on this computer and shows temperatures. The publisher is Cong Dev.

## Support

- Computer: COLORFIRE MEOW R15 24 with Ryzen 7 8845HS and RTX 4070 Laptop. The fan protocol is Clevo AcpiBridge. Other computers are outside this release.
- System: 64-bit Windows 11, with the AcpiBridge driver already installed. hygeia does not load WinRing0.
- Features: read both fans' speed, duty, and heatsink temperature. Auto mode follows temperature points, and points can be added or removed. Manual mode uses two sliders. At 95°C both fans go to 100% until the temperature falls to 90°C. Closing the window leaves hygeia in the tray. Fans return to firmware when you exit from the tray, or when every temperature read fails. hygeia can start in the background when Windows starts. The interface is Simplified Chinese or English. The window can show the computer model, Windows version, processor, graphics, BIOS, network, audio, and Bluetooth driver versions, and whether the AcpiBridge driver is running.
- The large CPU temperature shows the core temperature when that reading is valid. Otherwise it shows the CPU heatsink temperature from the fan data. GPU prefers the nvidia-smi core temperature, then the GPU heatsink temperature.
- At 100% duty the two fans can spin at different speeds. The right fan can vary by a few percent at full power. That is the fan's own speed.

Settings are stored in `data\settings.json` inside the program folder. If you install hygeia on another drive, its files and settings stay in the folder you chose.

## Use

Portable build: unzip `hygeia-0.1.0-alpha.1-portable.zip` and run `hygeia.exe`.

Installer: run `hygeia-0.1.0-alpha.1-setup.exe`. Choose the install folder and language. The default folder is `C:\Program Files\hygeia`. The language you choose is the interface language on first launch.

Speed: choose Auto to follow the temperature points, or Manual and move the CPU and GPU sliders. The duty is written after about a second.

Exit: use Exit in the tray menu. Closing the window keeps the current mode running.

Startup: check “Run in the background at startup”. In Task Manager Startup apps, the publisher is Cong Dev.
