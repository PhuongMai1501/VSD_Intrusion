# Dòng Chảy Hoạt Động Hệ Thống Live Camera Processing

Tài liệu này mô tả chi tiết cách cấu hình và vận hành dự án giám sát `LiveCameraProcessing`, đồng thời giải thích vai trò các mô-đun và hàm quan trọng trong mã nguồn.

## 1. Tổng Quan
- Giải pháp giám sát tối đa 6 camera RTSP trong thời gian thực, phát hiện hành vi xâm nhập và phát cảnh báo.
- Kiến trúc đa tiến trình: tiến trình giao diện (`CameraManager`) quản lý các tiến trình giải mã độc lập (`CameraWorker`).
- Giao tiếp giữa các tiến trình thông qua `MemoryMappedFile` và `Mutex` để chia sẻ frame ở định dạng BGR24.
- Hệ thống tương tác với MySQL để lấy cấu hình camera, lưu log sự kiện và danh sách người nhận cảnh báo.

## 2. Cấu Trúc Và Thành Phần Chính
| Thành phần | Tệp/Tập tin | Vai trò |
| --- | --- | --- |
| **Giải pháp Visual Studio** | `LiveCameraProcessing.sln` | Gom 2 project WinForms (.NET 8 x64). |
| **CameraManager** | `CameraManager/` | Giao diện vận hành, hiển thị luồng hình, overlay, cảnh báo. |
| **CameraWorker** | `CameraWorker/` | Tiến trình FFmpeg giải mã RTSP và ghi frame vào MMF. |
| **Cấu hình chung** | `CameraManager/Class/*`, `CameraManager/Config/*` | Singleton `ClassSystemConfig`, hàm tiện ích DB/ghi log, đọc file cấu hình. |
| **Tài nguyên cấu hình runtime** | `Config Setting/` | Các file `.ini` chứa tham số gửi cảnh báo, secrets bot. |
| **Công cụ** | `tools/analyze_logs.py` | Script phân tích log track/detection phục vụ debug. |

### 2.1. CameraManager (WinForms chính)
- `Program.cs`: ép ứng dụng bật console để dễ quan sát log, sau đó chạy `Form1` với xử lý lỗi bao quanh.
- `Form1.cs`: trái tim hệ thống, đảm nhận các nhóm chức năng:
  - Khởi tạo UI, bố trí layout theo số camera (`LayoutCameraSpreadView`).
  - Liên kết tới DB để tải danh sách RTSP (`LoadCameraList` → `ClassFunction.GetRtspUrls`).
  - Khởi tạo và giám sát `CameraWorker` thông qua `LittleForker.ProcessSupervisor` (`StartCameraSystem`, `RestartWorker`).
  - Đọc frame từ `MemoryMappedFile`, cập nhật `PictureBox`, lưu frame gần nhất để chạy AI (`UpdatePictureBox`).
  - Lập lịch phát hiện xâm nhập bằng timer (`DetectionTimer_Tick` → `ProcessDetectionAsync` → `DetectIntrusionAsync`).
  - Vẽ overlay phát hiện/region, hiển thị trạng thái No-Signal, bật full-screen, lưu ảnh chụp (`OnPictureBoxPaint`, `DrawRegionOverlayIfAvailable`).
  - Gửi cảnh báo Telegram, bật popup xác nhận, ghi log DB (`SendAlarmToActiveRecipientsAsync`, `ActivateCameraAlert`, `AddCameraLogData`).
- Các Form phụ trợ: `FormCameraList` (quản lý bảng `camera_list`), `FormConfigMessage` (cấu hình gửi tin nhắn & bypass), `FormLogView` (xem log), `MeasureRecipe2` (tham số quy trình), `FormConfirmVision` (popup xác nhận sự kiện).

### 2.2. CameraWorker (tiến trình con)
- `Program.cs`: nhận đối số `rtsp_url`, `mmf_name`, `mutex_name`, `camera_name`, `stt`, `connectionString`.
  - Đăng ký thư viện FFmpeg (`FFmpegBinariesHelper.RegisterFFmpegBinaries`).
  - Vòng lặp tự phục hồi: kết nối RTSP, giải mã bằng FFmpeg.AutoGen, chuyển YUV → BGR24.
  - Ghi chiều rộng/cao vào header 8 byte đầu MMF, frame đặt sau header; đồng bộ bằng `Mutex`.
  - Kiểm tra tín hiệu dừng để shutdown an toàn, ghi log chi tiết vào file riêng (`CameraWorkerLogger`).
  - Nếu cung cấp connection string, định kỳ đọc lại URL từ `camera_list` theo `STT` để tự cập nhật khi thay đổi (`ResolveRtspUrl`).

## 3. Quy Trình Khởi Động & Vận Hành
1. Người dùng chạy `CameraManager.exe` → `Program.Main` mở console, khởi động `Form1`.
2. Trong constructor `Form1`:
   - Bật `KeyPreview`, hiển thị form loading, đăng ký các hook shutdown.
   - Khởi tạo hệ thống phát hiện (`InitializeDetectionSystem`), timer cảnh báo (`_alertTimer`).
3. Sự kiện `Form1_Load`:
   - `InitializeUI` đọc `DeviceConfig.ini` (nếu có) và dựng các form cấu hình.
   - `LoadCameraList` đọc bảng `camera_list`; nếu không có dữ liệu, nạp sẵn danh sách RTSP mặc định.
   - Tính toán layout 2×3 hoặc phù hợp số camera thực (`UpdateLayoutForCameraCount`, `LayoutCameraSpreadView`).
4. Người dùng nhấn *Start* (`btnStartCamera_Click`) → `StartCameraSystem`:
   - Dọn cache region, kiểm tra `CameraWorker.exe`.
   - Với từng camera: tạo MMF `Cam_{i}_MMF`, mutex `Global\Cam_{i}_Mutex`, spawn worker với tham số tương ứng.
   - Kích hoạt `DisplayTimer` để bắt đầu nhận và vẽ frame.
5. `CameraWorker` giải mã và ghi frame; `Form1.UpdatePictureBox` đọc frame, clone lưu vào `_latestFrames`, cập nhật thời gian nhận.
6. Timer `DetectionTimer_Tick` duyệt từng camera, clone bitmap vuông, gọi `DetectIntrusionAsync` qua `ActionRecognitionClient` tới API `/detect`.
7. Kết quả trả về được chuẩn hoá, làm mượt track (`UpdateAndBuildTracks`), lưu vào `_cameraDetections`. `OnPictureBoxPaint` vẽ bbox, region, border cảnh báo.
8. Khi phát hiện hợp lệ (điểm trung tâm nằm trong polygon region):
   - Gửi cảnh báo Telegram (`SendAlarmToActiveRecipientsAsync`), bật nhấp nháy viền, hiện form xác nhận.
   - Ghi log DB (`camera_log`) và cập nhật bảng điều khiển.
9. Hệ thống theo dõi mất tín hiệu (`UpdateNoSignalOverlay`), tự restart worker nếu quá 7 giây không có frame (`TryRestartStalledCamera`, `RestartWorker`).
10. Khi thoát ứng dụng, `Form1_FormClosing` đảm bảo tắt timer, huỷ supervisor, MMF, mutex và dispose tài nguyên.

## 4. Cấu Hình Hệ Thống
### 4.1. Thư viện & môi trường
- .NET 8.0, build x64. Cần FFmpeg đầy đủ trong `FFmpeg/bin/x64` (đi kèm repo) hoặc khai báo đường dẫn hệ thống.
- Hệ điều hành mục tiêu: Windows 10/11 x64.

### 4.2. Kết nối cơ sở dữ liệu MySQL
- Chuỗi kết nối mặc định: `Server=localhost;Database=action_recog;Uid=root;Pwd=123456;` (có thể chỉnh trong `ClassCommon.connectionString`).
- Các bảng sử dụng:
  - `camera_list`: chứa `STT`, `Name`, `RTSP_URL`, thông tin đăng nhập, toạ độ polygon (`x1..y4`).
  - `camera_log`: ghi lịch sử cảnh báo (`Camera`, `Time`, `Event`, `image_Path`).
  - `alarm_mes`: danh sách người nhận Telegram (`Name`, `SDT`, `ChatID`, `IsActive`).

### 4.3. File cấu hình `.ini`
| File | Vị trí | Nội dung |
| --- | --- | --- |
| `Config Setting/MessageSecrets.ini` | Tự tạo khi chạy | Token Telegram/Discord. Điền thật để bật gửi tin. |
| `Config Setting/SendMessageConfig.ini` | Lưu qua `FormConfigMessage` | `SEND MESSAGE MODE`, `BYPASS ALERT` (0 gửi, 1 bỏ qua). |
| `Config Setting/DeviceConfig.ini` | Lưu layout thiết bị | Tham số giao diện, kích thước hiển thị, v.v. |

Ngoài ra `FormParamCamera`, `MeasureRecipe2` có thể ghi thêm file cấu hình chuyên biệt tuỳ quy trình.

### 4.4. API phát hiện xâm nhập
- Biến `INTRUSION_API_BASE_URL` trong `Form1` (mặc định `http://192.168.210.250:5001`).
- `ActionRecognitionClient` kết nối `/detect`, gửi JSON `{ frame: <base64>, stream_id }`, timeout 3.5s với retry nhẹ.
- Khi API trả kết quả yếu lặp lại nhiều lần, hệ thống tự gọi `/reset` để clear buffer (`DetectIntrusionAsync`).

## 5. Luồng Dữ Liệu & Xử Lý Phát Hiện
1. **CameraWorker** đọc RTSP, chuyển đổi và ghi vào MMF (`ProcessRTSPStream`).
2. **CameraManager** đọc frame, lưu `_latestFrames`, cập nhật `_frameSeqByCam`.
3. **DetectionTimer** gửi frame nén JPEG 75% tới API.
4. **NormalizeDetectionsToOriginalFrame** chuyển bbox từ ảnh vuông về toạ độ gốc.
5. **UpdateAndBuildTracks** làm mượt track, thêm “hangover frames” để giảm nhấp nháy.
6. **DrawDetectionsWithRegion** vẽ bbox màu theo nhãn, hiển thị thông tin confidence.
7. **Region overlay** được tải từ DB chỉ một lần mỗi camera (`EnsureRegionLoaded`) và vẽ mỗi frame.

Các hàm hỗ trợ quan trọng:
- `ResizeToSquare`, `EncodeJpeg`: chuẩn bị ảnh đầu vào API.
- `IsPointInPolygonF`: kiểm tra điểm giữa bbox có nằm trong vùng cấu hình.
- `SendAlarmToActiveRecipientsAsync`: throttle 10s, ghi log từng ChatID.
- `AddCameraLogData`: chèn record mới vào `camera_log`.

## 6. Cảnh Báo & Quản Trị
- **Gửi tin Telegram**: cần điền `BotToken` và `ChatID` hợp lệ. Có thể test trực tiếp từ `FormConfigMessage` (`btnTest_Click`).
- **Bypass cảnh báo**: bật checkbox trong `FormConfigMessage` để tạm ngưng gửi (ghi `BYPASS ALERT = 1`).
- **Popup xác nhận**: `FormConfirmVision` hiển thị nhắc nhở, gọi `OnConfirm` khi người vận hành chấp thuận.
- **Log hiển thị**: `FormLogView` đọc dữ liệu DB hoặc log file (`Logs/CameraManager_*.log`).
- **Restart tự động**: `TryRestartStalledCamera` kiểm tra mỗi camera, nếu >7s không có frame và cooldown >10s thì gọi `RestartWorker`.

## 7. Công Cụ Và Logging
- Log tiến trình chính: `Logs/CameraManager_*.log` (tự tạo khi chạy, thông qua `FileLogger`).
- Log worker: `Logs/CameraWorker_<PID>_<timestamp>.log` (ghi trong thư mục làm việc của worker).
- Script phân tích log overlay: `tools/analyze_logs.py` giúp kiểm tra track `API`, `DRAW`, `HANGOVER` để chẩn đoán ghost bbox.

## 8. Build & Triển Khai
1. Cài đặt .NET 8 SDK và Visual Studio 2022 (Desktop development with C#).
2. Mở `LiveCameraProcessing.sln`, chọn cấu hình `Release | x64`.
3. Đảm bảo thư mục `FFmpeg/bin/x64` nằm cạnh `CameraManager.exe` khi publish.
4. Kiểm tra MySQL: tạo database `action_recog`, các bảng cần thiết, cấp quyền tài khoản trong connection string.
5. Điền `Config Setting/MessageSecrets.ini` bằng token thật, cập nhật `SendMessageConfig.ini` nếu cần.
6. Chạy `CameraManager.exe`, cấu hình camera qua `FormCameraList`, nhấn *Start* để kích hoạt giám sát.

---
Tài liệu này được xây dựng dựa trên mã nguồn hiện tại tại thư mục gốc dự án. Khi thay đổi logic, vui lòng cập nhật lại các bước tương ứng để luôn đồng bộ với triển khai thực tế.
