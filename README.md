# 🥋 ZENITH - Third-Person Martial Arts Action Game

Một dự án game hành động võ thuật góc nhìn thứ ba (Third-Person Action) lấy cảm hứng từ phong cách chiến đấu thực chiến, dứt khoát của **SIFU** và **Tekken**. Dự án được xây dựng trên nền tảng **Unity 6 (Universal Render Pipeline - URP)**.

---

## 📌 1. Yêu Cầu Hệ Thống (Requirements)

| Thành phần | Yêu cầu khuyến nghị |
| :--- | :--- |
| **Unity Engine** | **Unity 6000.6.7f1** (Unity 6 trở lên) |
| **Render Pipeline** | Universal Render Pipeline (URP 17.x+) |
| **Input System** | Unity New Input System package (`com.unity.inputsystem`) |
| **Hệ điều hành** | Windows 10/11 (64-bit) / macOS / Linux |
| **Phần cứng** | Card đồ họa hỗ trợ DirectX 11/12 hoặc Vulkan, tối thiểu 8GB RAM |

---

## 🚀 2. Hướng Dẫn Cài Đặt & Chạy Dự Án (Getting Started)

### Bước 1: Clone Repository
```bash
git clone https://github.com/ChuongNguyenNHC/Zenith.git
cd Zenith
```

### Bước 2: Mở Dự Án Bằng Unity Hub
1. Mở **Unity Hub**.
2. Chọn **Add** ➔ **Add project from disk**.
3. Chọn thư mục `Zenith` vừa clone.
4. Chọn đúng phiên bản Editor: **Unity 6000.6.7f1** (nếu Hub chưa có phiên bản này, bạn có thể tải qua thẻ *Installs* trong Unity Hub).
5. Nhấp mở dự án và chờ Unity import assets lần đầu (khoảng 1 - 3 phút).

### Bước 3: Mở Scene Chính & Chơi Thử
1. Trong cửa sổ **Project**, điều hướng đến thư mục:
   ```text
   Assets/ZenithUnity.unity
   ```
2. Nhấp đúp chuột để mở Scene `ZenithUnity`.
3. Nhấn nút **Play (Ctrl + P)** ở giữa trên cùng Editor để bắt đầu trải nghiệm.

---

## 🎮 3. Cơ Chế Điều Khiển (Controls)

| Phím bấm | Thao tác | Mô tả chi tiết |
| :--- | :--- | :--- |
| **W, A, S, D** | Di chuyển (Locomotion) | Di chuyển tự do theo hướng nhìn của Camera với Blend Tree mượt mà |
| **Left Shift** | Chạy nhanh (Sprint) | Tăng tốc độ di chuyển của nhân vật |
| **Mouse Move** | Xoay Camera | Camera Boom qua vai góc nhìn thứ ba, chuột tự động khóa giữa màn hình |
| **Chuột Trái (LMB)** | Tấn công Combo 5-Hit | Tung chuỗi liên hoàn 5 đòn thế võ thuật thực chiến |

---

## 🥋 4. Tính Năng Nổi Bật (Core Combat Features)

1. **Chuỗi Combo 5-Hit Võ Thuật Thực Chiến**:
   - Gồm 5 animation liên hoàn: `Đấm thẳng (Atk_P_1)` ➔ `Đấm móc (Atk_P_2)` ➔ `Đánh cùi chỏ (Elbow chop)` ➔ `Đấm lưng (Back fist)` ➔ `Xoay người đá rìu kết liễu (Spinning axe kick)`.
   - **Tốc độ đòn đánh dứt khoát**: Tích hợp bộ nhân tốc độ (`attackSpeedMultiplier = 1.3x`) giúp animation ra đòn sắc bén, gọn gàng chuẩn SIFU.

2. **Hệ Thống Input Buffering (Hàng Đợi Lệnh)**:
   - Cho phép người chơi nhấp chuột sớm đòn tiếp theo trong lúc đòn trước chưa kết thúc.
   - Nhân vật sẽ tự động nối đòn mượt mà ngay tại điểm dứt điểm mà không cần phải nhấp chuột siêu nhanh (không bị nuốt nút).

3. **Cửa Sổ Combo Độc Lập (Combo Window Time)**:
   - Cấp trọn vẹn **1.8 giây** sau khi đòn đánh trước dứt điểm để người chơi thong thả tung đòn tiếp theo.

4. **Cơ Chế Soft-Lock 360 Độ (Hút & Xoay Hướng Mục Tiêu)**:
   - Tự động quét và phát hiện kẻ địch gắn Tag `"Enemy"` trong bán kính 5 mét.
   - Hỗ trợ chọn mục tiêu theo góc nhìn camera hoặc hướng gạt phím di chuyển (WASD).

5. **Impact Frame Timing & Hitstop (Khựng Hình Va Chạm Chuẩn SIFU)**:
   - Sát thương và phản ứng trúng đòn chỉ kích hoạt đúng khoảnh khắc tay/chân chạm vào người đối thủ (ví dụ: đòn đá 5 xoay người 360 độ chỉ nhận đòn khi gót chân giáng xuống).
   - **Hitstop (`0.07s`)**: Khi va chạm, cả nhân vật và kẻ địch khựng nhẹ một khoảnh khắc tạo cảm giác độ nặng, uy lực của đòn đánh.

6. **Khoảng Cách Dừng Riêng Biệt Cho Từng Thế Võ (`Stop Distances Per Hit`)**:
   - Đòn đấm áp sát (`1.0m`), đòn chỏ sát người (`0.95m`), đòn đá rìu đứng xa (`1.45m`) giúp hoạt ảnh không bao giờ bị thọc sâu xuyên người đối thủ.

7. **Kiến Trúc Mở Rộng Kẻ Địch (`IDamageable`)**:
   - Sử dụng `IDamageable` interface: Bất kỳ quái vật, Dummy hay Boss nào chỉ cần gắn `CharacterController`, Tag `"Enemy"` và kế thừa `IDamageable` là lập tức tương thích hoàn hảo với hệ thống combat của Player.

---

## 📂 5. Cấu Trúc Thư Mục Chính (Project Structure)

```text
Assets/
├── Animators/                      # Animator Controller và các animation clip đòn đánh
├── Characters/                     # 3D Rigged Humanoid Models
├── Scripts/
│   ├── Camera/
│   │   └── ThirdPersonCamera.cs    # Camera Boom mượt mà góc nhìn thứ 3
│   └── Player/
│       ├── PlayerController.cs     # Bộ điều khiển chính: Di chuyển, Combo, Soft-Lock
│       ├── DummyEnemy.cs           # Script đối tượng luyện tập chịu đòn và Knockback
│       └── IDamageable.cs          # Interface chuẩn cho mọi đối tượng nhận sát thương
└── ZenithUnity.unity               # Scene gameplay chính
```

---

## 👨‍💻 Tác giả
- Dự án phát triển bởi **ChuongNguyenNHC**.
- Giấy phép: MIT License.
