# Frontend dashboard

Dashboard M5 là HTML/CSS/JavaScript thuần, được phục vụ bằng Nginx.

Backend phải chạy tại `http://127.0.0.1:25000` trước khi khởi động frontend.

```powershell
cd frontend
docker compose up --build -d
```

Mở `http://127.0.0.1:25100`.

Nginx chuyển tiếp các request `/api/*` đến backend nên trình duyệt không cần cấu hình CORS.
