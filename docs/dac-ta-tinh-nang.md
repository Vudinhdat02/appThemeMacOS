# Đặc tả tính năng MacDock 0.2

Tài liệu mô tả cách MacDock hoạt động bên trong, dành cho người muốn đọc hoặc sửa mã nguồn.

## 1. Kiến trúc

| Thư mục | Nội dung |
|---|---|
| `src/MacDock/Native` | P/Invoke (user32, shell32, dwm…), nạp icon qua `IShellItemImageFactory`, Core Audio, Wi-Fi (wlanapi) |
| `src/MacDock/Core` | Cấu hình JSON, ẩn/khôi phục taskbar, AppBar, màn hình, nhận diện app, theo dõi cửa sổ, hành động |
| `src/MacDock/Tray` | Chủ khay hệ thống (`Shell_TrayWnd` riêng) |
| `src/MacDock/Dock` | Mô hình mục Dock, vẽ + tương tác (`DockView`), cửa sổ theo màn hình |
| `src/MacDock/MenuBar` | Thanh Menu, menu app (Win32/UIA), popup trạng thái, dịch vụ trạng thái |
| `src/MacDock/Theme` | Theme engine |
| `src/MacDock/UI` | Cài đặt, màn hình chào, Giới thiệu máy này, icon vector, style XAML |

Một tiến trình duy nhất (mutex `Local\MacDock.SingleInstance`). Chạy lại `MacDock.exe` khi đang chạy sẽ gửi
thông điệp `MacDock.OpenSettings` tới cửa sổ ẩn `MacDock.Messaging` để mở Cài đặt.

## 2. Dock

- Cửa sổ WPF `AllowsTransparency`, `Topmost`, không hiện trên taskbar, không kích hoạt
  (`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, trả `MA_NOACTIVATE`). Vùng alpha = 0 cho chuột đi xuyên qua;
  mỗi cột icon được vẽ nền alpha 1 để bắt chuột.
- Toàn bộ Dock vẽ bằng `OnRender` trong hệ toạ độ trừu tượng (u dọc theo Dock, v từ cạnh màn hình vào),
  nên cùng một mã chạy cho cạnh dưới/trái/phải.
- **Phóng to**: vị trí chuột trong bố cục đang phóng được ánh xạ về bố cục gốc; hệ số mục tiêu
  `1 + (max-1)·(cos(πx)+1)/2` với `x = khoảng cách / (độ rộng đường cong × (kích thước + khoảng cách))`;
  làm mượt `k = 1 − e^(−dt·tốc độ)`.
- **Vòng lặp vẽ**: chỉ chạy `CompositionTarget.Rendering` khi đang rê chuột / có hiệu ứng; chuột đứng yên
  hoặc ra ngoài thì dừng, quay về bộ hẹn giờ 50 ms rất nhẹ (`GetCursorPos`). Toạ độ chuột lấy theo cửa sổ,
  không theo phần tử (vì phần tử dịch khi tự ẩn). Giới hạn FPS áp dụng khi vẽ.
- **Nảy**: parabol `4·p·(1−p)`, 3 lần khi mở app, 2 lần cao hơn khi cửa sổ nhấp nháy (`HSHELL_FLASH`).
- **Bấm icon**: chưa chạy → mở (+nảy); đang chạy nhưng không ở trước → đưa cửa sổ gần nhất lên;
  đang ở trước → 1 cửa sổ thì thu nhỏ, nhiều cửa sổ thì chuyển vòng.
- **Chuột phải**: danh sách cửa sổ, Mở cửa sổ mới, Tuỳ chọn (Giữ trên Dock, chuyển trái/phải, Hiện trong
  Explorer, đổi icon), Ẩn, Thoát, Buộc thoát. Chuột phải lên Launchpad/vạch ngăn: tuỳ chọn Dock + đổi theme nhanh.
- **Kéo thả**: kéo icon để sắp xếp (tự ghim), kéo ra xa Dock hoặc vào Thùng rác để gỡ; thả file từ Explorer:
  lên app → mở bằng app; lên Thùng rác → `SHFileOperation(FO_DELETE, FOF_ALLOWUNDO)`; chỗ khác → ghim exe/lnk/thư mục.
- **Huy hiệu**: chấm đỏ khi cửa sổ nhấp nháy; số khi tiêu đề bắt đầu bằng `(n)` / `[n]`.
- **AppBar** (`ABM_NEW/QUERYPOS/SETPOS`) để chừa chỗ khi không tự ẩn; ẩn Dock/Thanh Menu trên màn hình đang có
  cửa sổ phủ kín và không `IsZoomed` (game, video toàn màn hình), kiểm tra khi đổi cửa sổ foreground,
  khi nhận `ABN_FULLSCREENAPP` và mỗi 3 giây.

## 3. Nhận diện ứng dụng & cửa sổ

- Liệt kê kiểu Alt-Tab: hiện, không owner (trừ `WS_EX_APPWINDOW`), không toolwindow/noactivate, không bị DWM
  cloak, có tiêu đề, không thuộc lớp shell (Progman, Shell_TrayWnd…).
- App đóng gói (ApplicationFrameHost, `\WindowsApps\`, `\SystemApps\`) nhóm theo AUMID
  (`SHGetPropertyStoreForWindow` + `PKEY_AppUserModel_ID`, dự phòng `GetApplicationUserModelId`); app thường
  nhóm theo đường dẫn exe (`QueryFullProcessImageName`). Exe chạy từ thư mục con theo phiên bản
  (vd `Zalo\Zalo-25.x\Zalo.exe`) vẫn khớp mục ghim `Zalo\Zalo.exe`.
- Sự kiện: shell hook (`RegisterShellHookWindow`: tạo/huỷ/kích hoạt/đổi tiêu đề/nhấp nháy) +
  `SetWinEventHook` (foreground, thu nhỏ, cloak). Chỉ quét lại `EnumWindows` (~1 ms) khi có sự kiện, gộp 120 ms,
  kèm quét dự phòng mỗi 10 giây.
- Icon: `SHCreateItemFromParsingName` → `IShellItemImageFactory::GetImage(SIIGBF_ICONONLY|BIGGERSIZEOK)` →
  HBITMAP → `GetDIBits` top-down → `Pbgra32`; app Store dùng `shell:AppsFolder\<AUMID>`. Nạp trên luồng STA riêng.

## 4. Taskbar gốc

- Ẩn: đọc `ABM_GETSTATE`, **ghi trạng thái gốc ra `taskbar-state.json` trước**, đặt `ABS_AUTOHIDE`, `SW_HIDE`
  cho `Shell_TrayWnd` và mọi `Shell_SecondaryTrayWnd`. Kiểm tra lại mỗi 3 giây và khi nhận `TaskbarCreated`.
- Khôi phục: `ABM_SETSTATE` trạng thái gốc + `SW_SHOWNA`, xoá file. Được gọi khi thoát, khi lỗi không bắt được,
  `ProcessExit`, `SessionEnding`.
- Sau crash: lần chạy kế tiếp thấy file còn và tiến trình cũ không còn → khôi phục trước.
- Watchdog: `MacDock.exe --watchdog <pid>` (không nạp WPF, ~10 MB) chờ tiến trình chính thoát; nếu file trạng thái
  vẫn báo đang ẩn thì khôi phục. Đã kiểm thử bằng cách kill tiến trình chính.
- Lệnh tay: `MacDock.exe --restore-taskbar` (báo bản đang chạy thoát rồi khôi phục).
- Phương án dự phòng: tắt "Ẩn thanh tác vụ của Windows" trong Cài đặt → giữ taskbar gốc, Dock nằm phía trên nó.

## 5. Khay hệ thống

Khi taskbar gốc bị ẩn, icon khay (Unikey/EVKey, Zalo, OneDrive…) không còn chỗ hiển thị. MacDock giải quyết:

1. Trên một **luồng riêng** có vòng lặp thông điệp riêng, tạo cửa sổ ẩn lớp `Shell_TrayWnd` (kèm con
   `TrayNotifyWnd`), đặt `HWND_TOPMOST` và kiểm tra mỗi giây để `FindWindow("Shell_TrayWnd")` (mà
   `Shell_NotifyIcon` dùng) tìm thấy cửa sổ này trước Explorer.
2. Phát `TaskbarCreated` để mọi app đăng ký lại icon.
3. Nhận `WM_COPYDATA` (dwData = 1, chữ ký `0x34753423`), giải mã `NOTIFYICONDATA` 32-bit
   (NIM_ADD/MODIFY/DELETE/SETVERSION, cờ NIF_MESSAGE/ICON/TIP/STATE/GUID), sao chép HICON.
4. **Chuyển tiếp nguyên vẹn** mọi thông điệp (kể cả `SHAppBarMessage`, dwData = 0) cho taskbar của Explorer
   bằng `SendMessageTimeout`, nên Explorer luôn đồng bộ; khi MacDock thoát, phá cửa sổ và phát lại `TaskbarCreated`.
5. Trả lời `Shell_NotifyIconGetRect` (dwData = 3) bằng vị trí icon trên Thanh Menu.
6. Bấm icon: `AllowSetForegroundWindow(pid)` rồi `PostMessage(uCallbackMessage)` theo đúng phiên bản:
   v4 → `wParam = (x,y)`, `lParam = (msg, uID)` và thêm `NIN_SELECT` / `WM_CONTEXTMENU`; cũ → `wParam = uID`, `lParam = msg`.

Giới hạn: icon "hệ thống" do Explorer tự vẽ (âm lượng, mạng, pin) không đi qua `Shell_NotifyIcon`; MacDock
thay bằng chỉ báo riêng. Icon pin trùng của Explorer được ẩn mặc định (có thể bật trong Cài đặt → Thanh Menu).

## 6. Thanh Menu

- AppBar `ABE_TOP` trên mỗi màn hình, nền theo theme, kính Acrylic qua `SetWindowCompositionAttribute`.
- Menu app: `GetMenu` → đọc `GetMenuItemInfo` (gửi `WM_INITMENU`/`WM_INITMENUPOPUP` để app tự điền),
  dựng lại thành menu WPF, chọn mục → `WM_COMMAND`. Không có menu Win32 → UI Automation tìm `MenuBar`
  (chạy nền, tối đa 1,5 giây, cache 30 giây), chọn → `ExpandCollapse`/`Invoke` menu thật của app.
  Không có cả hai → menu chung (Tệp/Sửa/Xem/Cửa sổ/Trợ giúp gửi phím tắt chuẩn).
- Rê sang mục khác khi một menu đang mở sẽ chuyển menu (giống macOS).
- Bên phải: khay hệ thống, bộ gõ (`GetKeyboardLayout`, đổi bằng `WM_INPUTLANGCHANGEREQUEST`), pin
  (`GetSystemPowerStatus`), Wi-Fi (`WlanQueryInterface`), âm lượng (`IAudioEndpointVolume` + callback),
  Trung tâm điều khiển (`Windows.Devices.Radios`, WMI `WmiMonitorBrightness`, chế độ tối qua registry
  `Personalize` + `WM_SETTINGCHANGE "ImmersiveColorSet"`), thông báo (Win+N), đồng hồ (`DateTime.ToString`
  với văn hoá vi-VN, cập nhật đúng đầu phút/giây).

## 7. Theme

`theme.json` (xem `%APPDATA%\MacDock\theme-mau\*.json`):

```json
{
  "Id": "sonoma", "Name": "Sonoma",
  "Light": { "DockBackground": "#66F6F6F8", "DockBackground2": null, "DockBorder": "#66FFFFFF", "MenuBarBackground": "#B3F4F4F6", "...": "..." },
  "Dark":  { "...": "..." },
  "CornerRadius": 18, "PopupCornerRadius": 9, "BorderThickness": 1,
  "ShadowBlur": 22, "ShadowOpacity": 0.28,
  "MenuBarMaterial": "acrylic | blur | solid", "DockMaterial": "glass | solid",
  "FontFamily": "Segoe UI Variable Text, Segoe UI", "FontSize": 13, "IconStyle": "Original | Squircle"
}
```

- Màu dạng `#AARRGGBB`; có `...2` thì thành gradient.
- `.macdocktheme` = zip chứa `theme.json` và thư mục `icons/` (icon pack: `<tên exe>.png`, hoặc theo AUMID/họ gói).
- Chế độ: Sáng / Tối / Theo Windows (đọc `AppsUseLightTheme`, theo dõi `ImmersiveColorSet`) / Theo giờ.
- Tuỳ chỉnh trong Cài đặt (bo góc, độ đậm nền, viền, chất liệu Thanh Menu, phông) đè lên theme và có thể
  "Lưu thành theme mới".

## 8. Cấu hình

`%APPDATA%\MacDock\config.json` gồm các mục `General`, `Dock` (kèm danh sách `Pinned`), `MenuBar`, `Theme`,
`Performance`. Ghi nguyên tử (file tạm rồi đổi tên), gộp ghi sau 600 ms; file hỏng được đổi tên `.broken`
và dùng mặc định.

## 9. Hiệu năng

- GC workstation, không GC nền; sau khởi động và khi đóng Cài đặt thì thu gọn bộ nhớ (`EmptyWorkingSet`).
- Không vẽ khi rảnh; icon khay động chỉ cập nhật ảnh, không dựng lại giao diện.
- Đo trên máy thử (i7-9750H, 1920×1080 @125%, bản Release single-file): xem README.

## 10. Tham số dòng lệnh

| Tham số | Ý nghĩa |
|---|---|
| `--restore-taskbar` | Khôi phục taskbar gốc (và yêu cầu bản đang chạy thoát) |
| `--settings` | Mở Cài đặt khi khởi động |
| `--config-dir <thư mục>` | Dùng thư mục dữ liệu khác (kiểm thử / chạy di động) |
| `--autostart` | Do mục tự khởi động thêm vào |
| `--verbose` | Ghi log chi tiết |
| `--no-autostart` | Không ghi khoá Run (kiểm thử) |
| `--test` | Bật lệnh kiểm thử nội bộ qua thông điệp `MacDock.Test` (dành cho nhà phát triển) |
