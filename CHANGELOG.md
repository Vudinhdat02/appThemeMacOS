# Nhật ký thay đổi

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
