# MacDock — Installation & Usage Guide / Hướng dẫn cài đặt & sử dụng

[English](#english) · [Tiếng Việt](#tiếng-việt)

---

## English

### 1. Requirements
- Windows 11 (or Windows 10 version 1903 or later), 64-bit (x64).
- No extra software: the release already includes .NET.
- No administrator rights are needed.

### 2. Install
1. Download `MacDock-win-x64.zip` from [Releases](https://github.com/Vudinhdat02/appThemeMacOS/releases).
2. Extract it to a **permanent** folder, for example `%LOCALAPPDATA%\Programs\MacDock`
   (paste that path into File Explorer's address bar). Don't run it from Downloads and delete it later:
   the startup task points to wherever `MacDock.exe` lives.
3. Double-click `MacDock.exe`.
   - If SmartScreen shows *"Windows protected your PC"*, click **More info → Run anyway** (the file isn't code-signed yet).
   - Some antivirus tools may warn because MacDock listens to the volume keys and Win+Space (keyboard hook). It doesn't record anything.
   - The very first run takes a few seconds longer (files are unpacked once).
4. On the welcome screen choose whether to start with Windows, hide the Windows taskbar, show the menu bar,
   enable window previews, and pick a theme. Click **Get Started**.

When "Start with Windows" is on, MacDock creates a Task Scheduler task named `MacDock Autostart (<your user name>)`
that starts it right after you sign in. You don't have to change anything in Windows yourself.

### 3. Using the Dock
| Action | How |
|---|---|
| Open / switch to an app | Click its icon. Click again to minimize; with several windows, clicks cycle through them. |
| See an app's windows | Hover a running app: live previews appear. Click one to switch, × to close it. |
| App menu | Right-click an icon: window list, New Window, Keep in Dock, Move Left/Right, Show in Explorer, Change Icon, Hide, Quit, Force Quit. |
| Add an app | Right-click Launchpad or the divider → **Add Apps to Dock…**, or Settings → Dock → **Add apps…**, or drag an `.exe`/`.lnk`/folder onto the Dock. |
| Rearrange / remove | Drag icons left/right; drag an icon out of the Dock (or onto Trash) to remove it. |
| Open files with an app | Drop files onto the app's icon. |
| Delete files | Drop files onto Trash (goes to the Recycle Bin, can be undone). |
| Dock options | Right-click Launchpad or the divider: auto-hide, magnification, position, theme. |

**Auto-hide:** with *Only hide when a window covers the Dock* on, the Dock stays visible on an empty desktop and hides
when a window overlaps it. Touch the screen edge anywhere to bring it back instantly.

### 4. Using the menu bar
- **Round logo (top left):** About This PC, Windows Settings, MacDock Settings, Task Manager, Sleep, Restart, Shut Down, Lock, Log Out.
- **App name + menus:** the active app's real menus when available, otherwise generic File/Edit/View/Window/Help menus.
- **System tray icons:** left-click, right-click and double-click work like on the taskbar. Hide individual icons in Settings → Menu Bar.
- **Input method (EN/VI):** click to choose a layout; **Win+Space** / **Shift+Win+Space** switches too.
- **Battery, Wi-Fi, volume:** click for details. In the Wi-Fi popup, click a network to connect (a password box appears if needed). Scroll on the volume icon to change the volume.
- **Control Center:** Wi-Fi, Bluetooth, dark mode, brightness and volume.
- **Bell:** opens Windows notifications (Win+N). **Clock:** opens a small calendar.
- **Volume keys:** show a glass overlay (choose the Windows overlay instead in Settings → General).

### 5. Settings
Open from the logo menu → **MacDock Settings…**, or run `MacDock.exe` again while it's running.
- **General:** language (Tiếng Việt / English — MacDock restarts), start with Windows, hide the Windows taskbar, volume/brightness overlay, recovery.
- **Dock:** pinned apps, size, magnification, previews, bounce, auto-hide, position, displays.
- **Menu Bar:** height, background style and liquid amount, text color, top-left icon, clock format, right-side icons, tray icons.
- **Appearance:** themes, light/dark/scheduled mode, corner radius, opacity, font, import/export `.macdocktheme`.
- **Icons:** original or squircle icons, icon packs, per-app icons.
- **Performance:** FPS limit, disable animations, reduce on battery, live RAM/CPU.

### 6. Restoring the Windows taskbar
MacDock always restores the taskbar when it exits. If it ever doesn't come back, run:
```bash
MacDock.exe --restore-taskbar
```
or turn off **Hide the Windows taskbar** in Settings → General.

### 7. Uninstall
1. Settings → General → turn off **Start with Windows** (removes the startup task), then quit MacDock
   (logo menu → MacDock Settings → General → **Quit MacDock**, or right-click the Dock divider → **Quit MacDock**).
2. Delete the folder where you extracted MacDock.
3. Optionally delete your settings: `%APPDATA%\MacDock` and the icon cache `%LOCALAPPDATA%\MacDock`.

If the startup task is still there, remove it with:
```bash
schtasks /delete /tn "MacDock Autostart (%USERNAME%)" /f
```

### 8. Troubleshooting
| Problem | Fix |
|---|---|
| Taskbar didn't come back | `MacDock.exe --restore-taskbar` |
| Dock/menu bar hidden behind a window | Wait 2 seconds (MacDock repairs this automatically) or restart MacDock. |
| A tray icon is missing | Settings → Menu Bar → check *Current system tray icons*; some apps only add their icon after restarting them. |
| Something else | Settings → About → **Open log** and include `macdock.log` when reporting an issue. |

---

## Tiếng Việt

### 1. Yêu cầu
- Windows 11 (hoặc Windows 10 bản 1903 trở lên), 64-bit (x64).
- Không cần cài thêm gì: bản phát hành đã kèm .NET.
- Không cần quyền quản trị (admin).

### 2. Cài đặt
1. Tải `MacDock-win-x64.zip` ở mục [Releases](https://github.com/Vudinhdat02/appThemeMacOS/releases).
2. Giải nén vào một thư mục **cố định**, ví dụ `%LOCALAPPDATA%\Programs\MacDock`
   (dán đường dẫn này vào thanh địa chỉ của File Explorer). Đừng chạy trong thư mục Downloads rồi xoá file,
   vì tác vụ tự khởi động trỏ đúng vào chỗ đặt `MacDock.exe`.
3. Bấm đúp `MacDock.exe`.
   - Nếu SmartScreen báo *"Windows đã bảo vệ máy tính của bạn"*, bấm **Thông tin bổ sung → Vẫn chạy** (file chưa có chữ ký số).
   - Một số phần mềm diệt virus có thể cảnh báo vì MacDock nghe phím âm lượng và Win+Space (hook bàn phím). MacDock không ghi lại gì cả.
   - Lần chạy đầu tiên lâu hơn vài giây (giải nén thư viện một lần).
4. Ở màn hình chào, chọn có khởi động cùng Windows, ẩn taskbar Windows, hiện Thanh Menu, xem trước cửa sổ hay không
   và chọn chủ đề. Bấm **Bắt đầu sử dụng**.

Khi bật "Khởi động cùng Windows", MacDock tạo tác vụ Task Scheduler tên `MacDock Autostart (<tên người dùng>)`
để chạy ngay sau khi đăng nhập. Bạn không cần tự chỉnh gì trong Windows.

### 3. Dùng Dock
| Việc | Cách làm |
|---|---|
| Mở / chuyển sang app | Bấm icon. Bấm lại để thu nhỏ; app có nhiều cửa sổ thì mỗi lần bấm chuyển sang cửa sổ tiếp theo. |
| Xem các cửa sổ của app | Rê chuột lên app đang chạy: hiện ảnh xem trước. Bấm để chuyển, nút × để đóng. |
| Menu của app | Chuột phải lên icon: danh sách cửa sổ, Mở cửa sổ mới, Giữ trên Dock, chuyển trái/phải, Hiện trong Explorer, Đổi biểu tượng, Ẩn, Thoát, Buộc thoát. |
| Thêm app | Chuột phải lên Launchpad hoặc vạch ngăn → **Thêm ứng dụng vào Dock…**, hoặc Cài đặt → Dock → **Thêm ứng dụng…**, hoặc kéo file `.exe`/`.lnk`/thư mục vào Dock. |
| Sắp xếp / gỡ | Kéo icon sang trái/phải; kéo icon ra khỏi Dock (hoặc vào Thùng rác) để gỡ. |
| Mở file bằng app | Thả file lên icon của app. |
| Xoá file | Thả file vào Thùng rác (vào Thùng rác của Windows, có thể khôi phục). |
| Tuỳ chọn Dock | Chuột phải lên Launchpad hoặc vạch ngăn: tự ẩn, phóng to, vị trí, giao diện. |

**Tự ẩn:** khi bật *Chỉ ẩn khi có cửa sổ che Dock*, Dock luôn hiện ở màn hình chính và chỉ ẩn khi có cửa sổ đè lên.
Chạm chuột vào mép màn hình ở bất kỳ đâu là Dock hiện ngay.

### 4. Dùng Thanh Menu
- **Logo tròn (góc trái):** Giới thiệu máy này, Cài đặt Windows, Cài đặt MacDock, Task Manager, Ngủ, Khởi động lại, Tắt máy, Khoá, Đăng xuất.
- **Tên app + menu:** menu thật của app đang dùng nếu có, không thì menu chung Tệp/Sửa/Xem/Cửa sổ/Trợ giúp.
- **Icon khay hệ thống:** bấm trái, bấm phải, bấm đúp như trên taskbar. Ẩn từng icon trong Cài đặt → Thanh Menu.
- **Bộ gõ (EN/VI):** bấm để chọn; **Win+Space** / **Shift+Win+Space** cũng đổi được.
- **Pin, Wi-Fi, âm lượng:** bấm để xem chi tiết. Trong popup Wi-Fi, bấm vào một mạng để kết nối (cần mật khẩu thì hiện hộp nhập). Lăn chuột trên icon âm lượng để chỉnh âm lượng.
- **Trung tâm điều khiển:** Wi-Fi, Bluetooth, chế độ tối, độ sáng, âm lượng.
- **Chuông:** mở thông báo của Windows (Win+N). **Đồng hồ:** mở lịch nhỏ.
- **Phím âm lượng:** hiện bảng kính (chọn dùng bảng của Windows trong Cài đặt → Chung).

### 5. Cài đặt
Mở từ menu logo → **Cài đặt MacDock…**, hoặc chạy lại `MacDock.exe` khi nó đang chạy.
- **Chung:** ngôn ngữ (Tiếng Việt / English — MacDock tự khởi động lại), khởi động cùng Windows, ẩn taskbar Windows, bảng âm lượng/độ sáng, khôi phục.
- **Dock:** app đã ghim, kích thước, phóng to, xem trước, nảy, tự ẩn, vị trí, màn hình.
- **Thanh Menu:** chiều cao, kiểu nền và độ liquid, màu chữ, biểu tượng góc trái, định dạng đồng hồ, biểu tượng bên phải, icon khay.
- **Giao diện:** chủ đề, chế độ sáng/tối/theo giờ, bo góc, độ đậm, phông chữ, nhập/xuất `.macdocktheme`.
- **Biểu tượng:** icon gốc hoặc squircle, icon pack, đổi icon từng app.
- **Hiệu năng:** giới hạn FPS, tắt hiệu ứng, giảm khi dùng pin, đo RAM/CPU.

### 6. Khôi phục taskbar Windows
MacDock luôn khôi phục taskbar khi thoát. Nếu taskbar không hiện lại, chạy:
```bash
MacDock.exe --restore-taskbar
```
hoặc tắt **Ẩn thanh tác vụ của Windows** trong Cài đặt → Chung.

### 7. Gỡ cài đặt
1. Cài đặt → Chung → tắt **Khởi động cùng Windows** (xoá tác vụ tự khởi động), rồi thoát MacDock
   (menu logo → Cài đặt MacDock → Chung → **Thoát MacDock**, hoặc chuột phải lên vạch ngăn của Dock → **Thoát MacDock**).
2. Xoá thư mục đã giải nén MacDock.
3. Nếu muốn xoá cả cài đặt: xoá `%APPDATA%\MacDock` và bộ nhớ đệm icon `%LOCALAPPDATA%\MacDock`.

Nếu tác vụ tự khởi động vẫn còn, xoá bằng lệnh:
```bash
schtasks /delete /tn "MacDock Autostart (%USERNAME%)" /f
```

### 8. Xử lý sự cố
| Vấn đề | Cách xử lý |
|---|---|
| Taskbar không hiện lại | `MacDock.exe --restore-taskbar` |
| Dock/Thanh Menu bị cửa sổ che | Chờ 2 giây (MacDock tự sửa) hoặc khởi động lại MacDock. |
| Thiếu icon khay | Cài đặt → Thanh Menu → xem *Icon khay hệ thống đang có*; một số app chỉ thêm icon sau khi mở lại app đó. |
| Lỗi khác | Cài đặt → Giới thiệu → **Mở log**, gửi kèm file `macdock.log` khi báo lỗi. |
