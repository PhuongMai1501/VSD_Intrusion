# Live Camera Processing System

Hệ thống xử lý 6 camera RTSP đồng thời với kiến trúc đa tiến trình nhằm đảm bảo hiệu năng và độ ổn định cao cho giám sát thời gian thực.

## Cấu trúc dự án

### CameraWorker
- Tiến trình chuyên dụng để giải mã từng luồng camera RTSP
- Sử dụng FFmpeg.AutoGen với hỗ trợ tăng tốc phần cứng (CUDA / Intel QSV / DirectX)
- Ghi dữ liệu frame vào Memory-Mapped File để chia sẻ với tiến trình chính

### CameraManager
- Giao diện chính giám sát tối đa 6 camera trong layout 2x3
- Điều khiển và giám sát trạng thái từng tiến trình CameraWorker qua LittleForker
- Hiển thị video, lớp phủ (overlay) và cảnh báo theo thời gian thực
- Tự động khởi động lại worker khi phát hiện sự cố hoặc mất tín hiệu

## Yêu cầu hệ thống

- .NET 8.0
- Windows 10/11 (x64)
- Thư viện FFmpeg (được bao gồm trong dự án)
- GPU hỗ trợ CUDA, Intel QSV hoặc DirectX (tùy chọn, để tăng tốc giải mã)

## Hướng dẫn sử dụng

1. **Cấu hình RTSP**: Cập nhật danh sách camera trong cơ sở dữ liệu hoặc file cấu hình theo môi trường triển khai.
2. **Thiết lập MessageSecrets**: Tạo/điền giá trị thật cho `Config Setting/MessageSecrets.ini` nằm cạnh file chạy (`bin/<Configuration>/...`). Các khóa Telegram/Discord phải được cung cấp tại đây.
3. **Build dự án**: Mở solution `LiveCameraProcessing.sln`, chọn cấu hình **x64** rồi build.
4. **Chạy ứng dụng**: Khởi chạy `CameraManager.exe` để bắt đầu giám sát.

## Tính năng chính

- **Kiến trúc đa tiến trình**: Mỗi camera có một worker riêng, hạn chế ảnh hưởng lẫn nhau.
- **Tăng tốc phần cứng**: Hỗ trợ giải mã GPU khi khả dụng, fallback sang phần mềm nếu cần.
- **Tự phục hồi**: Worker tự khởi động lại khi mất kết nối hoặc treo.
- **Hiệu năng cao**: Chia sẻ frame qua Memory-Mapped File, giảm chi phí sao chép.
- **Thông báo sự kiện**: Hỗ trợ gửi Telegram/Discord theo cấu hình bảo mật bên ngoài source.

## Thông số kỹ thuật tham khảo

- Kích thước frame mặc định: 1920x1080 (BGR24)
- Tốc độ hiển thị: ~30 FPS (tùy cấu hình phần cứng)
- Kích thước Memory-Mapped File: ~6MB cho mỗi camera
- Cơ chế giám sát No-Signal và khởi động lại sau 7 giây không có khung hình

## Troubleshooting

1. **Thiếu FFmpeg DLL**: Đảm bảo build ở chế độ x64 và thư mục `FFmpeg/bin/x64` nằm cạnh file chạy.
2. **Không kết nối được camera**: Kiểm tra URL RTSP, firewall và thông tin xác thực.
3. **Hiệu năng thấp**: Cập nhật driver GPU, bật tăng tốc phần cứng hoặc giảm số camera.
4. **Telegram/Discord không gửi**: Kiểm tra lại `MessageSecrets.ini`, đảm bảo file đúng thư mục runtime và khóa hợp lệ.

## Giấy phép

Dự án sử dụng nội bộ. Khi phân phối cần đảm bảo tuân thủ giấy phép của các thư viện phụ thuộc (FFmpeg, Discord.Net, MySql.Data, v.v.).
