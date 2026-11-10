# Nhật ký thay đổi / Changelog

## [0.4.2] - 2026-11-10

### English
- **Fixed:** the Bluetooth panel in Control Center flickered nonstop. Showing the current Bluetooth state was treated as a click on the switch, so the panel kept rebuilding itself.

### Tiếng Việt
- **Sửa:** bảng Bluetooth trong Trung tâm điều khiển nháy liên tục. Việc hiển thị trạng thái Bluetooth hiện tại bị coi như một lần bấm công tắc nên bảng cứ tự dựng lại.

## [0.4.1] - 2026-11-10

### English
- **Fixed:** with auto-hide on, the Dock no longer hides while the pointer is on a window preview, or moving from the icon to the preview. The icon stays magnified, and the Dock hides only after the pointer leaves both the Dock and the preview.
- **Smoother Dock:** magnification now runs at the display's refresh rate (measured about 57-60 fps on a 60 Hz screen, previously about 27-30 fps with frequent stutters). The Dock window is composited directly by DWM instead of being a layered window, and the shadow is drawn directly instead of with a blur effect. The on-battery limit is now 60 FPS instead of 30.

### Tiếng Việt
- **Sửa:** khi bật tự ẩn, Dock không còn ẩn khi chuột đang ở ô xem trước cửa sổ hoặc đang di từ icon lên ô đó. Icon vẫn phóng to, và Dock chỉ ẩn khi chuột rời cả Dock lẫn ô xem trước.
- **Dock mượt hơn:** hiệu ứng phóng to chạy theo tần số màn hình (đo được khoảng 57-60 khung/giây trên màn hình 60 Hz, trước đây khoảng 27-30 khung/giây và hay bị giật). Cửa sổ Dock nay do DWM ghép trực tiếp thay vì là cửa sổ layered, và bóng được vẽ trực tiếp thay vì dùng hiệu ứng làm mờ. Giới hạn khi dùng pin nay là 60 FPS thay vì 30.

## [0.4.0] - 2026-11-10

### English
- **New Control Center** in a liquid-glass style: large rounded modules, circle and pill buttons, thick sliders.
  - Wi-Fi, Bluetooth and Hotspot open their own detail views: network list with connect/disconnect, paired Bluetooth devices, and a link to Settings.
  - Now Playing with artwork and playback controls (Spotify, browsers, Media Player…).
  - Display brightness, and Sound with output/input device switching.
  - Do Not Disturb, Dark Mode, Screenshot, Airplane Mode, Microphone, Night Light, Energy Mode, Screen Mirroring, Calculator, Accessibility and Lock Screen.
  - **Edit Controls…**: add, remove and reorder modules, and switch buttons between small and medium; the layout is saved.
- Clicking the Wi-Fi or volume icon in the menu bar opens the same glass panels; the volume panel also lets you pick the output device.
- **Fixed:** menu bar panels and menus now close when you click anywhere outside them, including other apps and the desktop. Esc also closes them, and clicking the same icon again toggles the panel.

### Tiếng Việt
- **Trung tâm điều khiển mới** kiểu kính lỏng: khối bo tròn lớn, nút tròn và nút viên thuốc, thanh trượt dày.
  - Wi-Fi, Bluetooth và Phát sóng mở trang chi tiết riêng: danh sách mạng để kết nối/ngắt, danh sách thiết bị Bluetooth đã ghép đôi, và lối vào Cài đặt.
  - Đang phát kèm ảnh bìa và nút điều khiển (Spotify, trình duyệt, Media Player…).
  - Độ sáng màn hình, và Âm thanh có chọn thiết bị phát/thu.
  - Không làm phiền, Chế độ tối, Chụp màn hình, Chế độ máy bay, Micro, Ánh sáng ban đêm, Chế độ năng lượng, Chiếu màn hình, Máy tính, Trợ năng và Khoá màn hình.
  - **Sửa điều khiển…**: thêm, bớt và sắp xếp module, và đổi cỡ nút nhỏ/vừa; bố cục được lưu lại.
- Bấm icon Wi-Fi hoặc âm lượng trên Thanh Menu sẽ mở bảng kính tương tự; bảng âm lượng chọn được thiết bị phát.
- **Sửa:** bảng và menu trên Thanh Menu tự đóng khi bấm ra bất kỳ đâu bên ngoài, kể cả app khác và desktop. Nhấn Esc cũng đóng được, và bấm lại cùng icon sẽ bật/tắt bảng.
## [0.3.1] - 2026-11-10

### English
- **Fixed:** after signing in, maximized windows could cover the menu bar and the Dock (their reserved space was lost). MacDock's own AppBar requests now go straight to Explorer, it waits for Explorer at sign-in, and it repairs the reserved space and z-order automatically.
- **Fixed:** the Shut Down / Restart / Log Out confirmation could open behind other windows, so the buttons seemed to do nothing. It now appears on top.
- **Fixed:** Win+Space / Shift+Win+Space switch the input language even while the Windows taskbar is hidden (with a small language overlay).
- **Fixed:** the Dock appears as soon as the pointer touches the screen edge anywhere, not only near the Dock.
- **Faster sign-in:** starts via Task Scheduler (no Windows startup delay), waits until Explorer is ready, shows the Dock and menu bar first and loads the rest in the background; icons are cached on disk.
- The original taskbar auto-hide setting is detected correctly, so it is restored exactly as it was.
- Bilingual README and installation & usage guide (GUIDE.md).

### Tiếng Việt
- **Sửa:** sau khi đăng nhập, cửa sổ phóng to có thể che mất Thanh Menu và Dock (mất vùng chừa chỗ). Yêu cầu AppBar của MacDock nay gửi thẳng tới Explorer, MacDock đợi Explorer sẵn sàng khi đăng nhập và tự sửa vùng chừa chỗ, thứ tự cửa sổ.
- **Sửa:** hộp xác nhận Tắt máy / Khởi động lại / Đăng xuất có thể mở phía sau các cửa sổ khác nên bấm như không có tác dụng. Nay hộp luôn hiện trên cùng.
- **Sửa:** Win+Space / Shift+Win+Space đổi bộ gõ được cả khi taskbar Windows đang ẩn (kèm bảng nhỏ báo ngôn ngữ).
- **Sửa:** Dock hiện ngay khi chuột chạm mép màn hình ở bất kỳ đâu, không chỉ gần Dock.
- **Đăng nhập nhanh hơn:** khởi động qua Task Scheduler (không bị Windows trì hoãn), đợi Explorer sẵn sàng, hiện Dock và Thanh Menu trước rồi nạp phần còn lại; cache icon trên đĩa.
- Đọc đúng chế độ tự ẩn gốc của taskbar để khôi phục y như cũ.
- README và hướng dẫn cài đặt & sử dụng (GUIDE.md) song ngữ.
## [0.3.0] - 2026-11-10

### Thêm
- **Xem trước cửa sổ** khi rê chuột lên app đang chạy trên Dock: ảnh thu nhỏ trực tiếp (DWM), bấm để chuyển, nút × để đóng; chỉnh bật/tắt, độ trễ, kích thước.
- **Wi-Fi ngay trong Thanh Menu**: bật/tắt, danh sách mạng (đã biết / khác), bấm để kết nối hoặc ngắt, hộp nhập mật khẩu riêng.
- **Bảng âm lượng/độ sáng kiểu macOS 26** khi bấm phím trên bàn phím (góc trên bên phải hoặc giữa phía dưới); chọn giữa giao diện MacDock và mặc định của Windows.
- **Giao diện Thanh Menu**: trong suốt kiểu macOS 26 (màu chữ tự chọn theo hình nền), kính mờ, đồng bộ với Dock, màu đặc; chỉnh độ đậm và màu chữ.
- Logo góc trái hình tròn, đổi được (hình tròn / bông hoa / ảnh tự chọn cắt tròn).
- Icon Launchpad chọn được: lưới màu, logo Windows 11 (vẽ vector) hoặc ảnh tự chọn.
- **Thêm ứng dụng vào Dock**: cửa sổ chọn mọi app đã cài (kể cả app Store) có tìm kiếm; danh sách ghim trong Cài đặt với nút lên/xuống/gỡ; mục "Thêm ứng dụng vào Dock…" trong menu chuột phải của Dock.
- "Giới thiệu máy này" và màn hình chào làm lại theo phong cách macOS 26 (cửa sổ kính bo góc).
- Trang Giới thiệu có thông tin nhà phát triển, mã QR ủng hộ (MB Bank) và liên kết GitHub, website.
- **Thanh Menu kính liquid**: làm mờ thật dải hình nền phía sau, phủ màu theo Dock, viền và bóng sáng; chỉnh được độ liquid.
- **Ngôn ngữ**: Tiếng Việt / English cho toàn bộ app (Cài đặt → Chung).

### Sửa
- Tự ẩn Dock thông minh: chỉ ẩn khi có cửa sổ che vùng Dock; ở màn hình chính Dock luôn hiện (có thể tắt).
- Dock hiện/ẩn mượt hơn: dịch cửa sổ thay vì vẽ lại cả Dock mỗi khung hình, độ trễ hiện mặc định 80 ms.
- Taskbar gốc không còn thỉnh thoảng hiện lên: theo dõi sự kiện hiện cửa sổ của Explorer và ẩn lại ngay.
- Popup trên Thanh Menu không còn nhảy sang trái khi đang thao tác.
- Không còn icon âm lượng/pin trùng trong khay hệ thống.
- Giảm vẽ lại khi rảnh (tiêu đề nhấp nháy, AppBar báo đổi vị trí qua lại).
- Thoát MacDock không bị treo khi Explorer phản hồi chậm.
- Dock hiện ngay khi chuột chạm cạnh màn hình (độ trễ mặc định 0, hiệu ứng nhanh hơn).
- Popup trên Thanh Menu tự đóng khi bấm ra ngoài.
- Sửa lỗi Thanh Menu/Dock bị cửa sổ phóng to che mất: yêu cầu chừa chỗ (AppBar) của MacDock nay gửi thẳng tới Explorer; chờ Explorer sẵn sàng khi đăng nhập; tự đăng ký lại nếu vùng chừa chỗ bị mất.
- Nút mũi tên và các nút trong màn hình chào bấm được (trước đó bị thao tác kéo cửa sổ nuốt mất).

## [0.2.0] - 2026-11-09

Viết lại toàn bộ, chạy thử thật trên Windows 11.

### Thêm
- **Dock**: phóng to theo đường cong cos, làm mượt theo thời gian, nảy khi mở app / khi app cần chú ý,
  chấm app đang chạy, nhãn tên, huy hiệu (cửa sổ nhấp nháy hoặc số trong tiêu đề như "(3) Messenger"),
  kéo thả sắp xếp, kéo ra ngoài để gỡ, kéo exe/lnk/thư mục vào để ghim, kéo file lên app để mở,
  kéo file vào Thùng rác để xoá (có thể hoàn tác). Vị trí dưới/trái/phải, tự ẩn với độ trễ, chừa chỗ (AppBar),
  nhiều màn hình, ẩn khi có app toàn màn hình.
- **Thanh Menu** ở cạnh trên mọi màn hình: menu hệ thống (logo riêng), tên app in đậm, menu thật của app
  (Win32 `GetMenu` hoặc UI Automation), menu chung khi app không có; đồng hồ tuỳ định dạng + lịch nhỏ,
  pin, Wi-Fi, âm lượng (thanh trượt, lăn chuột), bộ gõ/ngôn ngữ, Trung tâm điều khiển (Wi-Fi, Bluetooth,
  độ sáng, âm lượng, chế độ tối), thông báo (Win+N).
- **Khay hệ thống** trên Thanh Menu khi ẩn taskbar: MacDock tự làm chủ `Shell_TrayWnd`, nhận icon của
  mọi app (Unikey/EVKey, Zalo, OneDrive…), chuyển tiếp nguyên vẹn cho Explorer; bấm trái/phải/đúp hoạt động.
- **Cài đặt** kiểu System Settings bằng tiếng Việt, xem trước trực tiếp, khôi phục mặc định theo trang,
  trang Hiệu năng (giới hạn FPS, tắt hiệu ứng, giảm khi dùng pin, đo RAM/CPU).
- **Theme engine**: `theme.json`, 6 theme dựng sẵn, sáng/tối/theo Windows/theo giờ, kính Acrylic cho
  Thanh Menu, tuỳ chỉnh bo góc/độ đậm/viền/phông, icon squircle hoặc gốc, đổi icon từng app, icon pack,
  nhập/xuất `.macdocktheme`, đổi nhanh từ menu Dock.
- Tự khởi động cùng Windows (HKCU Run), màn hình chào lần đầu, một tiến trình duy nhất (mở lại → mở Cài đặt).
- An toàn taskbar: lưu trạng thái gốc ra file, khôi phục khi thoát/lỗi/đăng xuất, khôi phục sau crash ở lần chạy
  sau, tiến trình watchdog, lệnh `MacDock.exe --restore-taskbar`.

### Đã bỏ
- Nút cửa sổ đỏ/vàng/xanh (vì lo ổn định).

## [0.1.0]
- Bản thử nghiệm đầu tiên (viết trên Linux, chưa chạy thử trên Windows).
