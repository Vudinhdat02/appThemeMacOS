# Nhật ký thay đổi

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
