using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Messenger
{
    public partial class ChatForm : Form
    {
        // Các biến thành viên
        private TcpListener tcpListener; // Đối tượng lắng nghe kết nối TCP đến từ các client khác
        private UdpClient udpClient; // Đối tượng client UDP để gửi và nhận gói tin multicast (dùng cho khám phá người dùng)
        private const int TcpPort = 13000; // Cổng TCP mặc định mà ứng dụng lắng nghe
        private const string MulticastAddress = "239.255.0.1"; // Địa chỉ IP của nhóm multicast
        private const int MulticastPort = 13001; // Cổng UDP multicast
        private CancellationTokenSource ctsNetwork; // Đối tượng dùng để hủy bỏ các tác vụ mạng khi ứng dụng đóng
        private List<TcpClient> activeConnections = new List<TcpClient>(); // Danh sách các kết nối TCP đang hoạt động với các client khác
        private Dictionary<TcpClient, string> connectionUserMap = new Dictionary<TcpClient, string>(); // Ánh xạ giữa kết nối TCP và tên người dùng tương ứng
        private List<string> onlineUsers = new List<string>(); // Danh sách tên của những người dùng đang online (bao gồm cả mình)
        private ListBox lbOnlineUsers; // Điều khiển ListBox trên giao diện để hiển thị danh sách người dùng online
        private ListBox lbChatMessages; // Điều khiển ListBox trên giao diện để hiển thị các tin nhắn chat
        private TextBox txtMessageInput; // Điều khiển TextBox để người dùng nhập tin nhắn
        private Label lblMyStatus; // Điều khiển Label hiển thị trạng thái của người dùng hiện tại (tên, online/offline)
        private Panel pnlInputArea; // Điều khiển Panel chứa khu vực nhập tin nhắn
        private Label lblClock; // Điều khiển Label hiển thị đồng hồ
        private Label lblCalendar; // Điều khiển Label hiển thị ngày tháng
        private System.Windows.Forms.Timer clockTimer; // Timer để cập nhật đồng hồ và lịch trên giao diện
        private ContextMenuStrip messageContextMenu; // Menu ngữ cảnh khi click chuột phải vào tin nhắn
        private ToolStripMenuItem copyMessageMenuItem; // Menu item "Sao chép" trong menu ngữ cảnh
        private Dictionary<string, PrivateChatForm> privateChatWindows = new Dictionary<string, PrivateChatForm>(); // Dictionary lưu trữ các cửa sổ chat riêng với từng người dùng
        private string myName = Environment.UserName + "_" + Guid.NewGuid().ToString().Substring(0, 4); // Tên người dùng hiện tại (khởi tạo từ tên máy tính và thêm một chuỗi ngẫu nhiên)
        // Các hằng số định dạng tin nhắn (dùng cho vẽ tin nhắn tùy chỉnh)
        private const int MessageBubblePadding = 12; // Khoảng đệm bên trong bong bóng tin nhắn
        private const int MessageBubbleCornerRadius = 18; // Bán kính bo tròn góc của bong bóng tin nhắn
        private const int AvatarSize = 32; // Kích thước (chiều rộng và chiều cao) của avatar
        private const int AvatarMargin = 8; // Khoảng cách giữa avatar và bong bóng tin nhắn
        private const int TimestampHeight = 15; // Chiều cao ước tính của dòng hiển thị thời gian
        private const int SenderNameHeight = 14; // Chiều cao ước tính của dòng hiển thị tên người gửi
        private const int VerticalSpacing = 8; // Khoảng cách dọc giữa các thành phần trong một mục tin nhắn
        private const int MaxBubbleWidth = 350; // <-- Đảm bảo dòng này tồn tại và ở đây
        // Màu sắc sử dụng trong giao diện
        private Color SentMessageColor = Color.FromArgb(136, 219, 136); // Màu bong bóng tin nhắn đã gửi
        private Color ReceivedMessageColor = Color.FromArgb(220, 220, 220); // Màu bong bóng tin nhắn nhận được
        private Color ChocolateColor = Color.FromArgb(165, 105, 50); // Màu nâu sô cô la (có thể dùng cho viền hoặc các thành phần khác)
        private Color BackgroundColor = Color.FromArgb(240, 242, 245); // Màu nền chính của form
        private Color UserListBackgroundColor = Color.FromArgb(250, 250, 220); // Màu nền của danh sách người dùng online
        private Color ChatAreaBackgroundColor = Color.White; // Màu nền của khu vực hiển thị tin nhắn chat
        private Color SelectedItemColor = Color.FromArgb(250, 250, 250);
        private System.Windows.Forms.Timer cleanupTimer; // Timer để thực hiện dọn dẹp lịch sử chat cũ định kỳ
        private const string ChatHistoryDirectory = @"D:\Chat"; // Thư mục lưu trữ lịch sử chat
        private const int HistoryRetentionDays = 7; // Số ngày giữ lại lịch sử chat
        private Button btnChangeName; // Nút bấm để đổi tên người dùng
        private Label lblFooterInfo; // Label hiển thị thông tin ở chân form
        private NotifyIcon notifyIcon; // Icon hiển thị ở khay hệ thống (system tray)
        private ContextMenuStrip trayMenu; // Menu ngữ cảnh khi click chuột phải vào tray icon
        //Hiệu ứng cầu vồng tên tác giả
        private System.Windows.Forms.Timer rainbowTimer; // Timer cho hiệu ứng cầu vồng
        private bool isRainbowActive; // Trạng thái hiệu ứng cầu vồng
        private Color originalAuthorColor; // Màu gốc của tên tác giả
        private double rainbowPhase; // Giai đoạn để tính toán màu cầu vồng




        private List<Tuple<string, int, int>> FindUrlsInText(string text)
        {
            List<Tuple<string, int, int>> urls = new List<Tuple<string, int, int>>();
            Regex urlRegex = new Regex(@"\b(?:https?://|www\.)?[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*\.[a-zA-Z]{2,}(?:[/\w- .?%&=#]*)?\b", RegexOptions.IgnoreCase);
            MatchCollection matches = urlRegex.Matches(text);

            Debug.WriteLine($"[DEBUG FindUrlsInText] Văn bản đầu vào: '{text}'");
            if (matches.Count > 0)
            {
                Debug.WriteLine($"[DEBUG FindUrlsInText] Tìm thấy {matches.Count} URL khớp:");
                foreach (Match match in matches)
                {
                    Debug.WriteLine($"[DEBUG FindUrlsInText]   Giá trị khớp: '{match.Value}', Vị trí: {match.Index}, Độ dài: {match.Length}");
                    urls.Add(Tuple.Create(match.Value, match.Index, match.Length));
                }
            }
            else
            {
                Debug.WriteLine($"[DEBUG FindUrlsInText] Không tìm thấy URL nào khớp.");
            }
            return urls;
        }


        public ChatForm()
        {
            // Hàm tạo của form ChatForm
            InitializeMessageContextMenu(); // Khởi tạo menu ngữ cảnh cho tin nhắn
            InitializeComponent(); // Khởi tạo các điều khiển trên giao diện (do Designer tạo ra)
            InitializeCleanupTimer(); // Khởi tạo timer dọn dẹp lịch sử
            InitializeClockTimer(); // Khởi tạo timer cập nhật đồng hồ
            ctsNetwork = new CancellationTokenSource(); // Khởi tạo đối tượng hủy tác vụ mạng
            this.Load += ChatForm_Load; // Gán sự kiện Load cho form
            this.FormClosing += ChatForm_FormClosing; // Gán sự kiện FormClosing cho form
            lblMyStatus.Text = $"Tôi: {myName} (Đang khởi động...)"; // Cập nhật trạng thái ban đầu
            onlineUsers.Add(myName); // Thêm tên của mình vào danh sách online ban đầu
            try
            {
                // Thử tải icon cho form từ tài nguyên nhúng
                this.Icon = new Icon(typeof(ChatForm), "icon.ico");
            }
            catch
            {
                // Bỏ qua nếu không tìm thấy icon (ứng dụng vẫn chạy)
            }
            #region TÊN TÁC GIẢ
            // Khởi tạo màu gốc của tên tác giả
            originalAuthorColor = lblFooterInfo.ForeColor;

            // Khởi tạo Timer cho hiệu ứng cầu vồng
            rainbowTimer = new System.Windows.Forms.Timer();
            rainbowTimer.Interval = 50; // Cập nhật mỗi 50ms để chuyển màu mượt mà
            rainbowTimer.Tick += RainbowTimer_Tick; // Gán sự kiện Tick
            isRainbowActive = false; // Ban đầu không kích hoạt hiệu ứng
            rainbowPhase = 0; // Khởi tạo giai đoạn màu

            // Gán sự kiện MouseEnter và MouseLeave cho lblFooterInfo
            lblFooterInfo.MouseEnter += LblFooterInfo_MouseEnter;
            lblFooterInfo.MouseLeave += LblFooterInfo_MouseLeave;

        }

        private Color GetRainbowColor(double phase)
        {
            // Tính toán các giá trị RGB sử dụng hàm sin với các pha khác nhau
            int red = (int)(Math.Sin(phase) * 127 + 128); // Giá trị từ 0 đến 255
            int green = (int)(Math.Sin(phase + 2 * Math.PI / 3) * 127 + 128); // Pha lệch 120 độ
            int blue = (int)(Math.Sin(phase + 4 * Math.PI / 3) * 127 + 128); // Pha lệch 240 độ

            // Đảm bảo giá trị nằm trong khoảng hợp lệ
            red = Math.Max(0, Math.Min(255, red));
            green = Math.Max(0, Math.Min(255, green));
            blue = Math.Max(0, Math.Min(255, blue));

            return Color.FromArgb(red, green, blue);
        }

        // Xử lý sự kiện Tick của Timer cầu vồng
        private void RainbowTimer_Tick(object sender, EventArgs e)
        {
            if (isRainbowActive)
            {
                // Tăng giai đoạn để chuyển đổi màu
                rainbowPhase += 0.1; // Điều chỉnh tốc độ chuyển màu (giá trị nhỏ hơn = chậm hơn)
                if (rainbowPhase > 2 * Math.PI) rainbowPhase -= 2 * Math.PI; // Giữ phase trong khoảng 0 đến 2π

                // Cập nhật màu chữ của lblFooterInfo
                lblFooterInfo.ForeColor = GetRainbowColor(rainbowPhase);
            }
        }

        // Xử lý sự kiện khi chuột di vào lblFooterInfo
        private void LblFooterInfo_MouseEnter(object sender, EventArgs e)
        {
            isRainbowActive = true; // Bật hiệu ứng cầu vồng
            rainbowPhase = 0; // Reset giai đoạn để bắt đầu từ màu đầu tiên
            rainbowTimer.Start(); // Bắt đầu Timer
        }

        // Xử lý sự kiện khi chuột rời khỏi lblFooterInfo
        private void LblFooterInfo_MouseLeave(object sender, EventArgs e)
        {
            isRainbowActive = false; // Tắt hiệu ứng cầu vồng
            rainbowTimer.Stop(); // Dừng Timer
            lblFooterInfo.ForeColor = originalAuthorColor; // Khôi phục màu gốc
        }
        #endregion
        // Khởi tạo timer dọn dẹp lịch sử
        private void InitializeCleanupTimer()
        {
            cleanupTimer = new System.Windows.Forms.Timer();
            // Đặt khoảng thời gian dọn dẹp là 24 giờ
            cleanupTimer.Interval = (int)TimeSpan.FromHours(24).TotalMilliseconds;
            // Gán phương thức xử lý sự kiện Tick của timer
            cleanupTimer.Tick += CleanupTimer_Tick;
        }

        // Xử lý sự kiện Tick của timer dọn dẹp lịch sử
        private void CleanupTimer_Tick(object sender, EventArgs e)
        {
            // Gọi phương thức thực hiện dọn dẹp
            CleanupOldChatHistory();
        }

        // Phương thức thực hiện dọn dẹp các tệp lịch sử chat cũ
        private void CleanupOldChatHistory()
        {
            try
            {
                // Kiểm tra xem thư mục lịch sử chat có tồn tại không
                if (!Directory.Exists(ChatHistoryDirectory))
                {
                    LogMessage($"Thư mục lịch sử chat không tồn tại: {ChatHistoryDirectory}");
                    return; // Thoát nếu thư mục không tồn tại
                }
                // Lấy danh sách tất cả các tệp .txt trong thư mục lịch sử chat
                string[] historyFiles = Directory.GetFiles(ChatHistoryDirectory, "*.txt");
                // Duyệt qua từng tệp lịch sử
                foreach (string filePath in historyFiles)
                {
                    try
                    {
                        // Lấy thời gian chỉnh sửa cuối cùng của tệp
                        DateTime lastWriteTime = File.GetLastWriteTime(filePath);
                        // Tính thời gian giới hạn (hiện tại trừ đi số ngày giữ lịch sử)
                        DateTime cutoffTime = DateTime.Now.AddDays(-HistoryRetentionDays);
                        // Nếu thời gian chỉnh sửa cuối cùng cũ hơn thời gian giới hạn
                        if (lastWriteTime < cutoffTime)
                        {
                            // Xóa tệp
                            File.Delete(filePath);
                            LogMessage($"Đã xóa tệp lịch sử cũ: {filePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ghi log nếu có lỗi khi xử lý một tệp cụ thể
                        LogMessage($"Lỗi khi xử lý tệp lịch sử {filePath}: {ex.Message}");
                    }
                }
                // Ghi log khi hoàn thành quá trình dọn dẹp
                LogMessage("Đã hoàn thành kiểm tra và xóa lịch sử chat cũ.");
            }
            catch (Exception ex)
            {
                // Ghi log nếu có lỗi chung trong quá trình dọn dẹp
                LogMessage($"Lỗi chung khi xóa lịch sử chat cũ: {ex.Message}");
            }
        }

        // Xử lý sự kiện click nút đổi tên
        private void BtnChangeName_Click(object sender, EventArgs e)
        {
            // Mở hộp thoại nhập liệu để người dùng nhập tên mới
            string newName = InputBox("Nhập tên mới của bạn:", "Đổi tên", myName);
            // Kiểm tra tên mới hợp lệ: không rỗng, không chỉ chứa khoảng trắng, không quá 50 ký tự và khác tên cũ
            if (!string.IsNullOrWhiteSpace(newName) && newName.Length <= 50 && newName != myName)
            {
                // Cập nhật tên mới
                UpdateMyName(newName);
            }
            else if (newName == myName)
            {
                // Thông báo nếu tên mới giống tên cũ
                MessageBox.Show("Tên mới phải khác tên hiện tại.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (!string.IsNullOrWhiteSpace(newName) && newName.Length > 50)
            {
                // Thông báo nếu tên quá dài
                MessageBox.Show("Tên không được vượt quá 50 ký tự.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Phương thức tạo và hiển thị hộp thoại nhập liệu tùy chỉnh
        private string InputBox(string prompt, string title, string defaultValue)
        {
            Form form = new Form(); // Tạo một form mới
            Label label = new Label(); // Tạo label hiển thị thông báo
            TextBox textBox = new TextBox(); // Tạo textbox để nhập liệu
            Button buttonOk = new Button(); // Tạo nút OK
            Button buttonCancel = new Button(); // Tạo nút Hủy

            form.Text = title; // Đặt tiêu đề cho form
            label.Text = prompt; // Đặt nội dung cho label
            textBox.Text = defaultValue; // Đặt giá trị mặc định cho textbox
            buttonOk.Text = "OK"; // Đặt chữ cho nút OK
            buttonCancel.Text = "Hủy"; // Đặt chữ cho nút Hủy
            buttonOk.DialogResult = DialogResult.OK; // Đặt kết quả trả về khi nhấn OK
            buttonCancel.DialogResult = DialogResult.Cancel; // Đặt kết quả trả về khi nhấn Hủy

            label.AutoSize = true; // Tự động điều chỉnh kích thước label theo nội dung
            label.Location = new Point(10, 10); // Đặt vị trí cho label
            textBox.Location = new Point(10, 30); // Đặt vị trí cho textbox
            textBox.Size = new Size(200, 20); // Đặt kích thước cho textbox
            buttonOk.Location = new Point(50, 60); // Đặt vị trí cho nút OK
            buttonCancel.Location = new Point(130, 60); // Đặt vị trí cho nút Hủy

            form.ClientSize = new Size(230, 95); // Đặt kích thước vùng client của form
            // Thêm các điều khiển vào form
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.FormBorderStyle = FormBorderStyle.FixedDialog; // Đặt kiểu viền form (không cho thay đổi kích thước)
            form.StartPosition = FormStartPosition.CenterScreen; // Hiển thị form ở giữa màn hình
            form.MinimizeBox = false; // Vô hiệu hóa nút Minimize
            form.MaximizeBox = false; // Vô hiệu hóa nút Maximize
            form.AcceptButton = buttonOk; // Nút OK sẽ được nhấn khi nhấn Enter
            form.CancelButton = buttonCancel; // Nút Hủy sẽ được nhấn khi nhấn Esc

            // Hiển thị form dưới dạng dialog và lấy kết quả trả về
            DialogResult result = form.ShowDialog();

            // Trả về nội dung của textbox nếu nhấn OK, ngược lại trả về chuỗi rỗng
            return result == DialogResult.OK ? textBox.Text : string.Empty;
        }

        // Phương thức cập nhật tên người dùng hiện tại
        private void UpdateMyName(string newName)
        {
            string oldName = myName; // Lưu tên cũ
            myName = newName; // Cập nhật tên mới
            lblMyStatus.Text = $"Tôi: {myName} (Online)"; // Cập nhật hiển thị trạng thái trên giao diện

            // Đồng bộ hóa truy cập vào danh sách onlineUsers để tránh xung đột luồng
            lock (onlineUsers)
            {
                // Nếu tên cũ còn trong danh sách, xóa nó đi
                if (onlineUsers.Contains(oldName))
                {
                    onlineUsers.Remove(oldName);
                }
                // Nếu tên mới chưa có trong danh sách, thêm vào
                if (!onlineUsers.Contains(myName))
                {
                    onlineUsers.Add(myName);
                }
            }
            // Cập nhật hiển thị danh sách người dùng online trên giao diện
            UpdateOnlineUsersList();
            // Gửi thông báo hiện diện (PRESENCE) để các client khác biết tên mới của mình
            SendPresenceUpdate();
            // Ghi log về việc đổi tên
            LogMessage($"Đã đổi tên từ '{oldName}' thành '{myName}'.");
        }

        // Phương thức gửi gói tin PRESENCE để thông báo sự hiện diện và tên của mình
        private async void SendPresenceUpdate()
        {
            // Tạo nội dung gói tin PRESENCE theo định dạng: PRESENCE:TênNgườiDùng:IP:Port
            string presenceMessage = $"PRESENCE:{myName}:{GetLocalIPAddress()}:{TcpPort}";
            // Chuyển nội dung tin nhắn thành mảng byte
            byte[] buffer = Encoding.UTF8.GetBytes(presenceMessage);

            try
            {
                // Tạo điểm cuối (endpoint) cho nhóm multicast
                IPEndPoint multicastEndpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), MulticastPort);
                // Gửi gói tin qua UDP đến nhóm multicast
                await udpClient.SendAsync(buffer, buffer.Length, multicastEndpoint);
                LogMessage($"Đã gửi gói tin PRESENCE tới multicast group: {MulticastAddress}:{MulticastPort}");
            }
            catch (Exception ex)
            {
                // Ghi log nếu có lỗi khi gửi qua multicast
                LogMessage($"Lỗi gửi gói tin PRESENCE qua multicast: {ex.Message}");
            }

            // Gửi thông tin hiện diện qua TCP đến tất cả các kết nối đang hoạt động
            // Sử dụng ToList() để tạo bản sao danh sách, tránh lỗi khi danh sách bị thay đổi trong quá trình duyệt
            foreach (var client in activeConnections.ToList())
            {
                // Kiểm tra xem kết nối còn hoạt động không
                if (client.Connected)
                {
                    try
                    {
                        // Chuyển nội dung tin nhắn thành mảng byte cho TCP
                        byte[] tcpBuffer = Encoding.UTF8.GetBytes(presenceMessage);
                        // Lấy luồng mạng của kết nối
                        NetworkStream stream = client.GetStream();
                        // Gửi dữ liệu không đồng bộ
                        await stream.WriteAsync(tcpBuffer, 0, tcpBuffer.Length);
                        await stream.FlushAsync(); // Đảm bảo dữ liệu được gửi đi ngay lập tức
                        LogMessage($"Đã gửi thông tin hiện diện (tên mới) đến {client.Client.RemoteEndPoint}");
                    }
                    catch (Exception ex)
                    {
                        // Ghi log nếu có lỗi khi gửi qua TCP
                        LogMessage($"Lỗi gửi thông tin hiện diện (tên mới) qua TCP: {ex.Message}");
                    }
                }
            }
        }

        // Phương thức khởi tạo các thành phần giao diện (được tạo tự động bởi Designer, nhưng có thể tùy chỉnh)
        private void InitializeComponent()
        {
            // Khởi tạo các điều khiển
            this.lbOnlineUsers = new ListBox();
            this.lbChatMessages = new ListBox();
            this.txtMessageInput = new TextBox();
            this.lblMyStatus = new Label();
            this.pnlInputArea = new Panel();
            this.btnChangeName = new Button();
            this.lblClock = new Label();
            this.lblCalendar = new Label();
            // Bắt đầu tạm dừng bố cục để cấu hình
            this.SuspendLayout();
            this.pnlInputArea.SuspendLayout();

            // Cấu hình txtMessageInput (ô nhập tin nhắn)
            this.txtMessageInput.Text = "Nhập tin nhắn ..."; // Văn bản gợi ý ban đầu
            this.txtMessageInput.ForeColor = Color.Gray; // Màu chữ gợi ý
            // Gán sự kiện khi ô nhập liệu nhận focus và mất focus
            this.txtMessageInput.GotFocus += TxtMessageInput_GotFocus;
            this.txtMessageInput.LostFocus += TxtMessageInput_LostFocus;

            // Cấu hình form chính
            this.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0))); // Font mặc định cho form
            this.BackColor = BackgroundColor; // Màu nền của form

            // Cấu hình lblMyStatus (hiển thị trạng thái của tôi)
            this.lblMyStatus.AutoSize = true; // Tự động điều chỉnh kích thước theo nội dung
            this.lblMyStatus.Location = new Point(10, 10); // Vị trí
            this.lblMyStatus.Name = "lblMyStatus";
            this.lblMyStatus.Size = new Size(150, 17); // Kích thước ban đầu
            this.lblMyStatus.TabIndex = 0; // Thứ tự Tab
            this.lblMyStatus.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold); // Font
            this.lblMyStatus.ForeColor = Color.Green; // Màu chữ

            // Cấu hình btnChangeName (nút đổi tên)
            this.btnChangeName.Location = new Point(55, lblMyStatus.Bottom + 5); // Vị trí (dưới lblMyStatus)
            this.btnChangeName.Name = "btnChangeName";
            this.btnChangeName.Size = new Size(70, 25); // Kích thước
            this.btnChangeName.TabIndex = 4; // Thứ tự Tab
            this.btnChangeName.Text = "Đổi tên"; // Văn bản nút
            this.btnChangeName.UseVisualStyleBackColor = true; // Sử dụng style mặc định của hệ điều hành
            this.btnChangeName.Click += BtnChangeName_Click; // Gán sự kiện click

            // Cấu hình lbOnlineUsers (danh sách người dùng online)
            this.lbOnlineUsers.FormattingEnabled = true; // Cho phép định dạng mục
            this.lbOnlineUsers.Location = new Point(10, btnChangeName.Bottom + 10); // Vị trí (dưới btnChangeName)
            this.lbOnlineUsers.Name = "lbOnlineUsers";
            this.lbOnlineUsers.Size = new Size(180, 450); // Kích thước ban đầu
            this.lbOnlineUsers.TabIndex = 1; // Thứ tự Tab
            this.lbOnlineUsers.Font = new Font("Segoe UI", 9.75F); // Font
            this.lbOnlineUsers.MouseDoubleClick += LbOnlineUsers_MouseDoubleClick; // Gán sự kiện double click chuột
            // neo điều khiển vào các cạnh của form khi form thay đổi kích thước
            this.lbOnlineUsers.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            this.lbOnlineUsers.BorderStyle = BorderStyle.None; // Bỏ viền
            this.lbOnlineUsers.BackColor = UserListBackgroundColor; // Màu nền

            // Cấu hình lblClock (hiển thị đồng hồ)
            this.lblClock.AutoSize = false; // Không tự động điều chỉnh kích thước
            this.lblClock.Size = new Size(180, 30); // Kích thước cố định
            this.lblClock.Location = new Point(10, lbOnlineUsers.Bottom + 10); // Vị trí (dưới lbOnlineUsers)
            this.lblClock.Name = "lblClock";
            this.lblClock.TabIndex = 5; // Thứ tự Tab
            this.lblClock.Font = new Font("Segoe UI", 12F, FontStyle.Bold); // Font
            this.lblClock.Text = "00:00:00"; // Văn bản mặc định
            this.lblClock.TextAlign = ContentAlignment.MiddleCenter; // Căn giữa văn bản

            // Cấu hình lblCalendar (hiển thị ngày tháng)
            this.lblCalendar.AutoSize = false; // Không tự động điều chỉnh kích thước
            this.lblCalendar.Size = new Size(180, 20); // Kích thước cố định
            this.lblCalendar.Location = new Point(10, lblClock.Bottom + 5); // Vị trí (dưới lblClock)
            this.lblCalendar.Name = "lblCalendar";
            this.lblCalendar.TabIndex = 6; // Thứ tự Tab
            this.lblCalendar.Font = new Font("Segoe UI", 9.75F, FontStyle.Italic); // Font
            this.lblCalendar.Text = "[01/01/2000]"; // Văn bản mặc định
            this.lblCalendar.TextAlign = ContentAlignment.MiddleCenter; // Căn giữa văn bản

            // Cấu hình lbChatMessages (danh sách tin nhắn chat)
            this.lbChatMessages.FormattingEnabled = true; // Cho phép định dạng mục
            this.lbChatMessages.Location = new Point(200, 10); // Vị trí
            this.lbChatMessages.Name = "lbChatMessages";
            this.lbChatMessages.Size = new Size(415, 620); // Kích thước ban đầu
            this.lbChatMessages.TabIndex = 2; // Thứ tự Tab
            // Đặt chế độ vẽ tùy chỉnh cho ListBox
            this.lbChatMessages.DrawMode = DrawMode.OwnerDrawVariable;
            // Gán các sự kiện vẽ tùy chỉnh
            this.lbChatMessages.DrawItem += LbChatMessages_DrawItem;
            this.lbChatMessages.MeasureItem += LbChatMessages_MeasureItem;
            this.lbChatMessages.ContextMenuStrip = messageContextMenu; // Gán menu ngữ cảnh
            // Neo điều khiển vào các cạnh của form
            this.lbChatMessages.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.lbChatMessages.BackColor = ChatAreaBackgroundColor; // Màu nền
            this.lbChatMessages.BorderStyle = BorderStyle.None; // Bỏ viền
            this.lbChatMessages.MouseMove += LbChatMessages_MouseMove; // Thêm sự kiện MouseMove

            // Gắn sự kiện MouseDown và MouseMove
            lbChatMessages.MouseDown += LbChatMessages_MouseDown;
            lbChatMessages.MouseMove += LbChatMessages_MouseMove;

            // Cấu hình pnlInputArea (panel chứa ô nhập tin nhắn)
            this.pnlInputArea.Location = new Point(200, this.lbChatMessages.Bottom + 15); // Vị trí (dưới lbChatMessages)
            this.pnlInputArea.Name = "pnlInputArea";
            this.pnlInputArea.Size = new Size(this.lbChatMessages.Width, 60); // Kích thước ban đầu
            this.pnlInputArea.TabIndex = 3; // Thứ tự Tab
            this.pnlInputArea.Controls.Add(this.txtMessageInput); // Thêm txtMessageInput vào panel
            this.pnlInputArea.BackColor = Color.PaleGoldenrod; // Màu nền
            // Neo panel vào các cạnh dưới và ngang của form
            this.pnlInputArea.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            this.pnlInputArea.BorderStyle = BorderStyle.None; // Bỏ viền

            // Cấu hình txtMessageInput (TextBox nhập tin nhắn)
            this.txtMessageInput.Text = "Nhập tin nhắn ..."; // Đặt văn bản mặc định khi TextBox rỗng
            this.txtMessageInput.ForeColor = Color.Gray; // Đặt màu chữ xám cho văn bản mặc định
            this.txtMessageInput.GotFocus += TxtMessageInput_GotFocus; // Đăng ký sự kiện khi TextBox nhận focus
            this.txtMessageInput.LostFocus += TxtMessageInput_LostFocus; // Đăng ký sự kiện khi TextBox mất focus
            this.txtMessageInput.TextChanged += TxtMessageInput_TextChanged; // Đăng ký sự kiện khi văn bản thay đổi
            this.txtMessageInput.BorderStyle = BorderStyle.None; // Loại bỏ đường viền của TextBox
            this.txtMessageInput.Location = new Point(1, 1); // Đặt vị trí của TextBox trong Panel chứa nó
            this.txtMessageInput.Multiline = true; // Cho phép TextBox hiển thị nhiều dòng văn bản
            this.txtMessageInput.Name = "txtMessageInput"; // Đặt tên cho TextBox
            this.txtMessageInput.Size = new Size(this.pnlInputArea.Width - 2, txtMessageInput.Font.Height * 4 + 6); // Đặt kích thước ban đầu (chiều cao cho khoảng 4 dòng)
            this.txtMessageInput.TabIndex = 0; // Đặt thứ tự Tab cho TextBox
            this.txtMessageInput.Font = new Font("Segoe UI", 9.75F); // Đặt Font và kích thước cho văn bản trong TextBox
            this.txtMessageInput.BackColor = Color.FromArgb(255, 255, 225); // Đặt màu nền cho TextBox
            this.txtMessageInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; // Neo TextBox vào các cạnh của Panel chứa để tự điều chỉnh kích thước
            this.txtMessageInput.AcceptsReturn = true; // Cho phép phím Enter tạo dòng mới trong TextBox (khi giữ Shift+Enter)
            this.txtMessageInput.ScrollBars = ScrollBars.None; // Ban đầu không hiển thị thanh cuộn
            this.txtMessageInput.WordWrap = true; // Tự động xuống dòng khi văn bản vượt quá chiều rộng
            this.txtMessageInput.KeyDown += TxtMessageInput_KeyDown; // Đăng ký sự kiện nhấn phím để xử lý gửi tin nhắn hoặc xuống dòng

            // Cấu hình form chính (tiếp)
            this.ClientSize = new Size(620, 720); // Kích thước vùng client của form
            // Thêm các điều khiển chính vào form
            this.Controls.Add(this.pnlInputArea);
            this.Controls.Add(this.lbChatMessages);
            this.Controls.Add(this.lbOnlineUsers);
            this.Controls.Add(this.btnChangeName);
            this.Controls.Add(this.lblMyStatus);
            this.Controls.Add(this.lblClock);
            this.Controls.Add(this.lblCalendar);
            this.Name = "ChatForm"; // Tên của form
            this.Text = "Messenger"; // Tiêu đề của cửa sổ form
            this.MinimumSize = new Size(620, 720); // Kích thước tối thiểu của form
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // Kiểu viền form (không cho thay đổi kích thước)
            this.MaximizeBox = false; // Vô hiệu hóa nút phóng to

            // Cấu hình lblFooterInfo (thông tin footer)
            this.lblFooterInfo = new Label(); // Tạo label
            this.lblFooterInfo.AutoSize = true; // Tự động điều chỉnh kích thước
            // Điều chỉnh vị trí Y để label hiển thị ở gần cuối form
            this.lblFooterInfo.Location = new System.Drawing.Point(10, this.ClientSize.Height - 30); // Vị trí
            this.lblFooterInfo.Name = "lblFooterInfo";
            this.lblFooterInfo.Size = new System.Drawing.Size(150, 30); // Kích thước ban đầu (AutoSize sẽ điều chỉnh)
            this.lblFooterInfo.TabIndex = 99; // Thứ tự Tab (giá trị lớn để tránh xung đột)
            // Sử dụng Environment.NewLine để xuống dòng trong cùng một label
            this.lblFooterInfo.Text = "2025 ©Nông Văn Phấn" + Environment.NewLine + "FAB Inspection Part(SDV)"; // Nội dung
            this.lblFooterInfo.ForeColor = System.Drawing.Color.LightGray; // Màu chữ
            this.lblFooterInfo.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0))); // Font
            // Neo label vào cạnh dưới và trái của form
            this.lblFooterInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.Controls.Add(this.lblFooterInfo); // Thêm label vào form

            // Cập nhật lại kích thước form tối thiểu nếu cần để đảm bảo label hiển thị
            this.MinimumSize = new Size(this.MinimumSize.Width, Math.Max(this.MinimumSize.Height, this.ClientSize.Height - this.lblFooterInfo.Height - 10)); // Đảm bảo chiều cao tối thiểu

            // Khởi tạo NotifyIcon (icon ở khay hệ thống)
            this.notifyIcon = new NotifyIcon();
            // Sử dụng icon của form làm icon cho NotifyIcon
            this.notifyIcon.Icon = this.Icon = new Icon(typeof(ChatForm), "icon.ico");// Lấy icon từ tài nguyên nhúng
            this.notifyIcon.Visible = false; // Ban đầu ẩn icon
            this.notifyIcon.Text = "Messenger"; // Tooltip khi rê chuột vào icon

            // Khởi tạo ContextMenuStrip cho NotifyIcon (menu khi click chuột phải)
            this.trayMenu = new ContextMenuStrip();
            ToolStripMenuItem openMenuItem = new ToolStripMenuItem("Mở"); // Menu item "Mở"
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("Thoát"); // Menu item "Thoát"

            // Gán sự kiện click cho các menu item
            openMenuItem.Click += new EventHandler(OpenMenuItem_Click);
            exitMenuItem.Click += new EventHandler(ExitMenuItem_Click);

            // Thêm các menu item vào menu ngữ cảnh
            this.trayMenu.Items.Add(openMenuItem);
            this.trayMenu.Items.Add(exitMenuItem);

            // Gán menu ngữ cảnh cho NotifyIcon
            this.notifyIcon.ContextMenuStrip = this.trayMenu;

            // Gán sự kiện double click cho NotifyIcon để mở lại form
            this.notifyIcon.MouseDoubleClick += new MouseEventHandler(notifyIcon_MouseDoubleClick);
            // Gán sự kiện click vào thông báo (balloon tip)
            this.notifyIcon.BalloonTipClicked += new EventHandler(notifyIcon_BalloonTipClicked);

            // Kết thúc tạm dừng bố cục và áp dụng các thay đổi
            this.PerformLayout();
            this.pnlInputArea.ResumeLayout(false);
            this.pnlInputArea.PerformLayout();
        }

        // Khởi tạo timer cập nhật đồng hồ và lịch
        private void InitializeClockTimer()
        {
            clockTimer = new System.Windows.Forms.Timer();
            clockTimer.Interval = 500; // Khoảng thời gian cập nhật (0.5 giây)
            clockTimer.Tick += ClockTimer_Tick; // Gán phương thức xử lý sự kiện Tick
            clockTimer.Start(); // Bắt đầu timer
        }

        // Xử lý sự kiện Tick của timer đồng hồ/lịch
        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Lấy thông tin múi giờ GMT+7 (Giờ Đông Dương)
                TimeZoneInfo gmtPlus7 = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                // Chuyển đổi thời gian UTC hiện tại sang múi giờ GMT+7
                DateTime gmtPlus7Time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, gmtPlus7);
                // Cập nhật hiển thị giờ (HH:mm:ss)
                lblClock.Text = gmtPlus7Time.ToString("HH:mm:ss");
                // Cập nhật hiển thị ngày tháng (dd/MM/yyyy)
                lblCalendar.Text = gmtPlus7Time.ToString("dd/MM/yyyy");
            }
            catch (Exception ex)
            {
                // Ghi log nếu có lỗi khi cập nhật đồng hồ/lịch
                LogMessage($"Lỗi khi cập nhật đồng hồ/lịch: {ex.Message}");
            }
        }

        // Xử lý sự kiện khi ô nhập tin nhắn nhận focus
        private void TxtMessageInput_GotFocus(object sender, EventArgs e)
        {
            // Nếu nội dung là văn bản gợi ý ban đầu
            if (txtMessageInput.Text == "Nhập tin nhắn ...")
            {
                txtMessageInput.Text = ""; // Xóa văn bản gợi ý
                txtMessageInput.ForeColor = Color.Black; // Đổi màu chữ về màu đen
            }
        }

        // Xử lý sự kiện khi ô nhập tin nhắn mất focus
        private void TxtMessageInput_LostFocus(object sender, EventArgs e)
        {
            // Nếu nội dung rỗng hoặc chỉ chứa khoảng trắng
            if (string.IsNullOrWhiteSpace(txtMessageInput.Text))
            {
                txtMessageInput.Text = "Nhập tin nhắn ..."; // Hiển thị lại văn bản gợi ý
                txtMessageInput.ForeColor = Color.Gray; // Đổi màu chữ về màu xám
            }
        }

        // Phương thức vẽ viền màu nâu sô cô la cho một điều khiển (không sử dụng trong mã hiện tại)
        private void Control_Paint_ChocolateBorder(object sender, PaintEventArgs e)
        {
            Control control = (Control)sender;
            using (Pen chocolatePen = new Pen(ChocolateColor, 2))
            {
                e.Graphics.DrawRectangle(chocolatePen, 0, 0, control.Width - 1, control.Height - 1);
            }
        }

        // Xử lý sự kiện khi nội dung ô nhập tin nhắn thay đổi
        private void TxtMessageInput_TextChanged(object sender, EventArgs e)
        {
            int lineCount = txtMessageInput.GetLineFromCharIndex(txtMessageInput.TextLength) + 1;
            txtMessageInput.ScrollBars = lineCount > 3 ? ScrollBars.Vertical : ScrollBars.None;
        }

        // Xử lý sự kiện Paint cho nút gửi (không có logic cụ thể trong mã hiện tại)
        private void BtnSend_Paint(object sender, PaintEventArgs e)
        {
            // Giữ nguyên
        }

        // Khởi tạo menu ngữ cảnh cho tin nhắn
        private void InitializeMessageContextMenu()
        {
            messageContextMenu = new ContextMenuStrip(); // Tạo menu ngữ cảnh mới
            copyMessageMenuItem = new ToolStripMenuItem("Sao chép"); // Tạo menu item "Sao chép"
            copyMessageMenuItem.Click += CopyMessageMenuItem_Click; // Gán sự kiện click cho menu item
            messageContextMenu.Items.Add(copyMessageMenuItem); // Thêm menu item vào menu ngữ cảnh
            messageContextMenu.Opening += MessageContextMenu_Opening; // Gán sự kiện Opening (trước khi menu hiển thị)
        }

        // Xử lý sự kiện Opening của menu ngữ cảnh tin nhắn (trước khi hiển thị)
        private void MessageContextMenu_Opening(object sender, CancelEventArgs e)
        {
            // Kích hoạt menu item "Sao chép" chỉ khi có một mục tin nhắn được chọn
            copyMessageMenuItem.Enabled = lbChatMessages.SelectedIndex >= 0 && lbChatMessages.SelectedItem is ChatMessage;
            // Nếu không có mục nào được chọn hoặc mục được chọn không phải là ChatMessage, hủy hiển thị menu
            if (!copyMessageMenuItem.Enabled)
            {
                e.Cancel = true;
            }
        }

        // Xử lý sự kiện click menu item "Sao chép"
        private void CopyMessageMenuItem_Click(object sender, EventArgs e)
        {
            // Kiểm tra xem mục được chọn có phải là ChatMessage không
            if (lbChatMessages.SelectedItem is ChatMessage selectedMessage)
            {
                // Sao chép nội dung tin nhắn vào clipboard
                Clipboard.SetText(selectedMessage.Content);
                LogMessage("Đã sao chép tin nhắn vào clipboard."); // Ghi log
            }
        }

        // Xử lý sự kiện di chuyển chuột trên ListBox tin nhắn
        private void LbChatMessages_MouseMove(object sender, MouseEventArgs e)
        {
            ListBox listBox = sender as ListBox; // Lấy đối tượng ListBox từ sender
            if (listBox == null) return; // Nếu không phải ListBox, thoát khỏi hàm

            Debug.WriteLine($"[DEBUG MouseMove] Di chuyển chuột tại: X={e.X}, Y={e.Y}"); // Ghi log vị trí chuột

            int index = listBox.IndexFromPoint(e.Location); // Lấy chỉ số mục tại vị trí chuột
            Debug.WriteLine($"[DEBUG MouseMove] Index: {index}, Vị trí: {e.Location}"); // Ghi log chỉ số mục và vị trí

            // Kiểm tra xem chỉ số mục có hợp lệ không
            if (index < 0 || index >= listBox.Items.Count)
            {
                listBox.Cursor = Cursors.Default; // Nếu không hợp lệ, đặt con trỏ mặc định
                Debug.WriteLine($"[DEBUG MouseMove]Không có mục nào ở vị trí, Con trỏ được đặt thành Mặc định"); // Ghi log
                return; // Thoát khỏi hàm
            }

            // Kiểm tra mục tại chỉ số đó có phải là ChatMessage và có chứa URL không
            if (listBox.Items[index] is ChatMessage message && message.Urls != null && message.Urls.Any())
            {
                Rectangle itemBounds = listBox.GetItemRectangle(index); // Lấy ranh giới hình chữ nhật của mục
                PointF relativeLocation = new PointF(e.X - itemBounds.X, e.Y - itemBounds.Y); // Tính toán vị trí chuột tương đối so với góc trên bên trái của mục

                Debug.WriteLine($"[DEBUG MouseMove] Index: {index}, ItemBounds: {itemBounds}, Vị trí tương đối: {relativeLocation}"); // Ghi log

                // Tái tạo bố cục để tìm vùng URL (phần này lặp lại logic từ DrawItem/MeasureItem để xác định vị trí văn bản và URL)
                float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 3); // Tính chiều rộng văn bản tối đa trong bong bóng
                if (maxTextWidth < 1) maxTextWidth = 1; // Đảm bảo chiều rộng tối thiểu là 1

                float totalTextHeight = 0; // Tổng chiều cao của văn bản đã đo
                float totalTextWidth = 0; // Tổng chiều rộng của văn bản đã đo (sử dụng cho tính toán bong bóng)
                using (Graphics g = listBox.CreateGraphics()) // Tạo đối tượng Graphics để đo
                using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces)) // Cấu hình StringFormat
                {
                    sf.Trimming = StringTrimming.Character; // Cắt ký tự

                    if (!string.IsNullOrEmpty(message.Content)) // Nếu nội dung tin nhắn không rỗng
                    {
                        int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi văn bản
                        var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL theo vị trí
                        foreach (var urlInfo in sortedUrls) // Lặp qua từng URL
                        {
                            string url = urlInfo.Item1; // URL
                            int urlStartIndex = urlInfo.Item2; // Vị trí bắt đầu của URL
                            int urlLength = urlInfo.Item3; // Độ dài của URL

                            if (urlStartIndex > currentTextIndex) // Nếu có văn bản trước URL
                            {
                                string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex); // Lấy văn bản trước URL
                                if (!string.IsNullOrEmpty(textBeforeUrl)) // Nếu văn bản trước không rỗng
                                {
                                    SizeF sizeBefore = g.MeasureString(textBeforeUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước văn bản trước
                                    totalTextHeight += sizeBefore.Height; // Cộng dồn chiều cao
                                    totalTextWidth = Math.Max(totalTextWidth, sizeBefore.Width); // Cập nhật chiều rộng tối đa
                                }
                            }

                            SizeF sizeUrl = g.MeasureString(url, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước URL
                            totalTextHeight += sizeUrl.Height; // Cộng dồn chiều cao của URL
                            totalTextWidth = Math.Max(totalTextWidth, sizeUrl.Width); // Cập nhật chiều rộng tối đa
                            currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí hiện tại
                        }

                        if (currentTextIndex < message.Content.Length) // Nếu còn văn bản sau URL cuối cùng
                        {
                            string textAfterUrl = message.Content.Substring(currentTextIndex); // Lấy văn bản sau URL
                            if (!string.IsNullOrEmpty(textAfterUrl)) // Nếu văn bản sau không rỗng
                            {
                                SizeF sizeAfter = g.MeasureString(textAfterUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước văn bản sau
                                totalTextHeight += sizeAfter.Height; // Cộng dồn chiều cao
                                totalTextWidth = Math.Max(totalTextWidth, sizeAfter.Width); // Cập nhật chiều rộng tối đa
                            }
                        }
                    }
                }

                int bubbleWidth = Math.Min(MaxBubbleWidth, Math.Max(20, (int)totalTextWidth + MessageBubblePadding * 2)); // Tính toán chiều rộng bong bóng
                Size bubbleSize = new Size(bubbleWidth, Math.Max(20, (int)(totalTextHeight + MessageBubblePadding * 2))); // Tính toán chiều cao bong bóng
                Rectangle bubbleRect, textRect; // Hình chữ nhật cho bong bóng và văn bản

                if (message.IsSentByMe) // Nếu tin nhắn do tôi gửi
                {
                    Rectangle avatarRect = new Rectangle(itemBounds.Right - AvatarSize - 10, itemBounds.Top + VerticalSpacing, AvatarSize, AvatarSize); // Vị trí avatar (bên phải)
                    bubbleRect = new Rectangle(avatarRect.Left - AvatarMargin - bubbleSize.Width, itemBounds.Top + VerticalSpacing, bubbleSize.Width, bubbleSize.Height); // Vị trí bong bóng (bên trái avatar)
                    textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth, (int)totalTextHeight); // Vị trí văn bản trong bong bóng
                }
                else // Nếu tin nhắn do người khác gửi
                {
                    Rectangle avatarRect = new Rectangle(itemBounds.Left, itemBounds.Top + VerticalSpacing, AvatarSize, AvatarSize); // Vị trí avatar (bên trái)
                    int bubbleTopY = itemBounds.Top + VerticalSpacing; // Vị trí Y ban đầu của bong bóng
                    if (!string.IsNullOrEmpty(message.SenderName)) // Nếu có tên người gửi
                    {
                        using (Graphics g = listBox.CreateGraphics()) // Tạo đối tượng Graphics
                        using (Font senderNameFont = new Font(listBox.Font.FontFamily, listBox.Font.Size * 0.85f, FontStyle.Bold)) // Font cho tên người gửi
                        {
                            SizeF senderNameSizeF = g.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic); // Đo kích thước tên người gửi
                            bubbleTopY += (int)Math.Ceiling(senderNameSizeF.Height) + VerticalSpacing; // Cộng thêm chiều cao tên người gửi và khoảng cách vào vị trí Y của bong bóng
                        }
                    }
                    bubbleRect = new Rectangle(avatarRect.Right + AvatarMargin, bubbleTopY, bubbleSize.Width, bubbleSize.Height); // Vị trí bong bóng (bên phải avatar)
                    textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth, (int)totalTextHeight); // Vị trí văn bản trong bong bóng
                }

                // Tính toán vùng URL trong bong bóng (lặp lại logic từ DrawItem/MeasureItem)
                bool isOverUrl = false; // Biến kiểm tra chuột có nằm trên URL không
                using (Graphics g = listBox.CreateGraphics()) // Tạo đối tượng Graphics
                using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces)) // Cấu hình StringFormat
                {
                    sf.Trimming = StringTrimming.Character; // Cắt ký tự
                    sf.Alignment = StringAlignment.Near; // Căn lề trái
                    sf.LineAlignment = StringAlignment.Near; // Căn lề trên

                    float currentY = textRect.Y - itemBounds.Y; // Vị trí Y hiện tại (tương đối so với itemBounds)
                    int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi
                    var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL

                    foreach (var urlInfo in sortedUrls) // Lặp qua từng URL
                    {
                        string url = urlInfo.Item1;
                        int urlStartIndex = urlInfo.Item2;
                        int urlLength = urlInfo.Item3;

                        if (urlStartIndex > currentTextIndex) // Nếu có văn bản trước URL
                        {
                            string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex); // Lấy văn bản trước URL
                            if (!string.IsNullOrEmpty(textBeforeUrl)) // Nếu không rỗng
                            {
                                SizeF sizeBefore = g.MeasureString(textBeforeUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước
                                currentY += sizeBefore.Height; // Cập nhật vị trí Y
                            }
                        }

                        SizeF sizeUrl = g.MeasureString(url, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước URL
                        RectangleF urlRect = new RectangleF(textRect.X - itemBounds.X, currentY, sizeUrl.Width, sizeUrl.Height); // Tính toán hình chữ nhật cho vùng URL (tương đối)
                        Debug.WriteLine($"[DEBUG MouseMove] Đang kiểm tra URL: '{url}', Rect: {urlRect}"); // Ghi log

                        if (urlRect.Contains(relativeLocation)) // Kiểm tra xem chuột có nằm trong vùng URL này không
                        {
                            isOverUrl = true; // Nếu có, đặt biến cờ
                            Debug.WriteLine($"[DEBUG MouseMove] Di chuột qua URL: {url}, Rect: {urlRect}"); // Ghi log
                            break; // Thoát vòng lặp vì đã tìm thấy URL
                        }

                        currentY += sizeUrl.Height; // Cập nhật vị trí Y
                        currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí hiện tại trong chuỗi
                    }
                }

                // Đặt con trỏ chuột: Hand nếu di chuột qua URL, ngược lại là Default
                listBox.Cursor = isOverUrl ? Cursors.Hand : Cursors.Default;
                Debug.WriteLine($"[DEBUG MouseMove] Con trỏ được đặt thành: {(isOverUrl ? "Hand" : "Default")}"); // Ghi log
            }
            else // Nếu mục không phải ChatMessage hoặc không có URL
            {
                listBox.Cursor = Cursors.Default; // Đặt con trỏ mặc định
                Debug.WriteLine($"[DEBUG MouseMove] Không có tin nhắn hoặc URL hợp lệ, Con trỏ được đặt thành Mặc định"); // Ghi log
            }
        }

        // Xử lý sự kiện nhấn chuột trên ListBox tin nhắn
        private void LbChatMessages_MouseDown(object sender, MouseEventArgs e)
        {
            ListBox listBox = sender as ListBox; // Lấy đối tượng ListBox từ sender
            if (listBox == null) return; // Nếu không phải ListBox, thoát khỏi hàm

            Debug.WriteLine($"[DEBUG MouseDown] Click chuột vào: X={e.X}, Y={e.Y}"); // Ghi log vị trí nhấp chuột

            if (e.Button == MouseButtons.Right) // Nếu nhấn chuột phải
            {
                int index = listBox.IndexFromPoint(e.Location); // Lấy chỉ số mục tại vị trí chuột
                Debug.WriteLine($"[DEBUG MouseDown] Đã phát hiện nhấp chuột phải, Index: {index}"); // Ghi log
                if (index != ListBox.NoMatches) // Nếu tìm thấy mục tại vị trí nhấp
                {
                    listBox.SelectedIndex = index; // Chọn mục đó
                }
                else // Nếu không tìm thấy mục
                {
                    listBox.SelectedIndex = -1; // Bỏ chọn tất cả các mục
                }
            }
            else if (e.Button == MouseButtons.Left) // Nếu nhấn chuột trái
            {
                int index = listBox.IndexFromPoint(e.Location); // Lấy chỉ số mục tại vị trí chuột
                Debug.WriteLine($"[DEBUG MouseDown] Đã phát hiện nhấp chuột trái, Index: {index}, Vị trí: {e.Location}"); // Ghi log

                // Kiểm tra chỉ số có hợp lệ và nằm trong giới hạn các mục không
                if (index != ListBox.NoMatches && index < listBox.Items.Count)
                {
                    object item = listBox.Items[index]; // Lấy mục tại chỉ số đó
                                                        // Kiểm tra mục có phải là ChatMessage và có chứa URL không
                    if (item is ChatMessage message && message.Urls != null && message.Urls.Any())
                    {
                        Rectangle itemBounds = listBox.GetItemRectangle(index); // Lấy ranh giới hình chữ nhật của mục
                        PointF relativeClickLocation = new PointF(e.X - itemBounds.X, e.Y - itemBounds.Y); // Tính toán vị trí nhấp chuột tương đối so với góc trên bên trái của mục

                        Debug.WriteLine($"[DEBUG MouseDown] Index: {index}, ItemBounds: {itemBounds}, Vị trí tương đối: {relativeClickLocation}"); // Ghi log

                        // Tái tạo bố cục để tìm vùng URL (phần này lặp lại logic tương tự như MouseMove)
                        float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2); // Tính chiều rộng văn bản tối đa
                        if (maxTextWidth < 1) maxTextWidth = 1; // Đảm bảo chiều rộng tối thiểu là 1

                        float totalTextHeight = 0; // Tổng chiều cao văn bản đã đo
                        float totalTextWidth = 0; // Tổng chiều rộng văn bản đã đo
                        using (Graphics g = listBox.CreateGraphics()) // Tạo đối tượng Graphics
                        using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces)) // Cấu hình StringFormat
                        {
                            sf.Trimming = StringTrimming.Character; // Cắt ký tự

                            if (!string.IsNullOrEmpty(message.Content)) // Nếu nội dung tin nhắn không rỗng
                            {
                                int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi
                                var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL
                                foreach (var urlInfo in sortedUrls) // Lặp qua từng URL
                                {
                                    string url = urlInfo.Item1;
                                    int urlStartIndex = urlInfo.Item2;
                                    int urlLength = urlInfo.Item3;

                                    if (urlStartIndex > currentTextIndex) // Nếu có văn bản trước URL
                                    {
                                        string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex); // Lấy văn bản trước URL
                                        if (!string.IsNullOrEmpty(textBeforeUrl)) // Nếu không rỗng
                                        {
                                            SizeF sizeBefore = g.MeasureString(textBeforeUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước
                                            totalTextHeight += sizeBefore.Height; // Cộng dồn chiều cao
                                            totalTextWidth = Math.Max(totalTextWidth, sizeBefore.Width); // Cập nhật chiều rộng
                                        }
                                    }

                                    SizeF sizeUrl = g.MeasureString(url, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước URL
                                    totalTextHeight += sizeUrl.Height; // Cộng dồn chiều cao
                                    totalTextWidth = Math.Max(totalTextWidth, sizeUrl.Width); // Cập nhật chiều rộng
                                    currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí
                                }

                                if (currentTextIndex < message.Content.Length) // Nếu còn văn bản sau URL cuối cùng
                                {
                                    string textAfterUrl = message.Content.Substring(currentTextIndex); // Lấy văn bản sau URL
                                    if (!string.IsNullOrEmpty(textAfterUrl)) // Nếu không rỗng
                                    {
                                        SizeF sizeAfter = g.MeasureString(textAfterUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước
                                        totalTextHeight += sizeAfter.Height; // Cộng dồn chiều cao
                                        totalTextWidth = Math.Max(totalTextWidth, sizeAfter.Width); // Cập nhật chiều rộng
                                    }
                                }
                            }
                        }

                        int bubbleWidth = Math.Min(MaxBubbleWidth, Math.Max(20, (int)totalTextWidth + MessageBubblePadding * 3)); // Tính toán chiều rộng bong bóng
                        Size bubbleSize = new Size(bubbleWidth, Math.Max(20, (int)(totalTextHeight + MessageBubblePadding * 2))); // Tính toán chiều cao bong bóng
                        Rectangle bubbleRect, textRect; // Hình chữ nhật cho bong bóng và văn bản

                        if (message.IsSentByMe) // Nếu tin nhắn do tôi gửi
                        {
                            Rectangle avatarRect = new Rectangle(itemBounds.Right - AvatarSize - 10, itemBounds.Top + VerticalSpacing, AvatarSize, AvatarSize); // Vị trí avatar (bên phải)
                            bubbleRect = new Rectangle(avatarRect.Left - AvatarMargin - bubbleSize.Width, itemBounds.Top + VerticalSpacing, bubbleSize.Width, bubbleSize.Height); // Vị trí bong bóng (bên trái avatar)
                            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth, (int)totalTextHeight); // Vị trí văn bản
                        }
                        else // Nếu tin nhắn do người khác gửi
                        {
                            Rectangle avatarRect = new Rectangle(itemBounds.Left + 10, itemBounds.Top + VerticalSpacing, AvatarSize, AvatarSize); // Vị trí avatar (bên trái)
                            int bubbleTopY = itemBounds.Top + VerticalSpacing; // Vị trí Y ban đầu
                            if (!string.IsNullOrEmpty(message.SenderName)) // Nếu có tên người gửi
                            {
                                using (Graphics g = listBox.CreateGraphics()) // Tạo đối tượng Graphics
                                using (Font senderNameFont = new Font(listBox.Font.FontFamily, listBox.Font.Size * 0.85f, FontStyle.Bold)) // Font tên người gửi
                                {
                                    SizeF senderNameSizeF = g.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic); // Đo kích thước
                                    bubbleTopY += (int)Math.Ceiling(senderNameSizeF.Height) + VerticalSpacing; // Cập nhật vị trí Y
                                }
                            }
                            bubbleRect = new Rectangle(avatarRect.Right + AvatarMargin, bubbleTopY, bubbleSize.Width, bubbleSize.Height); // Vị trí bong bóng (bên phải avatar)
                            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth, (int)totalTextHeight); // Vị trí văn bản
                        }

                        // Tính toán vùng URL trong bong bóng (lặp lại logic)
                        using (Graphics g = listBox.CreateGraphics()) // Tạo đối tượng Graphics
                        using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces)) // Cấu hình StringFormat
                        {
                            sf.Trimming = StringTrimming.Character; // Cắt ký tự
                            sf.Alignment = StringAlignment.Near; // Căn lề trái
                            sf.LineAlignment = StringAlignment.Near; // Căn lề trên

                            float currentY = textRect.Y - itemBounds.Y; // Vị trí Y hiện tại (tương đối so với itemBounds)
                            int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi
                            var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL

                            foreach (var urlInfo in sortedUrls) // Lặp qua từng URL
                            {
                                string url = urlInfo.Item1;
                                int urlStartIndex = urlInfo.Item2;
                                int urlLength = urlInfo.Item3;

                                if (urlStartIndex > currentTextIndex) // Nếu có văn bản trước URL
                                {
                                    string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex); // Lấy văn bản trước URL
                                    if (!string.IsNullOrEmpty(textBeforeUrl)) // Nếu không rỗng
                                    {
                                        SizeF sizeBefore = g.MeasureString(textBeforeUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước
                                        currentY += sizeBefore.Height; // Cập nhật vị trí Y
                                    }
                                }

                                SizeF sizeUrl = g.MeasureString(url, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf); // Đo kích thước URL
                                RectangleF urlRect = new RectangleF(textRect.X - itemBounds.X, currentY, sizeUrl.Width, sizeUrl.Height); // Tính toán hình chữ nhật cho vùng URL (tương đối)
                                Debug.WriteLine($"[DEBUG MouseDown] Kiểm tra URL: '{url}', Rect: {urlRect}"); // Ghi log

                                if (urlRect.Contains(relativeClickLocation)) // Kiểm tra xem vị trí nhấp chuột có nằm trong vùng URL này không
                                {
                                    Debug.WriteLine($"[DEBUG MouseDown] Số lần nhấp vào URL: {url}, Rect: {urlRect}"); // Ghi log
                                    try
                                    {
                                        // Cố gắng mở URL
                                        if (!string.IsNullOrEmpty(url)) // Nếu URL không rỗng
                                        {
                                            // Thêm tiền tố http:// hoặc https:// nếu chưa có
                                            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                            {
                                                url = "https://" + url;
                                            }
                                            // Tạo đối tượng ProcessStartInfo để cấu hình cách mở URL
                                            ProcessStartInfo psi = new ProcessStartInfo
                                            {
                                                FileName = url, // Tên tệp hoặc URL cần mở
                                                UseShellExecute = true // Sử dụng shell của hệ điều hành để mở (sẽ mở bằng trình duyệt mặc định)
                                            };
                                            Process.Start(psi); // Chạy process để mở URL
                                            Debug.WriteLine($"[DEBUG MouseDown] URL đã mở: {url}"); // Ghi log URL đã mở
                                        }
                                    }
                                    catch (Exception ex) // Xử lý lỗi nếu không mở được URL
                                    {
                                        Debug.WriteLine($"[DEBUG MouseDown] Lỗi mở URL: {ex.Message}"); // Ghi log lỗi
                                        MessageBox.Show($"Không thể mở URL: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); // Hiển thị hộp thoại lỗi
                                    }
                                    break; // Thoát vòng lặp vì đã xử lý nhấp chuột vào URL
                                }
                                else // Nếu nhấp chuột không trúng URL này
                                {
                                    Debug.WriteLine($"[DEBUG MouseDown]Bấm vào URL bị hụt: {url}, Rect: {urlRect}"); // Ghi log
                                }

                                currentY += sizeUrl.Height; // Cập nhật vị trí Y cho phần tử tiếp theo
                                currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí hiện tại trong chuỗi
                            }
                        }
                    }
                    else // Nếu mục không phải ChatMessage hoặc không có URL
                    {
                        Debug.WriteLine($"[DEBUG MouseDown]Không có tin nhắn hoặc URL hợp lệ tại chỉ mục: {index}"); // Ghi log
                    }
                }
                else // Nếu không tìm thấy mục tại vị trí nhấp
                {
                    Debug.WriteLine($"[DEBUG MouseDown] Không có mục nào ở vị trí nhấp chuột, Index: {index}"); // Ghi log
                }
            }
        }

        // Xử lý sự kiện Load của form ChatForm
        private async void ChatForm_Load(object sender, EventArgs e)
        {
            lblMyStatus.Text = $"Tôi: {myName} (Đang khởi động...)"; // Cập nhật trạng thái
            await StartNetworkServicesAsync(); // Bắt đầu các dịch vụ mạng (TCP Listener, UDP Client)
            lblMyStatus.Text = $"Tôi: {myName} (Online)"; // Cập nhật trạng thái sau khi khởi động mạng
            _ = DiscoverUsersAsync(ctsNetwork.Token); // Bắt đầu quá trình khám phá người dùng (không chờ kết quả)
            ReceiveMessages(); // Bắt đầu lắng nghe nhận tin nhắn từ các kết nối TCP
            cleanupTimer.Start(); // Bắt đầu timer dọn dẹp lịch sử
            CleanupOldChatHistory(); // Thực hiện dọn dẹp lịch sử ngay khi khởi động
        }

        // Xử lý sự kiện FormClosing của form ChatForm
        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Kiểm tra lý do đóng form
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // Nếu người dùng nhấn nút X (đóng cửa sổ)
                e.Cancel = true; // Ngăn chặn việc đóng form
                this.Hide();     // Ẩn form đi
                if (this.notifyIcon != null)
                {
                    this.notifyIcon.Visible = true; // Hiển thị icon ở khay hệ thống
                }
            }
            else
            {
                // Nếu lý do đóng form không phải do người dùng nhấn X (ví dụ: thoát từ tray icon hoặc tắt máy)
                // Thì dừng các dịch vụ mạng và cho phép form đóng
                StopNetworkServices(); // Dừng các dịch vụ mạng
                cleanupTimer.Stop(); // Dừng timer dọn dẹp
                clockTimer.Stop(); // Dừng timer đồng hồ
                rainbowTimer.Stop(); // Dừng Timer cầu vồng
                rainbowTimer.Dispose(); // Giải phóng Timer
                if (this.notifyIcon != null)
                {
                    this.notifyIcon.Dispose(); // Giải phóng NotifyIcon khi thoát hẳn ứng dụng
                }
            }
        }

        #region NotifyIcon Events
        // Xử lý sự kiện click menu item "Mở" từ tray icon
        private void OpenMenuItem_Click(object sender, EventArgs e)
        {
            // Hiển thị form trở lại
            this.Show();
            this.WindowState = FormWindowState.Normal; // Đảm bảo form không ở trạng thái minimized
            this.BringToFront(); // Đưa form lên trên cùng
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Visible = false; // Ẩn icon ở khay hệ thống khi form hiển thị
            }
        }

        // Xử lý sự kiện click menu item "Thoát" từ tray icon
        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            // Khi click "Thoát" từ tray icon, đóng ứng dụng hoàn toàn
            // Gỡ bỏ sự kiện FormClosing tạm thời để tránh bị chặn bởi logic ẩn form
            this.FormClosing -= ChatForm_FormClosing;
            Application.Exit(); // Thoát ứng dụng
        }

        // Xử lý sự kiện double click vào tray icon
        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // Khi double click vào tray icon, hiển thị form trở lại (tương tự như OpenMenuItem_Click)
            this.Show();
            this.WindowState = FormWindowState.Normal; // Đảm bảo form không ở trạng thái minimized
            this.BringToFront(); // Đưa form lên trên cùng
                                 // Tùy chọn: kích hoạt cửa sổ để đưa nó lên tiêu điểm
            this.Activate();

            if (this.notifyIcon != null)
            {
                this.notifyIcon.Visible = false; // Ẩn icon ở khay hệ thống khi form hiển thị
            }
        }

        // Xử lý sự kiện click vào thông báo (balloon tip) của tray icon
        private void notifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            // Khi click vào thông báo, hiển thị form trở lại
            this.Show();
            this.WindowState = FormWindowState.Normal; // Đảm bảo form không ở trạng thái minimized
            this.BringToFront(); // Đưa form lên trên cùng
                                 // Tùy chọn: kích hoạt cửa sổ để đưa nó lên tiêu điểm
            this.Activate();

            if (this.notifyIcon != null)
            {
                this.notifyIcon.Visible = false; // Ẩn icon ở khay hệ thống khi form hiển thị
            }
        }
        #endregion

        // Phương thức bất đồng bộ để khởi động các dịch vụ mạng
        private async Task StartNetworkServicesAsync()
        {
            try
            {
                // Khởi tạo và bắt đầu TCP Listener để lắng nghe kết nối đến trên cổng TcpPort
                tcpListener = new TcpListener(IPAddress.Any, TcpPort);
                tcpListener.Start();
                LogMessage("TCP Listener đang chạy...");
                // Bắt đầu tác vụ chấp nhận kết nối TCP không đồng bộ (không chờ)
                _ = AcceptTcpConnectionsAsync(ctsNetwork.Token);

                // Khởi tạo UDP Client trên cổng MulticastPort
                udpClient = new UdpClient(MulticastPort);
                // Tham gia vào nhóm multicast
                udpClient.JoinMulticastGroup(IPAddress.Parse(MulticastAddress));
                udpClient.EnableBroadcast = false; // Vô hiệu hóa broadcast thông thường
                LogMessage($"Máy khách UDP đang chạy và tham gia nhóm đa hướng {MulticastAddress}:{MulticastPort}...");
                // Bắt đầu tác vụ lắng nghe gói tin UDP multicast không đồng bộ (không chờ)
                _ = ListenForUdpBroadcastsAsync(ctsNetwork.Token);

                // Kiểm tra và tạo thư mục lưu lịch sử chat nếu chưa tồn tại
                if (!Directory.Exists(ChatHistoryDirectory))
                {
                    Directory.CreateDirectory(ChatHistoryDirectory);
                    LogMessage($"Đã tạo thư mục lịch sử chat: {ChatHistoryDirectory}");
                }

                // Đánh dấu tác vụ đã hoàn thành
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // Ghi log nếu có lỗi khi khởi động dịch vụ mạng
                LogMessage($"Lỗi khởi động dịch vụ mạng: {ex.Message}");
            }
        }

        // Phương thức dừng các dịch vụ mạng
        private void StopNetworkServices()
        {
            // Yêu cầu hủy bỏ các tác vụ mạng đang chạy
            ctsNetwork.Cancel();

            // Dừng TCP Listener nếu đang chạy
            if (tcpListener != null)
            {
                tcpListener.Stop();
                tcpListener = null;
                LogMessage("TCP Listener đã dừng.");
            }

            // Dừng UDP Client nếu đang chạy
            if (udpClient != null)
            {
                try
                {
                    // Rời khỏi nhóm multicast
                    udpClient.DropMulticastGroup(IPAddress.Parse(MulticastAddress));
                    LogMessage($"Đã rời nhóm multicast {MulticastAddress}");
                }
                catch (Exception ex)
                {
                    LogMessage($"Lỗi khi rời nhóm multicast: {ex.Message}");
                }
                udpClient.Close(); // Đóng UDP Client
                udpClient = null;
                LogMessage("UDP Client đã dừng.");
            }

            // Đóng tất cả các kết nối TCP đang hoạt động
            foreach (var connection in activeConnections.ToList())
            {
                try { connection.Close(); } catch { } // Đóng kết nối và bỏ qua lỗi nếu có
            }
            activeConnections.Clear(); // Xóa danh sách kết nối
            connectionUserMap.Clear(); // Xóa ánh xạ kết nối-người dùng

            // Xóa danh sách người dùng online và chỉ giữ lại tên của mình
            onlineUsers.Clear();
            onlineUsers.Add(myName);
            UpdateOnlineUsersList(); // Cập nhật hiển thị danh sách

            // Đóng tất cả các cửa sổ chat riêng
            foreach (var chatForm in privateChatWindows.Values.ToList())
            {
                try { chatForm.Close(); } catch { } // Đóng form và bỏ qua lỗi nếu có
            }
            privateChatWindows.Clear(); // Xóa dictionary cửa sổ chat riêng

            LogMessage("Dịch vụ mạng đã dừng."); // Ghi log
        }

        // Phương thức bất đồng bộ để chấp nhận các kết nối TCP đến
        private async Task AcceptTcpConnectionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Vòng lặp chạy cho đến khi có yêu cầu hủy bỏ hoặc tcpListener bị null
                while (!cancellationToken.IsCancellationRequested && tcpListener != null)
                {
                    // Chấp nhận một kết nối TCP đến không đồng bộ
                    TcpClient client = await tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    // Xử lý kết nối TCP mới nhận được
                    HandleNewTcpConnection(client);
                    // Bắt đầu tác vụ xử lý dữ liệu từ client này không đồng bộ (không chờ)
                    _ = HandleTcpClientDataAsync(client, cancellationToken);
                }
            }
            catch (ObjectDisposedException)
            {
                // Xử lý ngoại lệ khi tcpListener đã bị giải phóng
                LogMessage("TCP Listener đã bị Dispose.");
            }
            catch (Exception ex)
            {
                // Ghi log nếu có lỗi khác khi chấp nhận kết nối
                LogMessage($"Lỗi chấp nhận kết nối TCP: {ex.Message}");
            }
        }

        // Phương thức xử lý một kết nối TCP mới
        private void HandleNewTcpConnection(TcpClient newClient)
        {
            // Kiểm tra kết nối mới có hợp lệ không
            if (newClient == null || newClient.Client == null)
            {
                LogMessage("Kết nối mới không hợp lệ.");
                return;
            }

            // Lấy điểm cuối từ xa (IP và Port) của kết nối mới
            var remoteEndPoint = newClient.Client.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint != null)
            {
                // Kiểm tra xem đã có kết nối nào từ cùng một địa chỉ IP và Port chưa
                bool isDuplicate = activeConnections.Any(c =>
                {
                    try
                    {
                        var ep = c.Client.RemoteEndPoint as IPEndPoint;
                        return ep != null && ep.Address.Equals(remoteEndPoint.Address) && ep.Port == remoteEndPoint.Port;
                    }
                    catch { return false; } // Bỏ qua lỗi khi truy cập RemoteEndPoint
                });

                if (isDuplicate)
                {
                    // Nếu là kết nối trùng lặp, ghi log và đóng kết nối mới
                    LogMessage($"Đã có kết nối đến {remoteEndPoint.Address}:{remoteEndPoint.Port}. Đóng kết nối mới.");
                    try { newClient.Close(); } catch { }
                    return;
                }
            }

            // Đồng bộ hóa truy cập vào danh sách activeConnections
            lock (activeConnections)
            {
                activeConnections.Add(newClient); // Thêm kết nối mới vào danh sách
                // Gán tên người dùng ban đầu cho kết nối mới (sử dụng thông tin điểm cuối)
                connectionUserMap[newClient] = newClient.Client.RemoteEndPoint.ToString();
            }

            LogMessage($"Người dùng mới đã kết nối từ {newClient.Client.RemoteEndPoint}"); // Ghi log
            SendPresenceInfo(newClient); // Gửi thông tin hiện diện của mình cho client mới này
        }

        // Phương thức bất đồng bộ để gửi thông tin hiện diện (tên người dùng, IP, Port) đến một client cụ thể
        private async void SendPresenceInfo(TcpClient client)
        {
            // Kiểm tra client có hợp lệ không
            if (client == null || client.Client == null)
            {
                LogMessage("Không thể gửi thông tin hiện diện: TcpClient không hợp lệ.");
                return;
            }

            try
            {
                // Kiểm tra kết nối còn hoạt động không
                if (client.Connected)
                {
                    // Tạo nội dung gói tin PRESENCE
                    string presenceMessage = $"PRESENCE:{myName}:{GetLocalIPAddress()}:{TcpPort}";
                    byte[] buffer = Encoding.UTF8.GetBytes(presenceMessage);
                    NetworkStream stream = client.GetStream(); // Lấy luồng mạng
                    await stream.WriteAsync(buffer, 0, buffer.Length); // Gửi dữ liệu không đồng bộ
                    await stream.FlushAsync(); // Đảm bảo dữ liệu được gửi đi
                    LogMessage($"Đã gửi thông tin hiện diện đến {client.Client.RemoteEndPoint}"); // Ghi log
                }
                else
                {
                    LogMessage("Không thể gửi thông tin hiện diện: Kết nối đã đóng."); // Ghi log nếu kết nối đã đóng
                }
            }
            catch (ObjectDisposedException)
            {
                // Xử lý ngoại lệ khi TcpClient đã bị giải phóng
                LogMessage("TcpClient đã bị giải phóng khi gửi thông tin hiện diện.");
                HandleConnectionDisconnected(client); // Xử lý ngắt kết nối
            }
            catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionReset)
            {
                // Xử lý ngoại lệ khi kết nối bị ngắt đột ngột
                LogMessage($"Lỗi gửi thông tin hiện diện: {ioEx.Message}");
                HandleConnectionDisconnected(client); // Xử lý ngắt kết nối
            }
            catch (Exception ex)
            {
                // Xử lý các ngoại lệ khác
                LogMessage($"Lỗi không xác định khi gửi thông tin hiện diện: {ex.Message}");
                HandleConnectionDisconnected(client); // Xử lý ngắt kết nối
            }
        }

        // Phương thức bất đồng bộ để xử lý dữ liệu nhận được từ một client TCP
        private async Task HandleTcpClientDataAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            NetworkStream clientStream = tcpClient.GetStream(); // Lấy luồng mạng của client
            byte[] buffer = new byte[4096]; // Buffer để nhận dữ liệu

            try
            {
                int bytesRead;
                // Vòng lặp đọc dữ liệu cho đến khi kết nối đóng hoặc có yêu cầu hủy bỏ
                while (tcpClient.Connected && (bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    // Chuyển dữ liệu nhận được từ byte sang chuỗi UTF8
                    string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    // Xử lý dữ liệu nhận được
                    ProcessReceivedData(tcpClient, receivedData);
                }
            }
            catch (OperationCanceledException)
            {
                // Xử lý ngoại lệ khi thao tác nhận bị hủy bỏ
                LogMessage($"Hoạt động nhận đã bị hủy vì {tcpClient.Client.RemoteEndPoint}.");
            }
            catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionReset)
            {
                // Xử lý ngoại lệ khi kết nối bị ngắt đột ngột bởi client
                LogMessage($"{tcpClient.Client.RemoteEndPoint} đã ngắt kết nối đột ngột.");
            }
            catch (Exception ex)
            {
                // Xử lý các ngoại lệ khác khi xử lý dữ liệu
                LogMessage($"Lỗi xử lý dữ liệu từ {tcpClient.Client.RemoteEndPoint}: {ex.Message}");
            }
            finally
            {
                // Luôn gọi phương thức xử lý ngắt kết nối khi vòng lặp kết thúc (do lỗi hoặc client ngắt kết nối)
                HandleConnectionDisconnected(tcpClient);
            }
        }

        // Phương thức lắng nghe nhận tin nhắn từ tất cả các kết nối TCP đang hoạt động
        private void ReceiveMessages()
        {
            // Chạy tác vụ này trên một luồng riêng
            Task.Run(async () =>
            {
                // Vòng lặp chạy cho đến khi có yêu cầu hủy bỏ
                while (!ctsNetwork.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Duyệt qua tất cả các client đang hoạt động (sử dụng ToList() để tránh lỗi khi danh sách thay đổi)
                        foreach (var client in activeConnections.ToList())
                        {
                            // Kiểm tra kết nối còn hoạt động và có dữ liệu sẵn sàng để đọc không
                            if (client.Connected && client.GetStream().DataAvailable)
                            {
                                byte[] buffer = new byte[4096]; // Buffer để nhận dữ liệu
                                // Đọc dữ liệu từ luồng không đồng bộ
                                int bytesRead = await client.GetStream().ReadAsync(buffer, 0, buffer.Length);
                                // Nếu có dữ liệu được đọc
                                if (bytesRead > 0)
                                {
                                    // Chuyển dữ liệu sang chuỗi UTF8
                                    string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                    // Xử lý dữ liệu nhận được
                                    ProcessReceivedData(client, receivedData);
                                }
                            }
                        }
                        // Chờ một khoảng thời gian ngắn trước khi kiểm tra lại
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        // Ghi log nếu có lỗi khi nhận tin nhắn (thực hiện trên luồng giao diện chính)
                        this.Invoke((MethodInvoker)(() =>
                            LogMessage($"Lỗi khi nhận tin nhắn: {ex.Message}")));
                    }
                }
            }, ctsNetwork.Token); // Truyền CancellationToken để có thể hủy tác vụ
        }

        // Phương thức xử lý dữ liệu nhận được từ một client TCP (bao gồm PRESENCE, PUBLIC, và tin nhắn riêng)
        private void ProcessReceivedData(TcpClient senderClient, string data)
        {
            LogMessage($"Nhận dữ liệu từ {senderClient.Client.RemoteEndPoint}: {data}"); // Ghi log dữ liệu nhận được

            // Kiểm tra nếu dữ liệu là gói tin PRESENCE
            if (data.StartsWith("PRESENCE:"))
            {
                var parts = data.Split(':');
                // Định dạng gói tin PRESENCE: PRESENCE:TênNgườiDùng:IP:Port
                if (parts.Length == 4)
                {
                    string userName = parts[1]; // Lấy tên người dùng
                    string userIp = parts[2]; // Lấy IP (có thể cần dùng sau này để xác định client)
                    int userPort;

                    // Thử phân tích Port từ chuỗi
                    if (!int.TryParse(parts[3], out userPort))
                    {
                        LogMessage($"Cổng không hợp lệ trong gói tin PRESENCE: {data}");
                        return; // Bỏ qua gói tin không hợp lệ
                    }

                    string oldName = null; // Biến lưu tên cũ của client này
                    bool nameChanged = false; // Cờ báo hiệu tên có thay đổi không

                    // Đồng bộ hóa truy cập vào connectionUserMap để tránh xung đột luồng
                    lock (connectionUserMap)
                    {
                        // Kiểm tra xem client này đã có tên trong map chưa (tức là đã kết nối trước đó)
                        if (connectionUserMap.ContainsKey(senderClient))
                        {
                            oldName = connectionUserMap[senderClient]; // Lấy tên cũ
                            // Cập nhật tên mới cho client này trong map
                            connectionUserMap[senderClient] = userName;
                            // Kiểm tra xem tên có thay đổi không
                            if (oldName != userName)
                            {
                                nameChanged = true; // Đặt cờ báo hiệu tên đã thay đổi
                                LogMessage($"Người dùng {oldName} đã đổi tên thành {userName}."); // Ghi log
                            }
                        }
                        else
                        {
                            // Nếu là client mới (chưa có trong map), thêm vào map với tên mới
                            connectionUserMap[senderClient] = userName;
                            LogMessage($"Đã thêm kết nối mới cho người dùng {userName}."); // Ghi log
                        }
                    }

                    bool listUpdated = false; // Cờ báo hiệu danh sách người dùng online có cần cập nhật giao diện không
                    // Đồng bộ hóa truy cập vào onlineUsers để tránh xung đột luồng
                    lock (onlineUsers)
                    {
                        // Nếu tên đã thay đổi VÀ tên cũ vẫn còn trong danh sách onlineUsers
                        if (nameChanged && onlineUsers.Contains(oldName))
                        {
                            onlineUsers.Remove(oldName); // Xóa tên cũ khỏi danh sách
                            LogMessage($"Đã xóa tên cũ '{oldName}' khỏi danh sách người dùng online."); // Ghi log
                            listUpdated = true; // Đánh dấu cần cập nhật giao diện
                        }

                        // Thêm tên mới vào danh sách onlineUsers nếu nó chưa có VÀ không phải là tên của mình
                        // (Trường hợp người dùng mới online hoặc đổi tên thành tên chưa có trong danh sách)
                        if (!onlineUsers.Contains(userName) && userName != myName)
                        {
                            onlineUsers.Add(userName); // Thêm tên mới
                            LogMessage($"Đã thêm tên '{userName}' vào danh sách người dùng online."); // Ghi log
                            listUpdated = true; // Đánh dấu cần cập nhật giao diện
                        }
                        // Xử lý trường hợp đặc biệt: Tên không đổi nhưng người dùng vừa được thêm vào connectionUserMap (là kết nối mới)
                        // và tên của họ chưa có trong onlineUsers (đảm bảo người dùng mới luôn được thêm vào danh sách)
                        else if (!nameChanged && !onlineUsers.Contains(userName) && userName != myName)
                        {
                            onlineUsers.Add(userName); // Thêm người dùng mới
                            LogMessage($"Đã thêm người dùng mới '{userName}' vào danh sách người dùng online."); // Ghi log
                            listUpdated = true; // Đánh dấu cần cập nhật giao diện
                        }
                    }

                    // Nếu danh sách onlineUsers đã thay đổi, cập nhật giao diện trên luồng giao diện chính
                    if (listUpdated)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            UpdateOnlineUsersList(); // Gọi phương thức cập nhật hiển thị danh sách
                        });
                    }
                }
                else
                {
                    // Ghi log nếu định dạng gói tin PRESENCE không đúng
                    LogMessage($"Định dạng gói tin PRESENCE không đúng: {data}");
                }
            }
            // Kiểm tra nếu dữ liệu là tin nhắn công cộng
            else if (data.StartsWith("PUBLIC|"))
            {
                // Loại bỏ \u001E và các ký tự điều khiển trước khi phân tách
                string cleanedData = data.TrimEnd('\u001E', '\r', '\n');
                var parts = cleanedData.Split('|');
                if (parts.Length >= 3)
                {
                    string senderName = parts[1];
                    // Nối các phần còn lại, đảm bảo không thêm ký tự điều khiển
                    string messageContent = string.Join("|", parts.Skip(2)).Trim();
                    Debug.WriteLine($"[DEBUG HandleClientConnectionAsync] Nội dung tin nhắn nhận được: '{messageContent}'");
                    var receivedMessage = new ChatMessage
                    {
                        SenderName = senderName,
                        Content = messageContent,
                        Timestamp = DateTime.Now,
                        IsSentByMe = false
                    };
                    this.Invoke((MethodInvoker)(() => LogChatMessage(receivedMessage)));
                }
                else
                {
                    LogMessage($"Định dạng gói tin PUBLIC không đúng: {cleanedData}");
                }
            }
            // Nếu không phải PRESENCE hoặc PUBLIC, giả định là tin nhắn riêng hoặc dữ liệu không xác định
            else
            {
                // Xử lý tin nhắn riêng (hoặc dữ liệu không xác định)
                // Giả định đây là định dạng tin nhắn riêng: TênNgườiGửi|NộiDungTinNhắn
                var parts = data.Split('|');
                if (parts.Length >= 2)
                {
                    string senderName = parts[0]; // Trong tin nhắn riêng, tên người gửi là phần đầu tiên
                    string messageContent = string.Join("|", parts.Skip(1)); // Lấy nội dung tin nhắn

                    // Tạo đối tượng ChatMessage cho tin nhắn nhận được
                    var receivedMessage = new ChatMessage
                    {
                        SenderName = senderName,
                        Content = messageContent,
                        Timestamp = DateTime.Now,
                        IsSentByMe = false // Đây là tin nhắn nhận được từ người khác
                    };

                    // Xử lý trên luồng giao diện chính
                    this.Invoke((MethodInvoker)(() =>
                    {
                        // Mở cửa sổ chat riêng nếu chưa tồn tại
                        if (!privateChatWindows.ContainsKey(senderName))
                        {
                            // Tìm TcpClient tương ứng với senderName (dựa vào connectionUserMap)
                            TcpClient client = activeConnections.FirstOrDefault(c => connectionUserMap.ContainsKey(c) && connectionUserMap[c] == senderName);

                            if (client != null)
                            {
                                // Tạo cửa sổ chat riêng mới
                                PrivateChatForm privateChat = new PrivateChatForm(senderName, client, myName);
                                privateChatWindows[senderName] = privateChat; // Lưu trữ cửa sổ chat riêng vào dictionary
                                // Gán sự kiện FormClosed để xóa form khỏi dictionary khi đóng
                                privateChat.FormClosed += (s, args) => privateChatWindows.Remove(senderName);
                                privateChat.Show(); // Hiển thị cửa sổ chat riêng
                                LogMessage($"Đã mở cửa sổ chat riêng với {senderName}."); // Ghi log
                            }
                            else
                            {
                                // Ghi log nếu không tìm thấy kết nối
                                LogMessage($"Không tìm thấy kết nối cho {senderName} để mở chat riêng.");
                                // Có thể thông báo cho người dùng rằng không thể mở chat riêng
                                MessageBox.Show($"Không tìm thấy người dùng {senderName} hoặc họ đã offline.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }

                        // Thêm tin nhắn vào cửa sổ chat riêng tương ứng
                        if (privateChatWindows.ContainsKey(senderName))
                        {
                            privateChatWindows[senderName].AddMessage(receivedMessage); // Thêm tin nhắn vào cửa sổ chat
                            privateChatWindows[senderName].BringToFront(); // Đưa cửa sổ chat lên trước
                            privateChatWindows[senderName].SaveMessageToFile(receivedMessage); // Lưu tin nhắn vào tệp lịch sử

                            // --- THÊM ĐOẠN NÀY ĐỂ HIỂN THỊ THÔNG BÁO ---
                            // Kiểm tra nếu form chính hoặc cửa sổ chat riêng đang ẩn VÀ notifyIcon đã được khởi tạo
                            if ((!this.Visible || !privateChatWindows[senderName].Visible) && this.notifyIcon != null)
                            {
                                // Hiển thị thông báo trên khay hệ thống
                                this.notifyIcon.ShowBalloonTip(
                                   5000, // Thời gian hiển thị (ms)
                                   $"Tin nhắn mới từ {senderName}", // Tiêu đề thông báo
                                   receivedMessage.Content, // Nội dung thông báo
                                   ToolTipIcon.Info // Biểu tượng thông báo
                               );
                            }
                            // ----------------------------------------
                        }
                    }));
                }
                else
                {
                    // Ghi log nếu dữ liệu không xác định hoặc định dạng tin nhắn riêng không đúng
                    LogMessage($"Dữ liệu không xác định hoặc định dạng tin nhắn riêng không đúng từ {senderClient.Client.RemoteEndPoint}: {data}");
                    // Có thể thêm xử lý cho các loại dữ liệu khác nếu có
                }
            }
        }

        // Phương thức xử lý khi một kết nối TCP bị ngắt
        private void HandleConnectionDisconnected(TcpClient disconnectedClient)
        {
            // Thực hiện trên luồng giao diện chính
            this.Invoke((MethodInvoker)delegate
            {
                // Đồng bộ hóa truy cập vào danh sách activeConnections
                lock (activeConnections)
                {
                    // Kiểm tra xem client bị ngắt kết nối có trong danh sách đang hoạt động không
                    if (activeConnections.Contains(disconnectedClient))
                    {
                        // Lấy tên người dùng tương ứng với client (nếu có)
                        string userName = connectionUserMap.ContainsKey(disconnectedClient) ? connectionUserMap[disconnectedClient] : "Unknown";
                        LogMessage($"Kết nối đến {disconnectedClient.Client.RemoteEndPoint} ({userName}) đã đóng."); // Ghi log

                        activeConnections.Remove(disconnectedClient); // Xóa client khỏi danh sách kết nối hoạt động
                        connectionUserMap.Remove(disconnectedClient); // Xóa client khỏi ánh xạ kết nối-người dùng

                        try { disconnectedClient.Close(); } catch { } // Đóng kết nối và bỏ qua lỗi nếu có

                        // Kiểm tra xem người dùng này còn kết nối nào khác không
                        // Nếu không còn kết nối nào với tên người dùng này VÀ đó không phải là tên của mình
                        if (!connectionUserMap.Values.Any(name => name == userName) && userName != myName)
                        {
                            onlineUsers.Remove(userName); // Xóa người dùng khỏi danh sách online
                            UpdateOnlineUsersList(); // Cập nhật hiển thị danh sách
                            LogMessage($"Đã xóa {userName} khỏi danh sách người dùng online."); // Ghi log
                        }
                    }
                }
            });
        }

        // Phương thức cập nhật hiển thị danh sách người dùng online trên giao diện
        private void UpdateOnlineUsersList()
        {
            // Thực hiện trên luồng giao diện chính
            this.Invoke((MethodInvoker)delegate
            {
                lbOnlineUsers.Items.Clear(); // Xóa tất cả các mục hiện có trong ListBox

                // Sắp xếp danh sách người dùng online theo tên
                var sortedUsers = onlineUsers.OrderBy(name => name).ToList();

                // Thêm từng người dùng vào ListBox
                foreach (var userName in sortedUsers)
                {
                    // Hiển thị "(Tôi)" bên cạnh tên của mình
                    lbOnlineUsers.Items.Add(userName == myName ? $"{userName} (Tôi)" : userName);
                }

                // Ghi log danh sách người dùng online hiện tại
                LogMessage($"Danh sách người dùng online: {string.Join(", ", sortedUsers)}");
            });
        }

        // Xử lý sự kiện double click chuột trên ListBox người dùng online
        private async void LbOnlineUsers_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // Lấy chỉ mục của mục được double click
            int index = lbOnlineUsers.IndexFromPoint(e.Location);

            // Nếu double click vào một mục hợp lệ
            if (index != ListBox.NoMatches)
            {
                // Lấy tên người dùng được chọn
                string selectedUser = lbOnlineUsers.Items[index].ToString();

                // Loại bỏ "(Tôi)" nếu có
                if (selectedUser.EndsWith(" (Tôi)"))
                {
                    selectedUser = selectedUser.Substring(0, selectedUser.Length - " (Tôi)".Length);
                }

                // Kiểm tra nếu người dùng double click vào tên của chính mình
                if (selectedUser == myName)
                {
                    MessageBox.Show("Bạn không thể chat riêng với chính mình.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Mở cửa sổ chat riêng với người dùng được chọn
                    await OpenPrivateChat(selectedUser);
                }
            }
        }

        // Phương thức bất đồng bộ để mở cửa sổ chat riêng với một người dùng
        private async Task OpenPrivateChat(string userName)
        {
            // Kiểm tra xem cửa sổ chat riêng với người dùng này đã tồn tại chưa
            if (!privateChatWindows.ContainsKey(userName))
            {
                // Nếu chưa tồn tại, tìm TcpClient tương ứng với tên người dùng
                TcpClient client = await GetTcpClientByUsernameAsync(userName);

                // Nếu tìm thấy client
                if (client != null)
                {
                    // Tạo cửa sổ chat riêng mới
                    PrivateChatForm privateChat = new PrivateChatForm(userName, client, myName);
                    privateChatWindows[userName] = privateChat; // Lưu trữ cửa sổ vào dictionary
                    // Gán sự kiện FormClosed để xóa cửa sổ khỏi dictionary khi đóng
                    privateChat.FormClosed += (s, args) => privateChatWindows.Remove(userName);
                    privateChat.Show(); // Hiển thị cửa sổ chat riêng
                }
                else
                {
                    // Thông báo lỗi nếu không tìm thấy kết nối
                    MessageBox.Show($"Không tìm thấy kết nối với {userName}.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // Nếu cửa sổ chat riêng đã tồn tại, đưa nó lên trước
                privateChatWindows[userName].BringToFront();
            }
        }

        // Phương thức bất đồng bộ để lấy TcpClient tương ứng với một tên người dùng
        private async Task<TcpClient> GetTcpClientByUsernameAsync(string username)
        {
            // Chạy tác vụ này trên một luồng riêng để tránh chặn luồng giao diện
            return await Task.Run(() =>
            {
                // Đồng bộ hóa truy cập vào connectionUserMap
                lock (connectionUserMap)
                {
                    // Tìm cặp KeyValuePair (TcpClient, string) trong connectionUserMap có giá trị (tên người dùng) khớp
                    var pair = connectionUserMap.FirstOrDefault(kv => kv.Value == username);

                    // Kiểm tra xem có tìm thấy cặp nào không VÀ client đó còn trong danh sách kết nối hoạt động không
                    if (pair.Key != null && activeConnections.Contains(pair.Key))
                    {
                        return pair.Key; // Trả về TcpClient tìm được
                    }
                }
                return null; // Trả về null nếu không tìm thấy
            });
        }

        // Xử lý sự kiện KeyDown trên ô nhập tin nhắn
        private void TxtMessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                SendPublicMessage();
            }
            else if (e.KeyCode == Keys.Enter && e.Shift)
            {
                int caretPosition = txtMessageInput.SelectionStart;
                txtMessageInput.Text = txtMessageInput.Text.Insert(caretPosition, Environment.NewLine);
                txtMessageInput.SelectionStart = caretPosition + Environment.NewLine.Length;
                txtMessageInput.SelectionLength = 0; // Đảm bảo không chọn văn bản
                txtMessageInput.ScrollToCaret(); // Cuộn đến vị trí con trỏ
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // Phương thức bất đồng bộ để gửi tin nhắn công cộng
        // Phương thức gửi tin nhắn công cộng (bất đồng bộ)
        private async void SendPublicMessage()
        {
            // Lấy nội dung tin nhắn từ TextBox và loại bỏ khoảng trắng ở đầu và cuối
            string messageContent = txtMessageInput.Text.Trim();
            // Kiểm tra xem nội dung tin nhắn có rỗng hoặc chỉ chứa khoảng trắng không
            if (!string.IsNullOrWhiteSpace(messageContent))
            {
                // Tạo một đối tượng ChatMessage cho tin nhắn công cộng
                var publicMessage = new ChatMessage
                {
                    SenderName = myName, // Tên người gửi (tôi)
                    Content = messageContent, // Nội dung tin nhắn
                    Timestamp = DateTime.Now, // Dấu thời gian hiện tại
                    IsSentByMe = true // Đánh dấu đây là tin nhắn do tôi gửi
                };
                LogChatMessage(publicMessage); // Ghi log hoặc hiển thị tin nhắn trên giao diện (tùy thuộc vào cài đặt hàm LogChatMessage)
                                               // Lặp qua tất cả các kết nối client đang hoạt động
                foreach (var client in activeConnections.ToList()) // Sử dụng ToList() để tránh lỗi nếu danh sách thay đổi trong lúc lặp
                {
                    // Kiểm tra xem client có đang kết nối không
                    if (client.Connected)
                    {
                        try
                        {
                            // Định dạng tin nhắn để gửi đi, bao gồm loại tin nhắn ("PUBLIC"), tên người gửi và nội dung, kết thúc bằng ký tự phân cách record (\u001E)
                            string formattedMessage = $"PUBLIC|{myName}|{messageContent}\u001E";
                            // Chuyển đổi chuỗi tin nhắn thành mảng byte sử dụng mã hóa UTF8
                            byte[] buffer = Encoding.UTF8.GetBytes(formattedMessage);
                            NetworkStream stream = client.GetStream(); // Lấy luồng mạng của client
                            await stream.WriteAsync(buffer, 0, buffer.Length); // Ghi mảng byte vào luồng mạng một cách bất đồng bộ
                            await stream.FlushAsync(); // Xả bộ đệm của luồng mạng để đảm bảo dữ liệu được gửi đi ngay lập tức
                            Debug.WriteLine($"[DEBUG SendPublicMessage] Gửi tin nhắn: '{formattedMessage}'"); // Ghi log debug tin nhắn đã gửi
                        }
                        catch (Exception ex) // Bắt các ngoại lệ có thể xảy ra trong quá trình gửi
                        {
                            LogMessage($"Lỗi gửi tin nhắn công cộng: {ex.Message}"); // Ghi log lỗi (sử dụng hàm LogMessage khác)
                        }
                    }
                }
                txtMessageInput.Clear(); // Xóa nội dung trong TextBox nhập tin nhắn sau khi gửi
            }
        }

        // Phương thức gửi tin nhắn riêng đến một người dùng cụ thể
        public async void SendMessage(string recipientUserName, string messageContent)
        {
            // Kiểm tra nội dung tin nhắn có rỗng không hoặc người nhận có phải là chính mình không
            if (string.IsNullOrWhiteSpace(messageContent) || recipientUserName == myName)
                return; // Thoát nếu không hợp lệ

            // Tìm TcpClient tương ứng với tên người nhận
            TcpClient client = await GetTcpClientByUsernameAsync(recipientUserName);

            // Nếu không tìm thấy client
            if (client == null)
            {
                LogMessage($"Không tìm thấy kết nối để gửi tin nhắn đến {recipientUserName}."); // Ghi log
                return; // Thoát
            }

            // Nếu tìm thấy client và kết nối còn hoạt động
            if (client.Connected)
            {
                try
                {
                    // Định dạng tin nhắn riêng để gửi qua mạng: TênNgườiGửi|NộiDung
                    string formattedMessage = $"{myName}|{messageContent}";
                    byte[] buffer = Encoding.UTF8.GetBytes(formattedMessage); // Chuyển sang mảng byte
                    NetworkStream stream = client.GetStream(); // Lấy luồng mạng
                    await stream.WriteAsync(buffer, 0, buffer.Length); // Gửi dữ liệu không đồng bộ
                    await stream.FlushAsync(); // Đảm bảo dữ liệu được gửi đi

                    LogMessage($"Đã gửi tin nhắn riêng đến {recipientUserName}: {messageContent}"); // Ghi log

                    // Nếu cửa sổ chat riêng với người nhận đang mở
                    if (privateChatWindows.ContainsKey(recipientUserName))
                    {
                        // Tạo đối tượng ChatMessage cho tin nhắn đã gửi
                        var sentMessage = new ChatMessage
                        {
                            SenderName = myName, // Tên người gửi là tên của mình
                            Content = messageContent, // Nội dung
                            Timestamp = DateTime.Now, // Thời gian
                            IsSentByMe = true // Đánh dấu là tin nhắn đã gửi
                        };
                        privateChatWindows[recipientUserName].AddMessage(sentMessage); // Thêm tin nhắn vào cửa sổ chat riêng
                        privateChatWindows[recipientUserName].SaveMessageToFile(sentMessage); // Lưu tin nhắn vào tệp lịch sử
                    }
                }
                catch (Exception ex)
                {
                    // Ghi log nếu có lỗi khi gửi tin nhắn riêng
                    LogMessage($"Lỗi gửi tin nhắn riêng đến {recipientUserName}: {ex.Message}");
                }
            }
            else
            {
                // Ghi log nếu kết nối không khả dụng
                LogMessage($"Không thể gửi tin nhắn riêng đến {recipientUserName}: Kết nối không khả dụng.");
            }
        }

        // Phương thức bất đồng bộ để khám phá các người dùng khác trong mạng bằng multicast VÀ unicast
        private async Task DiscoverUsersAsync(CancellationToken cancellationToken)
        {
            // Vòng lặp chạy cho đến khi có yêu cầu hủy bỏ
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Tạo gói tin khám phá: DISCOVER:TênNgườiDùng:IP:Port
                    string discoveryMessage = $"DISCOVER:{myName}:{GetLocalIPAddress()}:{TcpPort}";
                    byte[] buffer = Encoding.UTF8.GetBytes(discoveryMessage); // Chuyển sang mảng byte

                    // --- BẮT ĐẦU: Gửi gói tin DISCOVER qua Multicast ---
                    try
                    {
                        if (udpClient == null)
                        {
                            LogMessage("Lỗi: udpClient chưa được khởi tạo.");
                            return;
                        }

                        // Tạo điểm cuối cho nhóm multicast
                        IPEndPoint multicastEndpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), MulticastPort);
                        // Gửi gói tin qua UDP đến nhóm multicast
                        await udpClient.SendAsync(buffer, buffer.Length, multicastEndpoint);
                        LogMessage($"Đã gửi gói tin DISCOVER tới multicast group: {MulticastAddress}:{MulticastPort}"); // Ghi log
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                    {
                        // Xử lý lỗi khi địa chỉ multicast không khả dụng
                        LogMessage("Lỗi Socket khi gửi multicast: Địa chỉ không khả dụng.");
                        // Tiếp tục vòng lặp, có thể thử lại sau
                    }
                    catch (Exception ex)
                    {
                        // Xử lý các ngoại lệ khác khi gửi gói tin multicast
                        LogMessage($"Lỗi gửi gói tin multicast: {ex.Message}");
                        // Tiếp tục vòng lặp, có thể thử lại sau
                    }
                    // --- KẾT THÚC: Gửi gói tin DISCOVER qua Multicast ---

                    // --- BẮT ĐẦU: Gửi gói tin DISCOVER qua Unicast đến các kết nối hiện có ---

                    if (activeConnections == null)
                    {
                        LogMessage("Lỗi: activeConnections chưa được khởi tạo.");
                        return;
                    }

                    foreach (var client in activeConnections.ToList())
                    {
                        if (client?.Connected == true)
                        {
                            try
                            {
                                // Lấy điểm cuối từ xa của client
                                var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                                if (remoteEndPoint != null)
                                {
                                    // Lấy địa chỉ IP của peer
                                    string peerIp = remoteEndPoint.Address.ToString();

                                    // Bỏ qua việc gửi unicast đến chính mình
                                    if (peerIp == GetLocalIPAddress())
                                    {
                                        continue;
                                    }

                                    // Tạo điểm cuối cho unicast (IP của peer và cổng MulticastPort)
                                    IPEndPoint unicastEndpoint = new IPEndPoint(IPAddress.Parse(peerIp), MulticastPort);

                                    // Gửi gói tin DISCOVER qua UDP unicast đến peer
                                    await udpClient.SendAsync(buffer, buffer.Length, unicastEndpoint);
                                    LogMessage($"Đã gửi gói tin DISCOVER unicast tới {peerIp}:{MulticastPort}"); // Ghi log
                                }
                            }
                            catch (Exception ex)
                            {
                                // Ghi log nếu có lỗi khi gửi unicast
                                LogMessage($"Lỗi gửi gói tin DISCOVER unicast: {ex.Message}");
                                // Tiếp tục với các client khác
                            }
                        }
                    }
                    // --- KẾT THÚC: Gửi gói tin DISCOVER qua Unicast ---

                    // Chờ 5 giây trước khi gửi gói tin khám phá tiếp theo
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Thoát vòng lặp nếu tác vụ bị hủy bỏ
                    LogMessage("Khám phá người dùng đã bị hủy.");
                    break;
                }
                catch (Exception ex)
                {
                    // Xử lý các ngoại lệ khác trong vòng lặp khám phá
                    LogMessage($"Lỗi trong quá trình khám phá người dùng: {ex.Message}");
                    try { await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); } catch { break; } // Chờ 30 giây trước khi thử lại
                }
            }
        }


        // Phương thức bất đồng bộ để lắng nghe các gói tin UDP multicast
        private async Task ListenForUdpBroadcastsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Vòng lặp chạy cho đến khi có yêu cầu hủy bỏ hoặc udpClient bị null
                while (!cancellationToken.IsCancellationRequested && udpClient != null)
                {
                    // Bắt đầu tác vụ nhận gói tin UDP không đồng bộ
                    var receiveTask = udpClient.ReceiveAsync();
                    // Tạo một tác vụ chờ hủy bỏ
                    var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);

                    // Chờ cho một trong hai tác vụ hoàn thành (nhận được gói tin hoặc bị hủy bỏ)
                    var completedTask = await Task.WhenAny(receiveTask, cancellationTask).ConfigureAwait(false);

                    // Nếu tác vụ hủy bỏ hoàn thành trước, ném ngoại lệ OperationCanceledException
                    if (completedTask == cancellationTask)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    // Lấy kết quả từ tác vụ nhận gói tin
                    UdpReceiveResult result = receiveTask.Result;
                    byte[] buffer = result.Buffer; // Dữ liệu nhận được
                    IPEndPoint remoteEndPoint = result.RemoteEndPoint; // Điểm cuối từ xa

                    // Chuyển dữ liệu sang chuỗi UTF8
                    string receivedData = Encoding.UTF8.GetString(buffer);
                    LogMessage($"Nhận gói tin UDP từ {remoteEndPoint}: {receivedData}"); // Ghi log

                    // Xử lý gói tin UDP nhận được
                    ProcessUdpBroadcast(remoteEndPoint, receivedData);
                }
            }
            catch (OperationCanceledException)
            {
                // Xử lý ngoại lệ khi thao tác lắng nghe UDP bị hủy bỏ
                LogMessage("UDP listen operation cancelled.");
            }
            catch (ObjectDisposedException)
            {
                // Xử lý ngoại lệ khi UDP Client đã bị giải phóng
                LogMessage("UDP Client đã bị Dispose.");
            }
            catch (Exception ex)
            {
                // Xử lý các ngoại lệ khác khi lắng nghe gói tin multicast
                LogMessage($"Lỗi lắng nghe gói tin multicast: {ex.Message}");
            }
            finally
            {
                // Đảm bảo rời khỏi nhóm multicast khi kết thúc lắng nghe (nếu udpClient còn tồn tại)
                if (udpClient != null)
                {
                    try
                    {
                        udpClient.DropMulticastGroup(IPAddress.Parse(MulticastAddress));
                        LogMessage($"Đã rời nhóm multicast {MulticastAddress}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Lỗi khi rời nhóm multicast: {ex.Message}");
                    }
                }
            }
        }

        // Phương thức xử lý gói tin UDP broadcast nhận được
        private void ProcessUdpBroadcast(IPEndPoint remoteEndPoint, string data)
        {
            // Thực hiện xử lý trên luồng giao diện chính
            this.Invoke((MethodInvoker)delegate
            {
                // Kiểm tra nếu gói tin là DISCOVER
                if (data.StartsWith("DISCOVER:"))
                {
                    var parts = data.Split(':');
                    // Định dạng gói tin DISCOVER: DISCOVER:TênNgườiDùng:IP:Port
                    if (parts.Length == 4)
                    {
                        string userName = parts[1]; // Lấy tên người dùng
                        string userIp = parts[2]; // Lấy IP
                        int userPort;

                        // Thử phân tích Port
                        if (!int.TryParse(parts[3], out userPort))
                        {
                            LogMessage($"Cổng không hợp lệ trong gói tin DISCOVER: {data}");
                            return; // Bỏ qua gói tin không hợp lệ
                        }

                        // Bỏ qua gói tin DISCOVER của chính mình
                        if (userIp == GetLocalIPAddress() && userPort == TcpPort)
                        {
                            LogMessage($"Bỏ qua gói tin DISCOVER của chính mình: {data}");
                            return;
                        }

                        // Kiểm tra xem đã có kết nối TCP đến IP này chưa
                        if (activeConnections.Any(c => (c.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() == userIp))
                        {
                            LogMessage($"Đã có kết nối đến {userIp}, bỏ qua DISCOVER.");
                            return; // Bỏ qua nếu đã có kết nối
                        }

                        LogMessage($"Nhận DISCOVER từ {userName}, thử kết nối TCP..."); // Ghi log
                        // Thử kết nối TCP đến người dùng này không đồng bộ (không chờ)
                        _ = TryConnectToUser(userIp, userPort, userName);
                    }
                    else
                    {
                        // Ghi log nếu định dạng gói tin DISCOVER không đúng
                        LogMessage($"Định dạng DISCOVER không đúng: {data}");
                    }
                }
                else
                {
                    // Ghi log nếu gói tin UDP không xác định
                    LogMessage($"Gói tin UDP không xác định: {data}");
                }
            });
        }

        // Phương thức bất đồng bộ để thử kết nối TCP đến một người dùng
        private async Task<TcpClient> TryConnectToUser(string ipAddress, int port, string userName)
        {
            // Bỏ qua nếu cố gắng kết nối đến chính mình
            if (ipAddress == GetLocalIPAddress() && port == TcpPort)
            {
                LogMessage($"Bỏ qua kết nối đến chính mình: {ipAddress}:{port}");
                return null;
            }

            TcpClient client = null; // Khởi tạo client là null
            try
            {
                client = new TcpClient(); // Tạo một TcpClient mới
                LogMessage($"Đang kết nối đến {userName} tại {ipAddress}:{port}"); // Ghi log

                // Bắt đầu tác vụ kết nối không đồng bộ
                var connectTask = client.ConnectAsync(ipAddress, port);
                // Chờ cho tác vụ kết nối hoàn thành hoặc hết thời gian chờ (5 giây)
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                {
                    // Nếu hết thời gian chờ
                    LogMessage($"Hết thời gian kết nối đến {userName} tại {ipAddress}:{port}"); // Ghi log
                    client.Close(); // Đóng client
                    return null; // Trả về null
                }

                // Chờ tác vụ kết nối hoàn thành (nếu chưa hoàn thành trong WhenAny)
                await connectTask;

                // Nếu kết nối thành công
                if (client.Connected)
                {
                    HandleNewTcpConnection(client); // Xử lý kết nối mới (thêm vào danh sách, map)
                    SendPresenceInfo(client); // Gửi thông tin hiện diện của mình cho client này
                    LogMessage($"Kết nối TCP thành công đến {userName} tại {ipAddress}:{port}"); // Ghi log
                    return client; // Trả về client đã kết nối
                }
                else
                {
                    // Nếu kết nối không thành công
                    LogMessage($"Không thể kết nối đến {userName} tại {ipAddress}:{port}"); // Ghi log
                    client.Close(); // Đóng client
                    return null; // Trả về null
                }
            }
            catch (Exception ex)
            {
                // Xử lý các ngoại lệ khi kết nối
                LogMessage($"Lỗi kết nối đến {userName} tại {ipAddress}:{port}: {ex.Message}"); // Ghi log lỗi
                if (client != null)
                {
                    try { client.Close(); } catch { } // Đóng client nếu nó đã được tạo
                }
                return null; // Trả về null
            }
        }

        // Phương thức lấy địa chỉ IP cục bộ của máy
        private string GetLocalIPAddress()
        {
            // Lấy thông tin host của máy tính hiện tại
            var host = Dns.GetHostEntry(Dns.GetHostName());
            // Duyệt qua tất cả các địa chỉ IP của host
            foreach (var ip in host.AddressList)
            {
                // Nếu địa chỉ IP là IPv4
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString(); // Trả về địa chỉ IPv4 đầu tiên tìm thấy
                }
            }
            // Nếu không tìm thấy địa chỉ IPv4, sử dụng địa chỉ loopback (localhost)
            LogMessage("Không tìm thấy địa chỉ IPv4, sử dụng localhost.");
            return IPAddress.Loopback.ToString();
        }

        // Phương thức ghi log thông báo (hiện tại không hiển thị trên giao diện chat chính)
        private void LogMessage(string message)
        {
            // Kiểm tra xem có cần Invoke để truy cập điều khiển giao diện không
            if (lbChatMessages.InvokeRequired)
            {
                lbChatMessages.Invoke((MethodInvoker)delegate
                {
                    // Các dòng này bị chú thích, log không hiển thị trong lbChatMessages
                    // lbChatMessages.Items.Add($"[LOG] {message}");
                    // lbChatMessages.TopIndex = lbChatMessages.Items.Count - 1;
                });
            }
            else
            {
                // Các dòng này bị chú thích, log không hiển thị trong lbChatMessages
                // lbChatMessages.Items.Add($"[LOG] {message}");
                // lbChatMessages.TopIndex = lbChatMessages.Items.Count - 1;
            }
            // Có thể ghi log vào Console hoặc một tệp log ở đây nếu cần
            System.Diagnostics.Debug.WriteLine($"[LOG] {message}");
        }

        // Phương thức ghi log tin nhắn chat lên giao diện chính
        public void LogChatMessage(ChatMessage message)
        {
            // Loại bỏ ký tự không mong muốn lần nữa để đảm bảo
            message.Content = message.Content.TrimEnd('\u001E', '\r', '\n');
            Debug.WriteLine($"[DEBUG LogChatMessage] Nội dung tin nhắn trước khi thêm: '{message.Content}'");

            // Tìm kiếm URL trong nội dung tin nhắn
            message.Urls = FindUrlsInText(message.Content);

            if (lbChatMessages.InvokeRequired)
            {
                lbChatMessages.Invoke((MethodInvoker)delegate
                {
                    lbChatMessages.Items.Add(message);
                    if (lbChatMessages.Items.Count > 0)
                    {
                        lbChatMessages.TopIndex = lbChatMessages.Items.Count - 1;
                    }
                    lbChatMessages.Invalidate(); // Làm mới ListBox để vẽ lại

                    // --- THÊM ĐOẠN NÀY ĐỂ HIỂN THỊ THÔNG BÁO ---
                    // Kiểm tra nếu form đang ẩn VÀ notifyIcon đã được khởi tạo
                    if (!this.Visible && this.notifyIcon != null)
                    {
                        // Hiển thị thông báo trên khay hệ thống
                        this.notifyIcon.ShowBalloonTip(
                            5000, // Thời gian hiển thị (ms)
                            "Tin nhắn công cộng mới", // Tiêu đề thông báo
                            $"{message.SenderName}: {message.Content}", // Nội dung thông báo
                            ToolTipIcon.Info // Biểu tượng thông báo (Thông tin)
                        );
                    }
                    // ----------------------------------------
                });
            }
            else
            {
                lbChatMessages.Items.Add(message);
                if (lbChatMessages.Items.Count > 0)
                {
                    lbChatMessages.TopIndex = lbChatMessages.Items.Count - 1;
                }
                lbChatMessages.Invalidate(); // Làm mới ListBox để vẽ lại

                // --- THÊM ĐOẠN NÀY ĐỂ HIỂN THỊ THÔNG BÁO ---
                // Kiểm tra nếu form đang ẩn VÀ notifyIcon đã được khởi tạo
                if (!this.Visible && this.notifyIcon != null)
                {
                    // Hiển thị thông báo trên khay hệ thống
                    this.notifyIcon.ShowBalloonTip(
                        5000, // Thời gian hiển thị (ms)
                        "Tin nhắn công cộng mới", // Tiêu đề thông báo
                        $"{message.SenderName}: {message.Content}", // Nội dung thông báo
                        ToolTipIcon.Info // Biểu tượng thông báo (Thông tin)
                    );
                }
                // ----------------------------------------
            }
            System.Diagnostics.Debug.WriteLine($"[CHAT] {message.SenderName}: {message.Content}");
        }

        // Phương thức thêm tin nhắn vào ListBox hiển thị
        public void AddMessage(ChatMessage message)
        {
            // Kiểm tra xem có cần Invoke để cập nhật giao diện từ một luồng khác không (đảm bảo an toàn luồng khi cập nhật UI Control)
            if (lbChatMessages.InvokeRequired)
            {
                // Nếu cần Invoke, thực hiện thao tác thêm tin nhắn trên luồng giao diện chính
                lbChatMessages.Invoke((MethodInvoker)delegate
                {
                    lbChatMessages.Items.Add(message); // Thêm đối tượng tin nhắn vào danh sách các mục trong ListBox
                                                       // Cuộn ListBox xuống cuối để hiển thị tin nhắn mới nhất
                    if (lbChatMessages.Items.Count > 0) // Kiểm tra xem có mục nào trong ListBox không
                    {
                        lbChatMessages.TopIndex = lbChatMessages.Items.Count - 1; // Đặt mục cuối cùng làm mục đầu tiên hiển thị ở trên cùng (hiệu quả là cuộn xuống cuối)
                    }
                    lbChatMessages.Invalidate(); // Yêu cầu ListBox vẽ lại để hiển thị tin nhắn mới
                });
            }
            else
            {
                // Nếu không cần Invoke (đã ở trên luồng giao diện), thực hiện trực tiếp
                lbChatMessages.Items.Add(message); // Thêm đối tượng tin nhắn vào danh sách các mục trong ListBox
                                                   // Cuộn ListBox xuống cuối để hiển thị tin nhắn mới nhất
                if (lbChatMessages.Items.Count > 0) // Kiểm tra xem có mục nào trong ListBox không
                {
                    lbChatMessages.TopIndex = lbChatMessages.Items.Count - 1; // Đặt mục cuối cùng làm mục đầu tiên hiển thị ở trên cùng (hiệu quả là cuộn xuống cuối)
                }
                lbChatMessages.Invalidate(); // Yêu cầu ListBox vẽ lại để hiển thị tin nhắn mới
            }
            Debug.WriteLine($"[CHAT] {message.SenderName}: {message.Content}"); // Ghi log debug tin nhắn đã thêm
        }

        // Xử lý sự kiện đo kích thước của một mục trong ListBox.
        // Sự kiện này được gọi trước khi một mục được vẽ để xác định chiều cao cần thiết.
        private void LbChatMessages_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            ListBox listBox = sender as ListBox; // Lấy đối tượng ListBox từ sender
            if (listBox == null) return; // Nếu không phải ListBox, thoát khỏi hàm

            // Kiểm tra xem chỉ số mục có hợp lệ không.
            // Nếu không hợp lệ, đặt chiều cao mặc định và thoát.
            if (e.Index < 0 || e.Index >= listBox.Items.Count)
            {
                e.ItemHeight = 20;
                return;
            }

            object item = listBox.Items[e.Index]; // Lấy mục tại chỉ số hiện tại
                                                  // Kiểm tra xem mục có phải là đối tượng ChatMessage không.
                                                  // Nếu không phải, đặt chiều cao mặc định và thoát.
            if (!(item is ChatMessage message))
            {
                e.ItemHeight = 20;
                return;
            }

            // Khởi tạo các biến cho việc đo kích thước.
            int timestampMargin = 8; // Khoảng cách lề cho dấu thời gian
            float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2); // Chiều rộng tối đa cho văn bản trong bong bóng (chiều rộng bong bóng - 2 * padding)
            if (maxTextWidth < 1) maxTextWidth = 1; // Đảm bảo chiều rộng tối thiểu là 1 để tránh lỗi đo lường

            float totalTextHeight = 0; // Tổng chiều cao của văn bản tin nhắn
            float totalTextWidth = 0; // Tổng chiều rộng lớn nhất của văn bản tin nhắn
            float measuredSenderNameHeight = 0; // Chiều cao đo được của tên người gửi
            float measuredTimestampHeight = 0; // Chiều cao đo được của dấu thời gian

            int bubbleTopY = VerticalSpacing; // Vị trí Y bắt đầu của bong bóng tin nhắn, có tính đến khoảng cách dọc ban đầu
                                              // Nếu tin nhắn không phải do tôi gửi VÀ tên người gửi không rỗng
            if (!message.IsSentByMe && !string.IsNullOrEmpty(message.SenderName))
            {
                // Sử dụng đối tượng Graphics để đo kích thước chuỗi.
                using (Graphics g = listBox.CreateGraphics())
                // Sử dụng Font nhỏ hơn và in đậm cho tên người gửi để đo kích thước.
                using (Font senderNameFont = new Font(listBox.Font.FontFamily, listBox.Font.Size * 0.85f, FontStyle.Bold))
                {
                    // Đo chiều cao cần thiết để hiển thị tên người gửi.
                    measuredSenderNameHeight = g.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic).Height;
                }
                // Cộng thêm chiều cao tên người gửi và khoảng cách dọc vào vị trí Y bắt đầu của bong bóng.
                bubbleTopY += (int)Math.Ceiling(measuredSenderNameHeight) + VerticalSpacing;
            }

            // Sử dụng đối tượng Graphics và StringFormat để đo kích thước nội dung tin nhắn và dấu thời gian.
            using (Graphics g = listBox.CreateGraphics())
            // Cấu hình StringFormat: NoClip để không cắt nội dung, NoWrap để đo chính xác chiều rộng dòng (không tự động xuống dòng khi đo).
            using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.NoWrap))
            {
                sf.Trimming = StringTrimming.None; // Không cắt văn bản khi đo

                // Nếu nội dung tin nhắn không rỗng hoặc null
                if (!string.IsNullOrEmpty(message.Content))
                {
                    // Đo kích thước của toàn bộ nội dung tin nhắn dựa trên chiều rộng tối đa cho phép.
                    SizeF textSizeF = g.MeasureString(message.Content, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf);
                    totalTextHeight = textSizeF.Height; // Lấy chiều cao đo được
                    totalTextWidth = textSizeF.Width; // Lấy chiều rộng đo được
                                                      // Ghi log debug kích thước văn bản đo được.
                    Debug.WriteLine($"[MeasureItem] Nội dung: '{message.Content}', Chiều rộng đo: {totalTextWidth}, Chiều cao đo: {totalTextHeight}");
                }

                // Sử dụng Font nhỏ hơn cho dấu thời gian để đo kích thước.
                using (Font timestampFont = new Font(listBox.Font.FontFamily, listBox.Font.Size * 0.7f))
                {
                    // Đo chiều cao cần thiết để hiển thị dấu thời gian.
                    measuredTimestampHeight = g.MeasureString(message.Timestamp.ToString("HH:mm"), timestampFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic).Height;
                }
            }

            // Tính toán chiều cao nội dung bên trong bong bóng (văn bản + 2 * padding).
            float bubbleContentHeight = totalTextHeight + MessageBubblePadding * 2;
            // Chiều cao hiệu quả của bong bóng, đảm bảo chiều cao tối thiểu là 20.
            float effectiveBubbleHeight = Math.Max(20, bubbleContentHeight);

            int totalHeight = 0; // Tổng chiều cao của mục trong ListBox
                                 // Tính toán tổng chiều cao dựa trên việc có hiển thị tên người gửi hay không.
            if (!message.IsSentByMe) // Nếu tin nhắn nhận được (hiển thị tên người gửi)
            {
                // Khoảng cách dọc (trên) + chiều cao tên người gửi (đã làm tròn lên) + khoảng cách dọc (giữa tên và bong bóng) + chiều cao bong bóng (đã làm tròn lên) + lề dấu thời gian + chiều cao dấu thời gian (đã làm tròn lên) + khoảng cách dọc (dưới)
                totalHeight = VerticalSpacing + (int)Math.Ceiling(measuredSenderNameHeight) + VerticalSpacing + (int)Math.Ceiling(effectiveBubbleHeight) + timestampMargin + (int)Math.Ceiling(measuredTimestampHeight) + VerticalSpacing;
            }
            else // Nếu tin nhắn gửi đi (không hiển thị tên người gửi ở đây)
            {
                // Khoảng cách dọc (trên) + chiều cao bong bóng (đã làm tròn lên) + lề dấu thời gian + chiều cao dấu thời gian (đã làm tròn lên) + khoảng cách dọc (dưới)
                totalHeight = VerticalSpacing + (int)Math.Ceiling(effectiveBubbleHeight) + timestampMargin + (int)Math.Ceiling(measuredTimestampHeight) + VerticalSpacing;
            }

            int minHeight = AvatarSize + VerticalSpacing * 2; // Chiều cao tối thiểu dựa trên kích thước avatar và khoảng cách dọc
                                                              // Đặt chiều cao của mục là giá trị lớn nhất giữa tổng chiều cao tính toán và chiều cao tối thiểu.
            e.ItemHeight = Math.Max(totalHeight, minHeight);
            if (e.ItemHeight < 1) e.ItemHeight = 1; // Đảm bảo chiều cao mục không nhỏ hơn 1
        }

        // Xử lý sự kiện vẽ một mục trong ListBox.
        // Phương thức này chịu trách nhiệm vẽ từng bong bóng tin nhắn, avatar, tên người gửi và dấu thời gian.
        private void LbChatMessages_DrawItem(object sender, DrawItemEventArgs e)
        {
            ListBox listBox = sender as ListBox; // Lấy đối tượng ListBox từ sender
            if (listBox == null) return; // Nếu không phải ListBox, thoát khỏi hàm

            // Kiểm tra xem chỉ số mục có hợp lệ không.
            // Nếu không hợp lệ, thoát khỏi hàm.
            if (e.Index < 0 || e.Index >= listBox.Items.Count)
                return;

            object item = listBox.Items[e.Index]; // Lấy mục tại chỉ số hiện tại
                                                  // Kiểm tra xem mục có phải là đối tượng ChatMessage không.
                                                  // Nếu không phải, vẽ nền và văn bản mặc định rồi thoát.
            if (!(item is ChatMessage message))
            {
                e.DrawBackground(); // Vẽ nền mặc định
                e.Graphics.DrawString(item.ToString(), e.Font, new SolidBrush(e.ForeColor), e.Bounds, StringFormat.GenericDefault); // Vẽ văn bản mặc định
                e.DrawFocusRectangle(); // Vẽ hình chữ nhật tiêu điểm nếu mục được chọn
                return;
            }

            // Ghi log debug nội dung tin nhắn đang được vẽ.
            Debug.WriteLine($"[DEBUG DrawItem] Nội dung tin nhắn: '{message.Content}'");

            // Vẽ nền của mục.
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected) // Nếu mục được chọn
            {
                // Sử dụng màu nền khi mục được chọn.
                using (SolidBrush backBrush = new SolidBrush(SelectedItemColor))
                {
                    e.Graphics.FillRectangle(backBrush, e.Bounds); // Tô màu nền
                }
            }
            else // Nếu mục không được chọn
            {
                // Sử dụng màu nền mặc định của mục.
                using (SolidBrush backBrush = new SolidBrush(e.BackColor))
                {
                    e.Graphics.FillRectangle(backBrush, e.Bounds); // Tô màu nền
                }
            }

            // Xác định màu sắc cho bong bóng tin nhắn (màu khác nhau cho tin nhắn gửi đi và nhận được).
            Color bubbleColor = message.IsSentByMe ? SentMessageColor : ReceivedMessageColor;
            // Cấu hình chế độ làm mịn và hiển thị văn bản cho đối tượng Graphics.
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; // Bật chế độ làm mịn (anti-aliasing)
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; // Bật chế độ ClearType để hiển thị văn bản rõ nét hơn

            // Tính toán chiều rộng tối đa cho văn bản trong bong bóng (chiều rộng bong bóng - 2 * padding).
            float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2);
            if (maxTextWidth < 1) maxTextWidth = 1; // Đảm bảo chiều rộng tối thiểu là 1

            float totalTextHeight = 0; // Tổng chiều cao của văn bản đã đo
            float totalTextWidth = 0; // Tổng chiều rộng lớn nhất của văn bản đã đo
                                      // Nếu nội dung tin nhắn không rỗng hoặc null
            if (!string.IsNullOrEmpty(message.Content))
            {
                // Sử dụng StringFormat để đo kích thước văn bản.
                using (StringFormat sfMeasure = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
                {
                    sfMeasure.Trimming = StringTrimming.None; // Không cắt văn bản khi đo

                    // Nếu không có URL trong tin nhắn
                    if (message.Urls == null || !message.Urls.Any())
                    {
                        // Đo kích thước của toàn bộ nội dung tin nhắn.
                        SizeF textSizeF = e.Graphics.MeasureString(message.Content, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure);
                        totalTextHeight = textSizeF.Height; // Lấy chiều cao đo được
                        totalTextWidth = textSizeF.Width; // Lấy chiều rộng đo được
                    }
                    else // Nếu có URL trong tin nhắn
                    {
                        int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi văn bản
                        var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL theo vị trí
                                                                                          // Lặp qua từng thông tin URL.
                        foreach (var urlInfo in sortedUrls)
                        {
                            string url = urlInfo.Item1; // URL
                            int urlStartIndex = urlInfo.Item2; // Vị trí bắt đầu của URL
                            int urlLength = urlInfo.Item3; // Độ dài của URL

                            if (urlStartIndex > currentTextIndex) // Nếu có văn bản trước URL
                            {
                                string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex); // Lấy văn bản trước URL
                                if (!string.IsNullOrEmpty(textBeforeUrl)) // Nếu văn bản trước không rỗng
                                {
                                    SizeF sizeBefore = e.Graphics.MeasureString(textBeforeUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure); // Đo kích thước văn bản trước
                                    totalTextHeight += sizeBefore.Height; // Cộng dồn chiều cao
                                    totalTextWidth = Math.Max(totalTextWidth, sizeBefore.Width); // Cập nhật chiều rộng tối đa
                                }
                            }

                            SizeF sizeUrl = e.Graphics.MeasureString(url, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure); // Đo kích thước URL
                            totalTextHeight += sizeUrl.Height; // Cộng dồn chiều cao của URL
                            totalTextWidth = Math.Max(totalTextWidth, sizeUrl.Width); // Cập nhật chiều rộng tối đa
                            currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí hiện tại
                        }

                        if (currentTextIndex < message.Content.Length) // Nếu còn văn bản sau URL cuối cùng
                        {
                            string textAfterUrl = message.Content.Substring(currentTextIndex); // Lấy văn bản sau URL
                            if (!string.IsNullOrEmpty(textAfterUrl)) // Nếu văn bản sau không rỗng
                            {
                                SizeF sizeAfter = e.Graphics.MeasureString(textAfterUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure); // Đo kích thước văn bản sau
                                totalTextHeight += sizeAfter.Height; // Cộng dồn chiều cao
                                totalTextWidth = Math.Max(totalTextWidth, sizeAfter.Width); // Cập nhật chiều rộng tối đa
                            }
                        }
                    }
                }
            }

            // Tính toán kích thước của bong bóng tin nhắn.
            int bubbleWidth = Math.Min(MaxBubbleWidth, Math.Max(20, (int)totalTextWidth + MessageBubblePadding * 2)); // Chiều rộng bong bóng (tối đa MaxBubbleWidth, tối thiểu 20, có tính padding)
            Size bubbleSize = new Size(bubbleWidth, Math.Max(20, (int)(totalTextHeight + MessageBubblePadding * 2))); // Chiều cao bong bóng (tối thiểu 20, có tính padding)
            Rectangle bubbleRect, avatarRect, textRect, senderNameRect = Rectangle.Empty, timestampRect; // Khai báo các hình chữ nhật cho các thành phần

            // Xác định vị trí của các thành phần dựa trên việc tin nhắn do tôi gửi hay nhận được.
            if (message.IsSentByMe) // Nếu tin nhắn do tôi gửi (avatar bên phải, bong bóng bên trái)
            {
                avatarRect = new Rectangle(e.Bounds.Right - AvatarSize - 10, e.Bounds.Top + VerticalSpacing, AvatarSize, AvatarSize); // Vị trí avatar (bên phải mục, có lề)
                bubbleRect = new Rectangle(avatarRect.Left - AvatarMargin - bubbleSize.Width, e.Bounds.Top + VerticalSpacing, bubbleSize.Width, bubbleSize.Height); // Vị trí bong bóng (bên trái avatar, có khoảng cách lề)
                textRect = new Rectangle(
                    bubbleRect.Left + MessageBubblePadding, // Vị trí X của văn bản trong bong bóng (có padding)
                    bubbleRect.Top + MessageBubblePadding, // Vị trí Y của văn bản trong bong bóng (có padding)
                    (int)totalTextWidth + 10, // Chiều rộng của vùng vẽ văn bản (thêm lề nhỏ)
                    (int)totalTextHeight + 10 // Chiều cao của vùng vẽ văn bản (thêm lề nhỏ)
                );

                // Đo kích thước và xác định vị trí của dấu thời gian (bên dưới bong bóng, căn phải)
                SizeF timestampSizeF = e.Graphics.MeasureString(message.Timestamp.ToString("HH:mm"), new Font(e.Font.FontFamily, e.Font.Size * 0.7f)); // Đo kích thước dấu thời gian
                Size timestampSize = Size.Ceiling(timestampSizeF); // Làm tròn kích thước lên số nguyên
                timestampRect = new Rectangle(bubbleRect.Right - timestampSize.Width, bubbleRect.Bottom + 8, timestampSize.Width, timestampSize.Height); // Vị trí dấu thời gian (bên phải bong bóng, có khoảng cách 8px xuống dưới)
            }
            else // Nếu tin nhắn do người khác gửi (avatar bên trái, bong bóng bên phải)
            {
                avatarRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + VerticalSpacing, AvatarSize, AvatarSize); // Vị trí avatar (bên trái mục, có lề)
                int bubbleTopY = e.Bounds.Top + VerticalSpacing; // Vị trí Y bắt đầu của bong bóng

                if (!string.IsNullOrEmpty(message.SenderName)) // Nếu có tên người gửi
                {
                    // Sử dụng Font nhỏ hơn và in đậm cho tên người gửi để đo kích thước.
                    using (Font senderNameFont = new Font(e.Font.FontFamily, e.Font.Size * 0.85f, FontStyle.Bold))
                    {
                        SizeF senderNameSizeF = e.Graphics.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic); // Đo kích thước tên người gửi
                        Size senderNameSize = Size.Ceiling(senderNameSizeF); // Làm tròn kích thước lên số nguyên
                        senderNameRect = new Rectangle(avatarRect.Right + AvatarMargin, e.Bounds.Top + VerticalSpacing, senderNameSize.Width, 14); // Vị trí tên người gửi (bên phải avatar, có khoảng cách lề)
                        bubbleTopY = senderNameRect.Bottom + VerticalSpacing; // Cập nhật vị trí Y bắt đầu của bong bóng (nằm dưới tên người gửi)
                    }
                }

                bubbleRect = new Rectangle(avatarRect.Right + AvatarMargin, bubbleTopY, bubbleSize.Width, bubbleSize.Height); // Vị trí bong bóng (bên phải avatar, có khoảng cách lề, vị trí Y đã được cập nhật)
                textRect = new Rectangle(
                    bubbleRect.Left + MessageBubblePadding, // Vị trí X của văn bản trong bong bóng (có padding)
                    bubbleRect.Top + MessageBubblePadding, // Vị trí Y của văn bản trong bong bóng (có padding)
                    (int)totalTextWidth + 10, // Chiều rộng của vùng vẽ văn bản (thêm lề nhỏ)
                    (int)totalTextHeight + 10 // Chiều cao của vùng vẽ văn bản (thêm lề nhỏ)
                );

                // Đo kích thước và xác định vị trí của dấu thời gian (bên dưới bong bóng, căn trái)
                SizeF timestampSizeF = e.Graphics.MeasureString(message.Timestamp.ToString("HH:mm"), new Font(e.Font.FontFamily, e.Font.Size * 0.7f)); // Đo kích thước dấu thời gian
                Size timestampSize = Size.Ceiling(timestampSizeF); // Làm tròn kích thước lên số nguyên
                timestampRect = new Rectangle(bubbleRect.Left, bubbleRect.Bottom + 8, timestampSize.Width, timestampSize.Height); // Vị trí dấu thời gian (bên trái bong bóng, có khoảng cách 8px xuống dưới)
            }

            // Vẽ bong bóng tin nhắn nếu kích thước hợp lệ.
            if (bubbleRect.Width > 0 && bubbleRect.Height > 0)
            {
                // Tính toán bán kính bo tròn hiệu quả, không vượt quá một nửa chiều rộng hoặc chiều cao của bong bóng.
                int effectiveRadius = Math.Min(MessageBubbleCornerRadius, Math.Min(bubbleRect.Width / 2, bubbleRect.Height / 2));
                // Tạo đường dẫn hình học cho hình chữ nhật bo tròn.
                using (GraphicsPath path = RoundedRectangle(bubbleRect, effectiveRadius))
                // Sử dụng SolidBrush với màu bong bóng đã xác định.
                using (SolidBrush bubbleFillBrush = new SolidBrush(bubbleColor))
                {
                    e.Graphics.FillPath(bubbleFillBrush, path); // Tô màu cho bong bóng
                }

                // Vẽ nội dung văn bản bên trong bong bóng nếu nội dung và kích thước vùng vẽ văn bản hợp lệ.
                if (!string.IsNullOrEmpty(message.Content) && textRect.Width > 0 && textRect.Height > 0)
                {
                    // Sử dụng StringFormat để vẽ văn bản.
                    using (StringFormat sfDraw = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
                    {
                        sfDraw.Trimming = StringTrimming.None; // Không cắt văn bản khi vẽ
                        sfDraw.Alignment = StringAlignment.Near; // Căn lề trái
                        sfDraw.LineAlignment = StringAlignment.Near; // Căn lề trên

                        // Nếu không có URL trong tin nhắn
                        if (message.Urls == null || !message.Urls.Any())
                        {
                            // Vẽ toàn bộ nội dung văn bản với màu đen mặc định.
                            using (SolidBrush defaultBrush = new SolidBrush(Color.Black))
                            {
                                e.Graphics.DrawString(message.Content, e.Font, defaultBrush, textRect, sfDraw);
                            }
                        }
                        else // Nếu có URL trong tin nhắn
                        {
                            float currentY = textRect.Y; // Vị trí Y hiện tại để vẽ văn bản hoặc URL
                            int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi nội dung tin nhắn
                            var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL theo vị trí

                            Debug.WriteLine($"[DEBUG DrawItem] Đang xử lý các URL cho tin nhắn tại chỉ số {e.Index}, Nội dung: {message.Content}"); // Ghi log

                            // Lặp qua từng thông tin URL đã sắp xếp.
                            foreach (var urlInfo in sortedUrls)
                            {
                                string url = urlInfo.Item1; // Lấy URL
                                int urlStartIndex = urlInfo.Item2; // Lấy vị trí bắt đầu của URL
                                int urlLength = urlInfo.Item3; // Lấy độ dài của URL

                                if (urlStartIndex > currentTextIndex) // Nếu có văn bản trước URL
                                {
                                    string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex); // Lấy văn bản trước URL
                                    if (!string.IsNullOrEmpty(textBeforeUrl)) // Nếu văn bản trước không rỗng
                                    {
                                        SizeF sizeBefore = e.Graphics.MeasureString(textBeforeUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfDraw); // Đo kích thước văn bản trước
                                        RectangleF beforeRect = new RectangleF(textRect.X, currentY, maxTextWidth + 10, sizeBefore.Height); // Hình chữ nhật để vẽ văn bản trước (có thêm lề nhỏ)
                                        using (SolidBrush textBrush = new SolidBrush(Color.Black)) // Sử dụng màu đen cho văn bản thường
                                        {
                                            e.Graphics.DrawString(textBeforeUrl, e.Font, textBrush, beforeRect, sfDraw); // Vẽ văn bản trước URL
                                            Debug.WriteLine($"[DEBUG DrawItem] Đã vẽ văn bản trước URL: '{textBeforeUrl}', Hình chữ nhật: {beforeRect}"); // Ghi log
                                        }
                                        currentY += sizeBefore.Height; // Cập nhật vị trí Y xuống dưới
                                    }
                                }

                                // Vẽ URL với font gạch chân và màu xanh.
                                using (Font urlFont = new Font(e.Font, FontStyle.Underline))
                                using (SolidBrush urlBrush = new SolidBrush(Color.Blue))
                                {
                                    SizeF sizeUrl = e.Graphics.MeasureString(url, urlFont, new SizeF(maxTextWidth, float.MaxValue), sfDraw); // Đo kích thước URL
                                    RectangleF urlRect = new RectangleF(textRect.X, currentY, sizeUrl.Width + 10, sizeUrl.Height); // Hình chữ nhật để vẽ URL (có thêm lề nhỏ)
                                    e.Graphics.DrawString(url, urlFont, urlBrush, urlRect, sfDraw); // Vẽ URL
                                    Debug.WriteLine($"[DEBUG DrawItem] Đã vẽ URL: '{url}', Hình chữ nhật: {urlRect}"); // Ghi log
                                    currentY += sizeUrl.Height; // Cập nhật vị trí Y xuống dưới
                                }

                                currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí hiện tại trong chuỗi sau khi xử lý URL
                            }

                            if (currentTextIndex < message.Content.Length) // Nếu còn văn bản sau URL cuối cùng
                            {
                                string textAfterUrl = message.Content.Substring(currentTextIndex); // Lấy văn bản sau URL
                                if (!string.IsNullOrEmpty(textAfterUrl)) // Nếu văn bản sau không rỗng
                                {
                                    SizeF sizeAfter = e.Graphics.MeasureString(textAfterUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfDraw); // Đo kích thước văn bản sau
                                    RectangleF afterRect = new RectangleF(textRect.X, currentY, maxTextWidth + 10, sizeAfter.Height); // Hình chữ nhật để vẽ văn bản sau (có thêm lề nhỏ)
                                    using (SolidBrush textBrush = new SolidBrush(Color.Black)) // Sử dụng màu đen cho văn bản thường
                                    {
                                        e.Graphics.DrawString(textAfterUrl, e.Font, textBrush, afterRect, sfDraw); // Vẽ văn bản sau URL
                                        Debug.WriteLine($"[DEBUG DrawItem] Đã vẽ văn bản sau URL: '{textAfterUrl}', Hình chữ nhật: {afterRect}"); // Ghi log
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Vẽ avatar (hình tròn với biểu tượng người).
            using (Brush avatarBrush = new SolidBrush(Color.FromArgb(200, 200, 200))) // Màu nền avatar
            using (Pen avatarPen = new Pen(Color.FromArgb(150, 150, 150), 1)) // Màu viền avatar
            {
                if (avatarRect.Width > 0 && avatarRect.Height > 0) // Nếu kích thước avatar hợp lệ
                {
                    e.Graphics.FillEllipse(avatarBrush, avatarRect); // Tô màu hình tròn
                    e.Graphics.DrawEllipse(avatarPen, avatarRect); // Vẽ viền hình tròn
                    using (GraphicsPath humanPath = new GraphicsPath()) // Tạo đường dẫn cho biểu tượng người
                    {
                        // Tính toán kích thước và vị trí đầu, thân biểu tượng dựa trên kích thước avatar.
                        float headSize = AvatarSize * 0.4f;
                        float bodyWidth = AvatarSize * 0.7f;
                        float bodyHeight = AvatarSize * 0.5f;
                        float headX = avatarRect.X + (AvatarSize - headSize) / 2;
                        float headY = avatarRect.Y + AvatarSize * 0.15f;
                        float bodyX = avatarRect.X + (AvatarSize - bodyWidth) / 2;
                        float bodyY = avatarRect.Y + AvatarSize * 0.5f;
                        Rectangle headRect = new Rectangle((int)headX, (int)headY, Math.Max(1, (int)headSize), Math.Max(1, (int)headSize)); // Hình chữ nhật cho đầu (đảm bảo kích thước tối thiểu 1)
                        if (headRect.Width > 0 && headRect.Height > 0)
                        {
                            humanPath.AddEllipse(headRect); // Thêm hình elip cho đầu vào đường dẫn
                        }
                        RectangleF bodyArcRect = new RectangleF(bodyX, bodyY - bodyHeight / 2, bodyWidth, bodyHeight); // Hình chữ nhật cho cung tròn của thân
                        if (bodyArcRect.Width > 0 && bodyArcRect.Height > 0)
                        {
                            humanPath.AddArc(bodyArcRect, 0, 180); // Thêm cung tròn 180 độ cho thân
                            humanPath.CloseFigure(); // Đóng hình để tạo hình dạng thân
                        }
                        using (Brush humanBrush = new SolidBrush(Color.DarkGray)) // Màu cho biểu tượng người
                        {
                            if (humanPath.PointCount > 0) // Kiểm tra xem đường dẫn có chứa điểm không
                            {
                                e.Graphics.FillPath(humanBrush, humanPath); // Tô màu biểu tượng người
                            }
                        }
                    }
                }
            }

            // Vẽ tên người gửi nếu tin nhắn không phải do tôi gửi, vùng vẽ tên hợp lệ và tên người gửi không rỗng.
            if (!message.IsSentByMe && senderNameRect.Width > 0 && senderNameRect.Height > 0 && !string.IsNullOrEmpty(message.SenderName))
            {
                using (Font senderNameFont = new Font(e.Font.FontFamily, e.Font.Size * 0.85f, FontStyle.Bold)) // Font nhỏ hơn và in đậm cho tên người gửi
                using (Brush senderNameBrush = new SolidBrush(Color.DimGray)) // Màu xám cho tên người gửi
                {
                    e.Graphics.DrawString(message.SenderName, senderNameFont, senderNameBrush, senderNameRect, StringFormat.GenericTypographic); // Vẽ tên người gửi
                }
            }

            // Vẽ dấu thời gian.
            using (Font timestampFont = new Font(e.Font.FontFamily, e.Font.Size * 0.7f)) // Font nhỏ hơn cho dấu thời gian
            {
                SizeF timestampSizeF = e.Graphics.MeasureString(message.Timestamp.ToString("HH:mm"), timestampFont, new SizeF(e.Bounds.Width, float.MaxValue), StringFormat.GenericTypographic); // Đo kích thước dấu thời gian
                Size timestampSize = Size.Ceiling(timestampSizeF); // Làm tròn kích thước lên số nguyên
                if (timestampRect.Width > 0 && timestampRect.Height > 0) // Nếu kích thước vùng vẽ dấu thời gian hợp lệ
                {
                    e.Graphics.DrawString(message.Timestamp.ToString("HH:mm"), timestampFont, new SolidBrush(Color.Gray), timestampRect, StringFormat.GenericTypographic); // Vẽ dấu thời gian với màu xám
                }
            }
            e.DrawFocusRectangle(); // Vẽ hình chữ nhật tiêu điểm nếu mục được chọn
        }

        // Phương thức trợ giúp để tạo một GraphicsPath cho hình chữ nhật bo tròn.
        private GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath(); // Khởi tạo một đối tượng GraphicsPath mới
                                                    // Kiểm tra nếu bán kính bo tròn nhỏ hơn hoặc bằng 0
            if (radius <= 0)
            {
                path.AddRectangle(bounds); // Nếu không bo tròn, chỉ thêm một hình chữ nhật thông thường vào đường dẫn
                return path; // Trả về đường dẫn chứa hình chữ nhật
            }
            int diameter = radius * 2; // Tính toán đường kính từ bán kính
                                       // Tạo một hình chữ nhật tạm thời có kích thước bằng đường kính để định vị các cung tròn
            Rectangle arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);

            // Thêm cung tròn cho góc trên bên trái (bắt đầu từ 180 độ, quét 90 độ)
            path.AddArc(arc, 180, 90);

            // Di chuyển hình chữ nhật tạm thời đến vị trí của góc trên bên phải
            arc.X = bounds.Right - diameter;
            // Thêm cung tròn cho góc trên bên phải (bắt đầu từ 270 độ, quét 90 độ)
            path.AddArc(arc, 270, 90);

            // Di chuyển hình chữ nhật tạm thời đến vị trí của góc dưới bên phải
            arc.Y = bounds.Bottom - diameter;
            // Thêm cung tròn cho góc dưới bên phải (bắt đầu từ 0 độ, quét 90 độ)
            path.AddArc(arc, 0, 90);

            // Di chuyển hình chữ nhật tạm thời đến vị trí của góc dưới bên trái
            arc.X = bounds.Left;
            // Thêm cung tròn cho góc dưới bên trái (bắt đầu từ 90 độ, quét 90 độ)
            path.AddArc(arc, 90, 90);

            path.CloseFigure(); // Đóng hình (tạo các đường thẳng nối giữa các cung tròn)
            return path; // Trả về đường dẫn hình chữ nhật bo tròn
        }
    }
}