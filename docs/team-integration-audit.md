# Đối chiếu phần code của nhóm

Tài liệu này ghi lại nguồn và cách tích hợp để tránh nhận nhầm tác giả hoặc vô tình ghi đè phần việc của thành viên.

## Minh Nhựt

- Nguồn: commit `5fad0bc` (`[add] oauth and smtp`) trên `origin/main`.
- Giữ đúng tên và vai trò chính của các file OAuth, SMTP, OTP và JWT: `AuthApiController`, `JwtTokenService`, `IJwtTokenService`, `PendingRegistrationService`, `SmtpEmailSender`, `EmailService` cùng các model/view liên quan.
- `EmailService.cs` được giữ nguyên implementation gốc. Runtime hiện đăng ký `SmtpEmailSender` vì service này đồng thời đáp ứng `IEmailSender` của Identity.
- `ConfirmEmailOtp` được hợp nhất vào `ConfirmRegistration`. Cách hiện tại xác nhận OTP trước khi tạo Identity user, tránh tài khoản chưa xác nhận nằm lại trong bảng người dùng.
- Migration `20260620063023_AddPendingRegistrations` được thay bằng migration hiện tại `20260620160746_AddPendingRegistrations`. Không chép cả hai migration vì cùng tạo một thay đổi schema.
- JWT giữ tên class, interface và endpoint gốc; phần tích hợp thêm kiểm tra khóa ký, refresh token và phản hồi cấu hình thiếu.

## Taddy811

- Nguồn: commit `4eba95e` (`Them 2 chuc nang`) trên `origin/Taddy811`.
- Giữ `LocalizationController`, `_LanguageSelector`, resource `vi/en/ja/ko/zh`, `ExamSecurityController` và `AntiCheatReportViewModel`.
- `ExamSecurityController.Report` giữ route, request model, kiểm tra người sở hữu lượt thi và hợp đồng JSON. Dữ liệu được ánh xạ sang `AntiCheatEvent` đã chuẩn hóa của dự án; metadata cũ được lưu trong `MetadataJson` để không thêm lại các cột trùng.
- View lượt thi Admin giữ nội dung chức năng của nhánh và được đồng bộ sang Materio, tiếng Việt có dấu, cùng model quản trị hiện tại.
- Không nhập lại `Areas/Teacher`: theo thiết kế đã thống nhất, giáo viên là chủ của từng lớp/phòng, không phải Identity role hoặc Area hệ thống.
- Không nhập các controller CRUD và migration cũ đã bị thay bởi luồng lớp học, đề thi và phân quyền theo ownership hiện tại.

## Quy tắc tích hợp

- Không đổi tên class/file backend của thành viên khi không có xung đột kỹ thuật.
- View được phép thay markup và CSS để đồng bộ Scholar/Materio, nhưng phải giữ action, route, validation và chức năng.
- Khi schema hoặc kiến trúc đã thay đổi, giữ hợp đồng nghiệp vụ và ghi rõ lớp tương thích thay vì phục hồi code cũ làm hỏng dữ liệu.
