# Facebook Webhook Platform

Một hệ thống tự động hóa phản hồi bình luận/tin nhắn Facebook chuyên nghiệp, sử dụng Kafka kết hợp cùng AI (Gemini).

Hệ thống cho phép:
- Nhận diện intent (ý định) của người dùng: *Hỏi giá, Khiếu nại, Khen ngợi,...* để tự động đưa ra các câu trả lời thông minh.
- Nhận diện Spam độc hại (chứa link) và Spam lặp từ: Tự động ẩn bình luận vi phạm và đẩy vào hàng chờ cho admin kiểm duyệt thủ công.
- Theo dõi trạng thái từng event bằng In-Memory State Tracker.
- Cơ chế tự động Retry mạnh mẽ khi AI hoặc Facebook API bị sập.

---

## 🏛️ Kiến trúc hệ thống

Hệ thống được chia thành 3 dự án độc lập kết nối với nhau thông qua **Apache Kafka**:

1. **`FB.Webhook.API`**: Cổng giao tiếp trực tiếp với Facebook. Tiếp nhận dữ liệu webhook, xác thực chữ ký bảo mật (HMAC-SHA256) và ném dữ liệu thô vào Kafka (Topic: `raw_events`).
2. **`FB.Webhook.CoreService`**: Trái tim của hệ thống (Worker). Chạy dưới nền, liên tục kéo dữ liệu từ `raw_events` về. Nó sẽ qua bộ lọc Spam, sau đó gọi AI Gemini phân tích ý định, rồi gọi lệnh xuống Facebook (Reply, Hide, Like).
3. **`FB.Webhook.RetryService`**: (Worker bảo hiểm). Chuyên lắng nghe topic `send_failed`. Nếu CoreService gọi AI bị lỗi mạng hoặc lỗi Token, event sẽ rơi vào đây. RetryService sẽ thử gọi lại liên tục sau mỗi 30s.

### Các Topic trên Kafka
- **`raw_events`**: Chứa toàn bộ webhook hợp lệ từ Facebook.
- **`manual_review`**: Chứa các bình luận chứa link lừa đảo / spam. Bình luận đã bị ẩn, chờ Admin vào xem xét.
- **`send_failed`**: Chứa các bình luận xử lý thất bại cần thử lại.

---

## 🚀 Hướng dẫn cài đặt

### Yêu cầu tiên quyết
- .NET 8 SDK
- Docker & Docker Compose (để chạy Kafka và Zookeeper)
- Ngrok (để đưa localhost ra Internet)
- Một Facebook App & Page

### Bước 1: Khởi động Kafka
Mở terminal ở thư mục gốc và chạy:
```bash
docker-compose up -d
```
Xem luồng dữ liệu Kafka trực quan bằng cách truy cập: `http://localhost:8080` (Kafka UI).

### Bước 2: Thiết lập môi trường (.env)
1. Copy file `.env.example` thành `.env` nằm ở thư mục gốc.
2. Mở file `.env` và điền đầy đủ các thông tin:
```env
Facebook__AppId=...
Facebook__AppSecret=...
Facebook__VerifyToken=FB_WEBHOOK_VERIFY_2026
Facebook__PageAccessToken=... (phải có quyền pages_manage_engagement)

Kafka__BootstrapServers=localhost:9092

Gemini__ApiKey=...
Gemini__Model=gemini-2.5-flash-lite
```

### Bước 3: Đưa hệ thống lên Internet
Mở một terminal mới, chạy lệnh Ngrok trỏ về IPv4 `127.0.0.1` của .NET (nếu ở Windows):
```bash
ngrok http http://127.0.0.1:5000
```
Copy địa chỉ `https://<id>.ngrok-free.app` để làm Webhook URL cho Facebook.

### Bước 4: Chạy các dự án
Mở 3 terminal riêng biệt để chạy 3 service:
```bash
# Terminal 1
cd FB.Webhook.API
dotnet run

# Terminal 2
cd FB.Webhook.CoreService
dotnet run

# Terminal 3
cd FB.Webhook.RetryService
dotnet run
```

---

## 🧪 Hướng dẫn Test (Kịch bản AI & Spam)

Để test hệ thống chuẩn nhất, hãy **dùng một tài khoản Facebook khác** (không phải tài khoản Admin) để vào Page comment:

1. **Test Hỏi Giá**: 
   - Comment: *"Shop ơi cho mình xin giá nhé"*
   - AI nhận diện `AskPrice` -> Tự động Reply thông tin giá.
2. **Test Khiếu nại**: 
   - Comment: *"Sản phẩm lỗi rồi shop ơi, tệ quá"*
   - AI nhận diện `Complaint` -> Tự động Reply xin lỗi và xin check inbox.
3. **Test Khen ngợi**: 
   - Comment: *"Chất lượng sản phẩm rất tuyệt vời"*
   - AI nhận diện `Positive` -> Hệ thống sẽ tự động **Like** bình luận đó.
4. **Test Link Spam**: 
   - Comment: *"Click link nhận quà http://scam.vn"*
   - Bộ lọc Spam bắt được -> Tự động Ẩn comment -> Quăng vào hàng chờ `manual_review` ở Kafka UI.
5. **Test Spam Lặp từ**: 
   - Comment chữ *"a"* liên tiếp 3 lần trong vòng 10 phút. 
   - Ở lần thứ 3 -> Bị quy tội Spam -> Tự động Ẩn comment.

> Cửa sổ `CoreService` có tính năng **State Tracker** in ra chi tiết đường đi của dữ liệu: `[STATE TRACKER] Event ... -> Received -> Processing -> Replied`.
