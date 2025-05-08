using Messenger;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;

// Định nghĩa partial class cho form chat riêng tư, thừa kế từ Form
public partial class PrivateChatForm : Form
{    
    private string recipientUserName;// Biến lưu trữ tên người nhận tin nhắn    
    private TcpClient tcpClient;// Đối tượng TcpClient để kết nối mạng    
    private ListBox lbPrivateChatMessages;// ListBox để hiển thị các tin nhắn chat riêng tư   
    private TextBox txtMessageInput; // TextBox để người dùng nhập tin nhắn    
    private Panel pnlInputArea;// Panel chứa khu vực nhập tin nhắn    
    private ContextMenuStrip messageContextMenu;// Menu ngữ cảnh cho tin nhắn    
    private ToolStripMenuItem copyMessageMenuItem;// Mục menu để sao chép tin nhắn    
    private string myName;// Tên của người dùng hiện tại (người gửi)    
    private const int MessageBubblePadding = 12;// Hằng số định nghĩa khoảng đệm cho bong bóng tin nhắn    
    private const int MessageBubbleCornerRadius = 18;// Hằng số định nghĩa bán kính bo tròn góc cho bong bóng tin nhắn    
    private const int AvatarSize = 32;// Hằng số định nghĩa kích thước avatar    
    private const int AvatarMargin = 8;// Hằng số định nghĩa khoảng cách lề cho avatar    
    private const int TimestampHeight = 15;// Hằng số định nghĩa chiều cao cho dấu thời gian    
    private const int SenderNameHeight = 14;// Hằng số định nghĩa chiều cao cho tên người gửi    
    private const int VerticalSpacing = 8;// Hằng số định nghĩa khoảng cách dọc giữa các mục   
    private Color SentMessageColor = Color.FromArgb(136, 219, 136); // Màu sắc cho tin nhắn đã gửi đi
    private Color ReceivedMessageColor = Color.FromArgb(220, 220, 220);// Màu sắc cho tin nhắn nhận được   
    private Color BackgroundColor = Color.FromArgb(240, 242, 245);// Màu nền của form chat    
    private Color ChatAreaBackgroundColor = Color.White;// Màu nền của khu vực hiển thị chat (ListBox) 
    private Color SelectedItemColor = Color.FromArgb(250, 250, 250);// Màu sắc khi chọn một mục trong ListBox
    private const int MaxBubbleWidth = 250; // Chiều rộng tối đa của bong bóng tin nhắn

    // Hàm tìm các URL trong đoạn văn bản
    private List<Tuple<string, int, int>> FindUrlsInText(string text)
    {       
        List<Tuple<string, int, int>> urls = new List<Tuple<string, int, int>>();// Khởi tạo danh sách để lưu trữ thông tin URL (URL, vị trí bắt đầu, độ dài)       
        Regex urlRegex = new Regex(@"\b(?:https?://|www\.)?[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*\.[a-zA-Z]{2,}(?:[/\w- .?%&=#]*)?\b", RegexOptions.IgnoreCase);// Biểu thức chính quy để phát hiện URL       
        MatchCollection matches = urlRegex.Matches(text);// Tìm tất cả các URL phù hợp trong văn bản      
        foreach (Match match in matches)// Lặp qua các kết quả tìm được
        {            
            urls.Add(Tuple.Create(match.Value, match.Index, match.Length));// Thêm thông tin URL vào danh sách
            
            Debug.WriteLine($"[DEBUG FindUrlsInText] Tìm thấy URL: '{match.Value}' tại vị trí {match.Index}, độ dài {match.Length}");// Ghi log debug khi tìm thấy URL
        }        
        return urls;// Trả về danh sách URL tìm được
    }

    // Constructor của form chat riêng tư
    public PrivateChatForm(string recipientUserName, TcpClient tcpClient, string myName)
    {
        // Gán giá trị cho các biến thành viên
        this.recipientUserName = recipientUserName;
        this.tcpClient = tcpClient;
        this.myName = myName;
        // Khởi tạo menu ngữ cảnh cho tin nhắn
        InitializeMessageContextMenu();
        // Khởi tạo các component của form
        InitializeComponent();
        // Thiết lập tiêu đề form
        this.Text = $"Chat riêng với {recipientUserName}";
        try
        {
            // Thử tải biểu tượng cho form
            this.Icon = new Icon(typeof(Messenger.ChatForm), "icon.ico");
        }
        catch { } // Bỏ qua nếu có lỗi khi tải biểu tượng
        // Tải lịch sử chat từ tệp
        LoadHistoryFromFile();
    }

    // Phương thức khởi tạo các component của form
    private void InitializeComponent()
    {
        // Khởi tạo ListBox, TextBox và Panel
        this.lbPrivateChatMessages = new ListBox();
        this.txtMessageInput = new TextBox();
        this.pnlInputArea = new Panel();
        // Bắt đầu tạm dừng layout để tối ưu hiệu suất
        this.SuspendLayout();
        this.pnlInputArea.SuspendLayout();

        // Cấu hình TextBox nhập tin nhắn
        this.txtMessageInput.Text = "Nhập tin nhắn ..."; // Văn bản mặc định
        this.txtMessageInput.ForeColor = Color.Gray; // Màu chữ mặc định
        // Đăng ký các sự kiện focus và text changed
        this.txtMessageInput.GotFocus += TxtMessageInput_GotFocus;
        this.txtMessageInput.LostFocus += TxtMessageInput_LostFocus;
        this.txtMessageInput.TextChanged += TxtMessageInput_TextChanged;

        // Cấu hình font và màu nền cho form
        this.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
        this.BackColor = BackgroundColor;

        // Cấu hình ListBox hiển thị tin nhắn
        this.lbPrivateChatMessages.FormattingEnabled = true; // Cho phép định dạng
        this.lbPrivateChatMessages.Location = new Point(10, 10); // Vị trí
        this.lbPrivateChatMessages.Name = "lbPrivateChatMessages"; // Tên
        this.lbPrivateChatMessages.Size = new Size(380, 460); // Kích thước
        this.lbPrivateChatMessages.TabIndex = 0; // Thứ tự tab
        this.lbPrivateChatMessages.DrawMode = DrawMode.OwnerDrawVariable; // Chế độ vẽ tùy chỉnh
        // Đăng ký các sự kiện vẽ và đo kích thước mục
        this.lbPrivateChatMessages.DrawItem += LbPrivateChatMessages_DrawItem;
        this.lbPrivateChatMessages.MeasureItem += LbPrivateChatMessages_MeasureItem;
        this.lbPrivateChatMessages.ContextMenuStrip = messageContextMenu; // Gán menu ngữ cảnh
        // Cấu hình neo (anchor) để điều chỉnh kích thước khi form thay đổi
        this.lbPrivateChatMessages.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.lbPrivateChatMessages.BackColor = ChatAreaBackgroundColor; // Màu nền
        this.lbPrivateChatMessages.BorderStyle = BorderStyle.None; // Bỏ đường viền
        // Đăng ký các sự kiện chuột
        this.lbPrivateChatMessages.MouseDown += LbPrivateChatMessages_MouseDown;
        this.lbPrivateChatMessages.MouseMove += LbPrivateChatMessages_MouseMove;

        // Cấu hình Panel chứa khu vực nhập liệu
        this.pnlInputArea.BorderStyle = BorderStyle.None; // Bỏ đường viền
        this.pnlInputArea.Location = new Point(10, this.lbPrivateChatMessages.Bottom + 10); // Vị trí
        this.pnlInputArea.Name = "pnlInputArea"; // Tên
        this.pnlInputArea.Size = new Size(this.lbPrivateChatMessages.Width, 60); // Kích thước
        this.pnlInputArea.TabIndex = 1; // Thứ tự tab
        this.pnlInputArea.Controls.Add(this.txtMessageInput); // Thêm TextBox vào Panel
        this.pnlInputArea.Dock = DockStyle.Bottom; // Neo vào đáy form
        this.pnlInputArea.BackColor = Color.PaleGoldenrod; // Màu nền (có vẻ chỉ để debug hoặc tạm thời)
        // Cấu hình neo (anchor)
        this.pnlInputArea.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        this.pnlInputArea.BorderStyle = BorderStyle.None; // Đảm bảo không có đường viền

        // Cấu hình chi tiết hơn cho TextBox nhập tin nhắn
        this.txtMessageInput.BorderStyle = BorderStyle.None; // Bỏ đường viền
        this.txtMessageInput.Location = new Point(1, 1); // Vị trí trong Panel
        this.txtMessageInput.Multiline = true; // Cho phép nhiều dòng
        this.txtMessageInput.Name = "txtMessageInput"; // Tên
        // Kích thước dựa trên chiều cao font và số dòng
        this.txtMessageInput.Size = new Size(this.pnlInputArea.Width - 2, txtMessageInput.Font.Height * 4 + 6);
        this.txtMessageInput.TabIndex = 0; // Thứ tự tab trong Panel
        this.txtMessageInput.Font = new Font("Segoe UI", 9.75F); // Font
        this.txtMessageInput.BackColor = Color.FromArgb(255, 255, 225); // Màu nền
        // Cấu hình neo (anchor)
        this.txtMessageInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        this.txtMessageInput.AcceptsReturn = true; // Chấp nhận phím Enter tạo dòng mới
        this.txtMessageInput.ScrollBars = ScrollBars.None; // Ban đầu không hiển thị thanh cuộn
        this.txtMessageInput.WordWrap = true; // Tự động xuống dòng
        // Đăng ký sự kiện nhấn phím
        this.txtMessageInput.KeyDown += new KeyEventHandler(this.TxtMessageInput_KeyDown);

        // Cấu hình kích thước form
        this.ClientSize = new Size(400, 550);
        // Thêm Panel và ListBox vào Controls của form
        this.Controls.Add(this.pnlInputArea);
        this.Controls.Add(this.lbPrivateChatMessages);
        this.Name = "PrivateChatForm"; // Tên form
        this.MinimumSize = new Size(400, 350); // Kích thước tối thiểu
        // Tiếp tục layout sau khi tạm dừng
        this.ResumeLayout(false);
        this.pnlInputArea.ResumeLayout(false);
        this.pnlInputArea.PerformLayout(); // Đảm bảo Panel tính toán lại layout
    }

    // Phương thức khởi tạo menu ngữ cảnh cho tin nhắn
    private void InitializeMessageContextMenu()
    {       
        messageContextMenu = new ContextMenuStrip(); // Khởi tạo ContextMenuStrip       
        copyMessageMenuItem = new ToolStripMenuItem("Sao chép");// Khởi tạo ToolStripMenuItem "Sao chép"       
        copyMessageMenuItem.Click += CopyMessageMenuItem_Click; // Đăng ký sự kiện Click cho mục "Sao chép"       
        messageContextMenu.Items.Add(copyMessageMenuItem); // Thêm mục "Sao chép" vào menu       
        messageContextMenu.Opening += MessageContextMenu_Opening; // Đăng ký sự kiện Opening của menu ngữ cảnh
    }

    // Phương thức tính toán vị trí và kích thước của các vùng URL trong tin nhắn
    private List<Tuple<RectangleF, string>> CalculateUrlRegions(ChatMessage message, Rectangle itemBounds, Graphics g, Font font, float maxTextWidth)
    {
        // Khởi tạo danh sách để lưu trữ vùng hình chữ nhật và URL tương ứng
        var urlRegions = new List<Tuple<RectangleF, string>>();
        // Biến theo dõi tổng chiều cao và chiều rộng văn bản
        float totalTextHeight = 0;
        float totalTextWidth = 0;

        // Sử dụng StringFormat để đo kích thước chuỗi
        using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
        {
            sf.Trimming = StringTrimming.Character; // Cắt ký tự khi vượt quá kích thước

            // Kiểm tra nếu nội dung tin nhắn không rỗng hoặc null
            if (!string.IsNullOrEmpty(message.Content))
            {
                // Biến theo dõi vị trí hiện tại trong chuỗi văn bản
                int currentTextIndex = 0;
                // Sắp xếp các URL theo vị trí bắt đầu
                var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList();
                // Lặp qua từng thông tin URL đã sắp xếp
                foreach (var urlInfo in sortedUrls)
                {
                    string url = urlInfo.Item1; // Lấy URL
                    int urlStartIndex = urlInfo.Item2; // Lấy vị trí bắt đầu của URL
                    int urlLength = urlInfo.Item3; // Lấy độ dài của URL

                    // Xử lý văn bản trước URL (nếu có)
                    if (urlStartIndex > currentTextIndex)
                    {
                        string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                        if (!string.IsNullOrEmpty(textBeforeUrl))
                        {
                            // Đo kích thước văn bản trước URL
                            SizeF sizeBefore = g.MeasureString(textBeforeUrl, font, new SizeF(maxTextWidth, float.MaxValue), sf);
                            totalTextHeight += sizeBefore.Height; // Cộng dồn chiều cao
                            totalTextWidth = Math.Max(totalTextWidth, sizeBefore.Width); // Cập nhật chiều rộng lớn nhất
                        }
                    }

                    SizeF sizeUrl;
                    // Sử dụng font gạch chân để đo kích thước URL
                    using (Font urlFont = new Font(font, FontStyle.Underline))
                    {
                        sizeUrl = g.MeasureString(url, urlFont, new SizeF(maxTextWidth, float.MaxValue), sf);
                    }
                    totalTextHeight += sizeUrl.Height; // Cộng dồn chiều cao của URL
                    totalTextWidth = Math.Max(totalTextWidth, sizeUrl.Width); // Cập nhật chiều rộng lớn nhất
                    currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí hiện tại
                }

                // Xử lý văn bản sau URL cuối cùng (nếu có)
                if (currentTextIndex < message.Content.Length)
                {
                    string textAfterUrl = message.Content.Substring(currentTextIndex);
                    if (!string.IsNullOrEmpty(textAfterUrl))
                    {
                        // Đo kích thước văn bản sau URL
                        SizeF sizeAfter = g.MeasureString(textAfterUrl, font, new SizeF(maxTextWidth, float.MaxValue), sf);
                        totalTextHeight += sizeAfter.Height; // Cộng dồn chiều cao
                        totalTextWidth = Math.Max(totalTextWidth, sizeAfter.Width); // Cập nhật chiều rộng lớn nhất
                    }
                }
            }
        }

        // Tính toán kích thước bong bóng tin nhắn dựa trên kích thước văn bản
        int bubbleWidth = Math.Min(MaxBubbleWidth, Math.Max(20, (int)totalTextWidth + MessageBubblePadding * 2));
        Size bubbleSize = new Size(bubbleWidth, Math.Max(20, (int)(totalTextHeight + MessageBubblePadding * 2)));
        Rectangle bubbleRect, textRect; // Khai báo hình chữ nhật cho bong bóng và văn bản

        // Xác định vị trí của bong bóng và văn bản dựa trên người gửi (tôi hay người khác)
        if (message.IsSentByMe) // Nếu tin nhắn do tôi gửi
        {
            // Vị trí avatar (bên phải)
            Rectangle avatarRect = new Rectangle(itemBounds.Right - AvatarSize - 10, itemBounds.Top + VerticalSpacing, AvatarSize, AvatarSize);
            // Vị trí bong bóng (bên trái avatar)
            bubbleRect = new Rectangle(avatarRect.Left - AvatarMargin - bubbleSize.Width, itemBounds.Top + VerticalSpacing, bubbleSize.Width, bubbleSize.Height);
            // Vị trí văn bản bên trong bong bóng
            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth, (int)totalTextHeight);
        }
        else // Nếu tin nhắn do người khác gửi
        {
            // Vị trí avatar (bên trái)
            Rectangle avatarRect = new Rectangle(itemBounds.Left + 10, itemBounds.Top + VerticalSpacing, AvatarSize, AvatarSize);
            int bubbleTopY = itemBounds.Top + VerticalSpacing; // Vị trí Y ban đầu của bong bóng

            // Điều chỉnh vị trí Y nếu có hiển thị tên người gửi
            if (!string.IsNullOrEmpty(message.SenderName))
            {
                using (Font senderNameFont = new Font(font.FontFamily, font.Size * 0.85f, FontStyle.Bold))
                {
                    SizeF senderNameSizeF = g.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic);
                    bubbleTopY += (int)Math.Ceiling(senderNameSizeF.Height) + VerticalSpacing; // Cộng thêm chiều cao tên người gửi và khoảng cách
                }
            }
            // Vị trí bong bóng (bên phải avatar)
            bubbleRect = new Rectangle(avatarRect.Right + AvatarMargin, bubbleTopY, bubbleSize.Width, bubbleSize.Height);
            // Vị trí văn bản bên trong bong bóng
            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth, (int)totalTextHeight);
        }

        // Tính toán vị trí và kích thước cụ thể cho từng vùng URL dựa trên textRect
        using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
        {
            sf.Trimming = StringTrimming.Character;
            sf.Alignment = StringAlignment.Near;
            sf.LineAlignment = StringAlignment.Near;

            float currentY = textRect.Y - itemBounds.Y; // Vị trí Y hiện tại tương đối so với itemBounds
            int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi văn bản
            var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL theo vị trí

            // Lặp qua từng thông tin URL
            foreach (var urlInfo in sortedUrls)
            {
                string url = urlInfo.Item1;
                int urlStartIndex = urlInfo.Item2;
                int urlLength = urlInfo.Item3;

                // Xử lý văn bản trước URL
                if (urlStartIndex > currentTextIndex)
                {
                    string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                    if (!string.IsNullOrEmpty(textBeforeUrl))
                    {
                        SizeF sizeBefore = g.MeasureString(textBeforeUrl, font, new SizeF(maxTextWidth, float.MaxValue), sf);
                        currentY += sizeBefore.Height; // Tăng vị trí Y theo chiều cao văn bản trước URL
                    }
                }

                SizeF sizeUrl;
                // Sử dụng font gạch chân để đo kích thước URL
                using (Font urlFont = new Font(font, FontStyle.Underline))
                {
                    sizeUrl = g.MeasureString(url, urlFont, new SizeF(maxTextWidth, float.MaxValue), sf);
                    // Tạo hình chữ nhật cho vùng URL
                    RectangleF urlRect = new RectangleF(textRect.X - itemBounds.X, currentY, sizeUrl.Width, sizeUrl.Height);
                    // Thêm vùng URL vào danh sách
                    urlRegions.Add(Tuple.Create(urlRect, url));
                }

                currentY += sizeUrl.Height; // Tăng vị trí Y theo chiều cao của URL
                currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí hiện tại
            }
        }

        // Trả về danh sách các vùng URL
        return urlRegions;
    }

    // Xử lý sự kiện di chuyển chuột trên ListBox tin nhắn
    private void LbPrivateChatMessages_MouseMove(object sender, MouseEventArgs e)
    {
        ListBox listBox = sender as ListBox;
        if (listBox == null) return; // Đảm bảo đối tượng sender là ListBox

        Debug.WriteLine($"[GỠ LỖI MouseMove] Di chuyển chuột tại: X={e.X}, Y={e.Y}");

        // Lấy chỉ số mục tại vị trí chuột
        int index = listBox.IndexFromPoint(e.Location);
        Debug.WriteLine($"[GỠ LỖI MouseMove] Chỉ số: {index}, Vị trí: {e.Location}");

        // Kiểm tra chỉ số có hợp lệ không
        if (index < 0 || index >= listBox.Items.Count)
        {
            listBox.Cursor = Cursors.Default; // Đặt con trỏ mặc định
            Debug.WriteLine($"[GỠ LỖI MouseMove] Không có mục nào tại vị trí, Con trỏ được đặt thành Mặc định");
            return;
        }

        // Kiểm tra mục tại chỉ số có phải là tin nhắn và có chứa URL không
        if (listBox.Items[index] is ChatMessage message && message.Urls != null && message.Urls.Any())
        {
            Rectangle itemBounds = listBox.GetItemRectangle(index); // Lấy ranh giới của mục
            PointF relativeLocation = new PointF(e.X - itemBounds.X, e.Y - itemBounds.Y); // Tính vị trí tương đối
            float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2); // Chiều rộng văn bản tối đa
            if (maxTextWidth < 1) maxTextWidth = 1;

            Debug.WriteLine($"[GỠ LỖI MouseMove] Chỉ số: {index}, Ranh giới mục: {itemBounds}, Vị trí tương đối: {relativeLocation}");

            // Sử dụng đối tượng Graphics để đo và tính toán
            using (Graphics g = listBox.CreateGraphics())
            {
                // Tính toán các vùng URL
                var urlRegions = CalculateUrlRegions(message, itemBounds, g, listBox.Font, maxTextWidth);
                bool isOverUrl = false; // Biến kiểm tra chuột có di chuyển qua URL không

                // Lặp qua các vùng URL đã tính
                foreach (var urlInfo in urlRegions)
                {
                    RectangleF urlRect = urlInfo.Item1; // Lấy hình chữ nhật của URL
                    string url = urlInfo.Item2; // Lấy URL
                    Debug.WriteLine($"[GỠ LỖI MouseMove] Kiểm tra URL: '{url}', Hình chữ nhật: {urlRect}");
                    // Kiểm tra chuột có nằm trong vùng URL không
                    if (urlRect.Contains(relativeLocation))
                    {
                        isOverUrl = true; // Đặt biến thành true
                        Debug.WriteLine($"[GỠ LỖI MouseMove] Chuột di chuyển qua URL: {url}, Hình chữ nhật: {urlRect}");
                        break; // Thoát vòng lặp vì đã tìm thấy URL
                    }
                }
                // Đặt con trỏ chuột thành Hand nếu di chuyển qua URL, ngược lại là Default
                listBox.Cursor = isOverUrl ? Cursors.Hand : Cursors.Default;
                Debug.WriteLine($"[GỠ LỖI MouseMove] Con trỏ được đặt thành: {(isOverUrl ? "Bàn tay" : "Mặc định")}");
            }
        }
        else
        {
            listBox.Cursor = Cursors.Default; // Nếu không phải tin nhắn hoặc không có URL, đặt con trỏ mặc định
            Debug.WriteLine($"[GỠ LỖI MouseMove] Không có Tin nhắn hợp lệ hoặc URL, Con trỏ được đặt thành Mặc định");
        }
    }

    // Xử lý sự kiện khi TextBox nhập tin nhắn nhận focus
    private void TxtMessageInput_GotFocus(object sender, EventArgs e)
    {
        // Nếu văn bản là mặc định, xóa văn bản và đổi màu chữ thành đen
        if (txtMessageInput.Text == "Nhập tin nhắn ...")
        {
            txtMessageInput.Text = "";
            txtMessageInput.ForeColor = Color.Black;
        }
    }

    // Xử lý sự kiện khi TextBox nhập tin nhắn mất focus
    private void TxtMessageInput_LostFocus(object sender, EventArgs e)
    {
        // Nếu văn bản rỗng hoặc chỉ chứa khoảng trắng, đặt lại văn bản mặc định và màu chữ xám
        if (string.IsNullOrWhiteSpace(txtMessageInput.Text))
        {
            txtMessageInput.Text = "Nhập tin nhắn ...";
            txtMessageInput.ForeColor = Color.Gray;
        }
    }

    // Xử lý sự kiện khi văn bản trong TextBox nhập tin nhắn thay đổi
    private void TxtMessageInput_TextChanged(object sender, EventArgs e)
    {
        // Tính số dòng hiện tại trong TextBox
        int lineCount = txtMessageInput.GetLineFromCharIndex(txtMessageInput.TextLength) + 1;
        // Hiển thị thanh cuộn dọc nếu số dòng lớn hơn 3, ngược lại ẩn đi
        txtMessageInput.ScrollBars = lineCount > 3 ? ScrollBars.Vertical : ScrollBars.None;
    }

    // Xử lý sự kiện nhấn phím trong TextBox nhập tin nhắn
    private void TxtMessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        // Nếu nhấn phím Enter mà không giữ Shift
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.Handled = true; // Đánh dấu sự kiện đã được xử lý
            e.SuppressKeyPress = true; // Ngăn chặn sự kiện KeyPress tiếp theo
            SendMessage(); // Gửi tin nhắn
        }
        // Nếu nhấn phím Enter và giữ Shift
        else if (e.KeyCode == Keys.Enter && e.Shift)
        {
            int caretPosition = txtMessageInput.SelectionStart; // Lấy vị trí con trỏ
            // Chèn ký tự xuống dòng tại vị trí con trỏ
            txtMessageInput.Text = txtMessageInput.Text.Insert(caretPosition, Environment.NewLine);
            // Cập nhật vị trí con trỏ sau khi chèn
            txtMessageInput.SelectionStart = caretPosition + Environment.NewLine.Length;
            txtMessageInput.SelectionLength = 0; // Bỏ chọn văn bản
            txtMessageInput.ScrollToCaret(); // Cuộn đến vị trí con trỏ
            e.Handled = true; // Đánh dấu sự kiện đã được xử lý
            e.SuppressKeyPress = true; // Ngăn chặn sự kiện KeyPress tiếp theo
        }
    }

    // Xử lý sự kiện khi menu ngữ cảnh mở ra
    private void MessageContextMenu_Opening(object sender, CancelEventArgs e)
    {
        // Kích hoạt hoặc vô hiệu hóa mục "Sao chép" dựa trên việc có mục nào được chọn trong ListBox và mục đó có phải là ChatMessage không
        copyMessageMenuItem.Enabled = lbPrivateChatMessages.SelectedIndex >= 0 && lbPrivateChatMessages.SelectedItem is ChatMessage;
        // Nếu mục "Sao chép" không được kích hoạt, hủy bỏ việc mở menu ngữ cảnh
        if (!copyMessageMenuItem.Enabled)
        {
            e.Cancel = true;
        }
    }

    // Xử lý sự kiện khi click vào mục "Sao chép" trong menu ngữ cảnh
    private void CopyMessageMenuItem_Click(object sender, EventArgs e)
    {
        // Nếu mục được chọn là ChatMessage
        if (lbPrivateChatMessages.SelectedItem is ChatMessage selectedMessage)
        {
            Clipboard.SetText(selectedMessage.Content); // Sao chép nội dung tin nhắn vào clipboard
                                                        // MessageBox.Show("Đã sao chép tin nhắn vào clipboard.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information); // Thông báo sau khi bấm sao chép tin nhắn (đã được comment lại)
        }
    }

    // Xử lý sự kiện nhấn chuột trên ListBox tin nhắn
    private void LbPrivateChatMessages_MouseDown(object sender, MouseEventArgs e)
    {
        ListBox listBox = sender as ListBox;
        if (listBox == null) return; // Đảm bảo đối tượng sender là ListBox

        Debug.WriteLine($"[GỠ LỖI MouseDown] Nhấp chuột tại: X={e.X}, Y={e.Y}");

        // Nếu nhấn chuột phải
        if (e.Button == MouseButtons.Right)
        {
            // Lấy chỉ số mục tại vị trí chuột
            int index = listBox.IndexFromPoint(e.Location);
            Debug.WriteLine($"[GỠ LỖI MouseDown] Phát hiện nhấp chuột phải, Chỉ số: {index}");
            // Nếu tìm thấy mục tại vị trí nhấp
            if (index != ListBox.NoMatches)
            {
                listBox.SelectedIndex = index; // Chọn mục đó
            }
            else
            {
                listBox.SelectedIndex = -1; // Bỏ chọn tất cả các mục
            }
        }
        // Nếu nhấn chuột trái
        else if (e.Button == MouseButtons.Left)
        {
            // Lấy chỉ số mục tại vị trí chuột
            int index = listBox.IndexFromPoint(e.Location);
            Debug.WriteLine($"[GỠ LỖI MouseDown] Phát hiện nhấp chuột trái, Chỉ số: {index}, Vị trí: {e.Location}");

            // Kiểm tra chỉ số có hợp lệ và nằm trong giới hạn các mục không
            if (index != ListBox.NoMatches && index < listBox.Items.Count)
            {
                object item = listBox.Items[index]; // Lấy mục tại chỉ số đó
                // Kiểm tra mục có phải là ChatMessage và có chứa URL không
                if (item is ChatMessage message && message.Urls != null && message.Urls.Any())
                {
                    Rectangle itemBounds = listBox.GetItemRectangle(index); // Lấy ranh giới của mục
                    PointF relativeClickLocation = new PointF(e.X - itemBounds.X, e.Y - itemBounds.Y); // Tính vị trí nhấp chuột tương đối
                    float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2); // Chiều rộng văn bản tối đa
                    if (maxTextWidth < 1) maxTextWidth = 1;

                    Debug.WriteLine($"[GỠ LỖI MouseDown] Chỉ số: {index}, Ranh giới mục: {itemBounds}, Vị trí nhấp tương đối: {relativeClickLocation}");

                    // Sử dụng đối tượng Graphics để đo và tính toán
                    using (Graphics g = listBox.CreateGraphics())
                    {
                        // Tính toán các vùng URL
                        var urlRegions = CalculateUrlRegions(message, itemBounds, g, listBox.Font, maxTextWidth);
                        // Lặp qua các vùng URL đã tính
                        foreach (var urlInfo in urlRegions)
                        {
                            RectangleF urlRect = urlInfo.Item1; // Lấy hình chữ nhật của URL
                            string url = urlInfo.Item2; // Lấy URL
                            Debug.WriteLine($"[GỠ LỖI MouseDown] Kiểm tra URL: '{url}', Hình chữ nhật: {urlRect}");
                            // Kiểm tra vị trí nhấp chuột có nằm trong vùng URL không
                            if (urlRect.Contains(relativeClickLocation))
                            {
                                Debug.WriteLine($"[GỠ LỖI MouseDown] Nhấp trúng URL: {url}, Hình chữ nhật: {urlRect}");
                                try
                                {
                                    // Thử mở URL
                                    if (!string.IsNullOrEmpty(url))
                                    {
                                        // Thêm tiền tố "https://" nếu chưa có
                                        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            url = "https://" + url;
                                        }
                                        // Tạo ProcessStartInfo để mở URL bằng ứng dụng mặc định
                                        ProcessStartInfo psi = new ProcessStartInfo
                                        {
                                            FileName = url,
                                            UseShellExecute = true // Sử dụng shell để mở
                                        };
                                        Process.Start(psi); // Mở process
                                        Debug.WriteLine($"[GỠ LỖI MouseDown] Đã mở URL: {url}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[GỠ LỖI MouseDown] Lỗi khi mở URL: {ex.Message}");
                                    // Hiển thị thông báo lỗi nếu không mở được URL
                                    MessageBox.Show($"Không thể mở URL: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                break; // Thoát vòng lặp vì đã xử lý nhấp chuột
                            }
                            else
                            {
                                Debug.WriteLine($"[GỠ LỖI MouseDown] Nhấp không trúng URL: {url}, Hình chữ nhật: {urlRect}");
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"[GỠ LỖI MouseDown] Không có Tin nhắn hợp lệ hoặc URL tại chỉ số: {index}");
                }
            }
            else
            {
                Debug.WriteLine($"[GỠ LỖI MouseDown] Không có mục nào tại vị trí nhấp, Chỉ số: {index}");
            }
        }
    }

    // Phương thức thêm tin nhắn vào ListBox hiển thị
    public void AddMessage(ChatMessage message)
    {
        // Loại bỏ ký tự xuống dòng hoặc ký tự không mong muốn ở cuối nội dung tin nhắn
        message.Content = message.Content.TrimEnd('\n', '\r');
        Debug.WriteLine($"[DEBUG AddMessage] Nội dung tin nhắn sau khi làm sạch: '{message.Content}'");

        // Đảm bảo URL được phát hiện cho các tin nhắn nhận được (nếu chưa có)
        if (!message.IsSentByMe && (message.Urls == null || !message.Urls.Any()))
        {
            message.Urls = FindUrlsInText(message.Content); // Tìm URL trong nội dung
            Debug.WriteLine($"[DEBUG AddMessage] Đã phát hiện URL cho tin nhắn đã nhận: {message.Urls.Count} URL được tìm thấy");
        }

        // Kiểm tra xem có cần Invoke để cập nhật giao diện từ một luồng khác không
        if (lbPrivateChatMessages.InvokeRequired)
        {
            // Sử dụng Invoke để thực hiện thao tác trên luồng giao diện
            lbPrivateChatMessages.Invoke((MethodInvoker)delegate
            {
                lbPrivateChatMessages.Items.Add(message); // Thêm tin nhắn vào ListBox
                // Cuộn xuống tin nhắn cuối cùng
                if (lbPrivateChatMessages.Items.Count > 0)
                {
                    lbPrivateChatMessages.TopIndex = lbPrivateChatMessages.Items.Count - 1;
                }
                lbPrivateChatMessages.Invalidate(); // Vẽ lại ListBox
            });
        }
        else
        {
            // Nếu không cần Invoke, thực hiện trực tiếp
            lbPrivateChatMessages.Items.Add(message); // Thêm tin nhắn vào ListBox
            // Cuộn xuống tin nhắn cuối cùng
            if (lbPrivateChatMessages.Items.Count > 0)
            {
                lbPrivateChatMessages.TopIndex = lbPrivateChatMessages.Items.Count - 1;
            }
            lbPrivateChatMessages.Invalidate(); // Vẽ lại ListBox
        }
        Debug.WriteLine($"[CHAT] {message.SenderName}: {message.Content}"); // Ghi log chat
    }

    // Phương thức lưu tin nhắn vào tệp lịch sử
    public void SaveMessageToFile(ChatMessage message)
    {
        try
        {
            // Lấy đường dẫn thư mục lịch sử chat chung
            string commonHistoryDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChatHistory");
            // Tạo thư mục nếu chưa tồn tại
            if (!Directory.Exists(commonHistoryDirectory))
            {
                Directory.CreateDirectory(commonHistoryDirectory);
            }
            // Lấy tên tệp lịch sử chat riêng tư dựa trên tên hai người dùng
            string filePath = Path.Combine(commonHistoryDirectory, GetPrivateChatHistoryFileName(myName, recipientUserName));
            // Định dạng tin nhắn để lưu vào tệp
            string formattedMessage = $"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}] {message.SenderName}: {message.Content}";
            // Ghi thêm dòng tin nhắn vào cuối tệp
            File.AppendAllText(filePath, formattedMessage + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // Hiển thị thông báo lỗi nếu có vấn đề khi lưu tệp
            MessageBox.Show($"Lỗi khi lưu lịch sử trò chuyện: {ex.Message}", "Lỗi tệp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Phương thức tải lịch sử chat từ tệp
    private void LoadHistoryFromFile()
    {
        try
        {
            // Lấy đường dẫn thư mục lịch sử chat chung
            string commonHistoryDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChatHistory");
            // Lấy đường dẫn tệp lịch sử chat riêng tư
            string filePath = Path.Combine(commonHistoryDirectory, GetPrivateChatHistoryFileName(myName, recipientUserName));
            // Kiểm tra nếu tệp tồn tại
            if (File.Exists(filePath))
            {
                // Đọc tất cả các dòng từ tệp
                string[] lines = File.ReadAllLines(filePath);
                // Lặp qua từng dòng
                foreach (string line in lines)
                {
                    try
                    {
                        // Phân tích cú pháp dòng để lấy dấu thời gian, tên người gửi và nội dung
                        int timestampEndIndex = line.IndexOf(']');
                        int senderNameEndIndex = line.IndexOf(':', timestampEndIndex + 2);
                        // Kiểm tra định dạng dòng hợp lệ
                        if (timestampEndIndex > 0 && senderNameEndIndex > timestampEndIndex)
                        {
                            string timestampString = line.Substring(1, timestampEndIndex - 1); // Lấy chuỗi dấu thời gian
                            string senderName = line.Substring(timestampEndIndex + 2, senderNameEndIndex - (timestampEndIndex + 2)).Trim(); // Lấy tên người gửi
                            string content = line.Substring(senderNameEndIndex + 2).Trim(); // Lấy nội dung tin nhắn
                            // Chuyển đổi chuỗi dấu thời gian sang đối tượng DateTime
                            if (DateTime.TryParseExact(timestampString, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out DateTime timestamp))
                            {
                                // Tạo đối tượng ChatMessage
                                var loadedMessage = new ChatMessage
                                {
                                    SenderName = senderName, // Gán tên người gửi
                                    Content = content, // Gán nội dung
                                    Timestamp = timestamp, // Gán dấu thời gian
                                    IsSentByMe = (senderName == myName), // Kiểm tra xem tin nhắn có phải do tôi gửi không
                                    Urls = FindUrlsInText(content) // Phát hiện URL cho các tin nhắn đã tải
                                };
                                // Thêm tin nhắn đã tải vào ListBox
                                AddMessage(loadedMessage);
                            }
                        }
                    }
                    catch (Exception lineEx)
                    {
                        // Ghi log nếu có lỗi khi đọc một dòng cụ thể
                        Console.WriteLine($"Lỗi khi đọc lịch sử chat dòng: {lineEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Ghi log nếu có lỗi khi tải lịch sử chat
            Console.WriteLine($"Lỗi khi tải lịch sử trò chuyện riêng tư: {ex.Message}");
        }
    }

    // Phương thức tạo tên tệp lịch sử chat riêng tư dựa trên tên hai người dùng (đảm bảo thứ tự tên để tên tệp là duy nhất)
    private static string GetPrivateChatHistoryFileName(string user1, string user2)
    {
        string[] users = { user1, user2 };
        Array.Sort(users); // Sắp xếp tên người dùng
        return $"{users[0]}_{users[1]}.txt"; // Trả về tên tệp theo định dạng user1_user2.txt
    }

    // Phương thức gửi tin nhắn (bất đồng bộ)
    private async void SendMessage()
    {
        // Lấy nội dung tin nhắn từ TextBox và loại bỏ khoảng trắng ở đầu và cuối
        string messageContent = txtMessageInput.Text.Trim();
        // Kiểm tra nội dung tin nhắn có hợp lệ không
        if (!string.IsNullOrWhiteSpace(messageContent) && messageContent != "Nhập tin nhắn ...")
        {
            // Tạo đối tượng ChatMessage cho tin nhắn gửi đi
            var message = new ChatMessage
            {
                SenderName = myName, // Tên người gửi (tôi)
                Content = messageContent, // Nội dung tin nhắn
                Timestamp = DateTime.Now, // Dấu thời gian hiện tại
                IsSentByMe = true, // Đánh dấu tin nhắn là do tôi gửi
                Urls = FindUrlsInText(messageContent) // Phát hiện URL trong nội dung
            };

            AddMessage(message); // Thêm tin nhắn vào ListBox hiển thị
            SaveMessageToFile(message); // Lưu tin nhắn vào tệp lịch sử

            // Kiểm tra kết nối TCPClient
            if (tcpClient.Connected)
            {
                try
                {
                    // Định dạng tin nhắn để gửi qua mạng
                    string formattedMessage = $"{myName}|{messageContent}\n";
                    Debug.WriteLine($"[DEBUG SendMessage] Gửi: '{formattedMessage}' (Độ dài: {formattedMessage.Length})");
                    // Chuyển đổi chuỗi thành mảng byte
                    byte[] buffer = Encoding.UTF8.GetBytes(formattedMessage);
                    Debug.WriteLine($"[DEBUG SendMessage] Kích thước bộ đệm: {buffer.Length} bytes");
                    NetworkStream stream = tcpClient.GetStream(); // Lấy luồng mạng
                    await stream.WriteAsync(buffer, 0, buffer.Length); // Ghi dữ liệu bất đồng bộ
                    await stream.FlushAsync(); // Xả bộ đệm bất đồng bộ
                    Debug.WriteLine($"[DEBUG SendMessage] Gửi tin nhắn thành công: '{formattedMessage}'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DEBUG SendMessage] Lỗi khi gửi tin nhắn: {ex.Message}");
                    // Hiển thị thông báo lỗi nếu gửi tin nhắn không thành công
                    MessageBox.Show($"Lỗi gửi tin nhắn: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                Debug.WriteLine($"[DEBUG SendMessage] TcpClient không được kết nối");
                // Hiển thị thông báo lỗi nếu mất kết nối với server
                MessageBox.Show("Không thể gửi tin nhắn: Mất kết nối với server.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            txtMessageInput.Clear(); // Xóa nội dung trong TextBox nhập tin nhắn
        }
    }

    // Xử lý sự kiện đo kích thước của một mục trong ListBox
    private void LbPrivateChatMessages_MeasureItem(object sender, MeasureItemEventArgs e)
    {
        ListBox listBox = sender as ListBox;
        if (listBox == null) return; // Đảm bảo đối tượng sender là ListBox

        // Kiểm tra chỉ số mục hợp lệ
        if (e.Index < 0 || e.Index >= listBox.Items.Count)
        {
            e.ItemHeight = 20; // Chiều cao mặc định nếu chỉ số không hợp lệ
            return;
        }

        object item = listBox.Items[e.Index]; // Lấy mục tại chỉ số đó
        // Kiểm tra mục có phải là ChatMessage không
        if (!(item is ChatMessage message))
        {
            e.ItemHeight = 20; // Chiều cao mặc định nếu không phải ChatMessage
            return;
        }

        int timestampMargin = 8; // Khoảng cách lề cho dấu thời gian
        float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2); // Chiều rộng văn bản tối đa trong bong bóng
        if (maxTextWidth < 1) maxTextWidth = 1;

        float totalTextHeight = 0; // Tổng chiều cao của văn bản
        float totalTextWidth = 0; // Tổng chiều rộng của văn bản (được tính toán)
        float measuredSenderNameHeight = 0; // Chiều cao đo được của tên người gửi
        float measuredTimestampHeight = 0; // Chiều cao đo được của dấu thời gian

        // Sử dụng đối tượng Graphics và StringFormat để đo kích thước
        using (Graphics g = listBox.CreateGraphics())
        using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
        {
            sf.Trimming = StringTrimming.Character; // Cắt ký tự khi vượt quá kích thước

            // Nếu nội dung tin nhắn không rỗng
            if (!string.IsNullOrEmpty(message.Content))
            {
                // Nếu không có URL trong tin nhắn
                if (message.Urls == null || !message.Urls.Any())
                {
                    // Đo kích thước toàn bộ nội dung tin nhắn
                    SizeF textSizeF = g.MeasureString(message.Content, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf);
                    totalTextHeight = textSizeF.Height; // Lấy chiều cao
                    totalTextWidth = textSizeF.Width; // Lấy chiều rộng
                    Debug.WriteLine($"[MeasureItem] Nội dung: '{message.Content}', Chiều rộng đo: {totalTextWidth}, Chiều cao đo: {totalTextHeight}");
                }
                else // Nếu có URL trong tin nhắn
                {
                    int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi
                    var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL theo vị trí
                    // Lặp qua từng thông tin URL
                    foreach (var urlInfo in sortedUrls)
                    {
                        string url = urlInfo.Item1;
                        int urlStartIndex = urlInfo.Item2;
                        int urlLength = urlInfo.Item3;

                        // Đo văn bản trước URL
                        if (urlStartIndex > currentTextIndex)
                        {
                            string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                            if (!string.IsNullOrEmpty(textBeforeUrl))
                            {
                                SizeF sizeBefore = g.MeasureString(textBeforeUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf);
                                totalTextHeight += sizeBefore.Height; // Cộng dồn chiều cao
                                totalTextWidth = Math.Max(totalTextWidth, sizeBefore.Width); // Cập nhật chiều rộng lớn nhất
                                Debug.WriteLine($"[MeasureItem] Văn bản trước URL: '{textBeforeUrl}', Chiều rộng: {sizeBefore.Width}, Chiều cao: {sizeBefore.Height}");
                            }
                        }

                        // Đo URL
                        SizeF sizeUrl = g.MeasureString(url, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf);
                        totalTextHeight += sizeUrl.Height; // Cộng dồn chiều cao của URL
                        totalTextWidth = Math.Max(totalTextWidth, sizeUrl.Width); // Cập nhật chiều rộng lớn nhất
                        Debug.WriteLine($"[MeasureItem] URL: '{url}', Chiều rộng: {sizeUrl.Width}, Chiều cao: {sizeUrl.Height}");
                        currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí hiện tại
                    }

                    // Đo văn bản sau URL cuối cùng
                    if (currentTextIndex < message.Content.Length)
                    {
                        string textAfterUrl = message.Content.Substring(currentTextIndex);
                        if (!string.IsNullOrEmpty(textAfterUrl))
                        {
                            SizeF sizeAfter = g.MeasureString(textAfterUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf);
                            totalTextHeight += sizeAfter.Height; // Cộng dồn chiều cao
                            totalTextWidth = Math.Max(totalTextWidth, sizeAfter.Width); // Cập nhật chiều rộng lớn nhất
                            Debug.WriteLine($"[MeasureItem] Văn bản sau URL: '{textAfterUrl}', Chiều rộng: {sizeAfter.Width}, Chiều cao: {sizeAfter.Height}");
                        }
                    }
                }
            }

            // Đo chiều cao tên người gửi nếu tin nhắn không phải do tôi gửi và tên người gửi không rỗng
            if (!message.IsSentByMe && !string.IsNullOrEmpty(message.SenderName))
            {
                using (Font senderNameFont = new Font(listBox.Font.FontFamily, listBox.Font.Size * 0.85f, FontStyle.Bold))
                {
                    measuredSenderNameHeight = g.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic).Height;
                }
            }

            // Đo chiều cao dấu thời gian
            using (Font timestampFont = new Font(listBox.Font.FontFamily, listBox.Font.Size * 0.7f))
            {
                measuredTimestampHeight = g.MeasureString(message.Timestamp.ToString("HH:mm"), timestampFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic).Height;
            }
        }

        // Tính toán chiều cao nội dung bong bóng (văn bản + đệm)
        float bubbleContentHeight = totalTextHeight + MessageBubblePadding * 2;
        // Chiều cao hiệu quả của bong bóng (tối thiểu là 20)
        float effectiveBubbleHeight = Math.Max(20, bubbleContentHeight);

        int totalHeight = 0; // Tổng chiều cao của mục trong ListBox
        // Tính tổng chiều cao dựa trên người gửi
        if (!message.IsSentByMe) // Nếu tin nhắn nhận được
        {
            // Khoảng cách dọc + chiều cao tên người gửi + khoảng cách dọc + chiều cao bong bóng + lề dấu thời gian + chiều cao dấu thời gian + khoảng cách dọc
            totalHeight = VerticalSpacing + (int)Math.Ceiling(measuredSenderNameHeight) + VerticalSpacing + (int)Math.Ceiling(effectiveBubbleHeight) + timestampMargin + (int)Math.Ceiling(measuredTimestampHeight) + VerticalSpacing;
        }
        else // Nếu tin nhắn gửi đi
        {
            // Khoảng cách dọc + chiều cao bong bóng + lề dấu thời gian + chiều cao dấu thời gian + khoảng cách dọc
            totalHeight = VerticalSpacing + (int)Math.Ceiling(effectiveBubbleHeight) + timestampMargin + (int)Math.Ceiling(measuredTimestampHeight) + VerticalSpacing;
        }

        int minHeight = AvatarSize + VerticalSpacing * 2; // Chiều cao tối thiểu dựa trên kích thước avatar
        // Đặt chiều cao mục là giá trị lớn nhất giữa tổng chiều cao tính toán và chiều cao tối thiểu
        e.ItemHeight = Math.Max(totalHeight, minHeight);
        if (e.ItemHeight < 1) e.ItemHeight = 1; // Đảm bảo chiều cao không nhỏ hơn 1
    }

    // Xử lý sự kiện vẽ một mục trong ListBox
    private void LbPrivateChatMessages_DrawItem(object sender, DrawItemEventArgs e)
    {
        ListBox listBox = sender as ListBox;
        if (listBox == null) return; // Đảm bảo đối tượng sender là ListBox

        // Kiểm tra chỉ số mục hợp lệ
        if (e.Index < 0 || e.Index >= listBox.Items.Count)
            return;

        object item = listBox.Items[e.Index]; // Lấy mục tại chỉ số đó
        // Kiểm tra mục có phải là ChatMessage không
        if (!(item is ChatMessage message))
        {
            // Nếu không phải ChatMessage, vẽ nền và văn bản mặc định
            e.DrawBackground();
            e.Graphics.DrawString(item.ToString(), e.Font, new SolidBrush(e.ForeColor), e.Bounds, StringFormat.GenericDefault);
            e.DrawFocusRectangle();
            return;
        }

        Debug.WriteLine($"[DEBUG DrawItem] Nội dung tin nhắn: '{message.Content}'");

        // Vẽ nền của mục
        if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
        {
            // Vẽ nền màu khi mục được chọn
            using (SolidBrush backBrush = new SolidBrush(SelectedItemColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }
        }
        else
        {
            // Vẽ nền màu mặc định
            using (SolidBrush backBrush = new SolidBrush(e.BackColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }
        }

        // Xác định màu bong bóng tin nhắn dựa trên người gửi
        Color bubbleColor = message.IsSentByMe ? SentMessageColor : ReceivedMessageColor;
        // Cấu hình chế độ làm mịn và hiển thị văn bản cho Graphics
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2); // Chiều rộng văn bản tối đa trong bong bóng
        if (maxTextWidth < 1) maxTextWidth = 1;

        float totalTextHeight = 0; // Tổng chiều cao văn bản
        float totalTextWidth = 0; // Tổng chiều rộng văn bản (được tính toán)
        // Nếu nội dung tin nhắn không rỗng
        if (!string.IsNullOrEmpty(message.Content))
        {
            // Sử dụng StringFormat để đo kích thước
            using (StringFormat sfMeasure = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
            {
                sfMeasure.Trimming = StringTrimming.Character; // Cắt ký tự khi vượt quá kích thước
                // Nếu không có URL
                if (message.Urls == null || !message.Urls.Any())
                {
                    // Đo kích thước toàn bộ nội dung
                    SizeF textSizeF = e.Graphics.MeasureString(message.Content, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure);
                    totalTextHeight = textSizeF.Height; // Lấy chiều cao
                    totalTextWidth = textSizeF.Width; // Lấy chiều rộng
                }
                else // Nếu có URL
                {
                    int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi
                    var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL
                    // Lặp qua từng thông tin URL
                    foreach (var urlInfo in sortedUrls)
                    {
                        string url = urlInfo.Item1;
                        int urlStartIndex = urlInfo.Item2;
                        int urlLength = urlInfo.Item3;

                        // Đo văn bản trước URL
                        if (urlStartIndex > currentTextIndex)
                        {
                            string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                            if (!string.IsNullOrEmpty(textBeforeUrl))
                            {
                                SizeF sizeBefore = e.Graphics.MeasureString(textBeforeUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure);
                                totalTextHeight += sizeBefore.Height; // Cộng dồn chiều cao
                                totalTextWidth = Math.Max(totalTextWidth, sizeBefore.Width); // Cập nhật chiều rộng lớn nhất
                            }
                        }

                        // Đo URL
                        SizeF sizeUrl = e.Graphics.MeasureString(url, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure);
                        totalTextHeight += sizeUrl.Height; // Cộng dồn chiều cao
                        totalTextWidth = Math.Max(totalTextWidth, sizeUrl.Width); // Cập nhật chiều rộng lớn nhất
                        currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí
                    }

                    // Đo văn bản sau URL cuối cùng
                    if (currentTextIndex < message.Content.Length)
                    {
                        string textAfterUrl = message.Content.Substring(currentTextIndex);
                        if (!string.IsNullOrEmpty(textAfterUrl))
                        {
                            SizeF sizeAfter = e.Graphics.MeasureString(textAfterUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure);
                            totalTextHeight += sizeAfter.Height; // Cộng dồn chiều cao
                            totalTextWidth = Math.Max(totalTextWidth, sizeAfter.Width); // Cập nhật chiều rộng lớn nhất
                        }
                    }
                }
            }
        }

        // Tính toán kích thước bong bóng tin nhắn
        int bubbleWidth = Math.Min(MaxBubbleWidth, Math.Max(20, (int)totalTextWidth + MessageBubblePadding * 2));
        Size bubbleSize = new Size(bubbleWidth, Math.Max(20, (int)(totalTextHeight + MessageBubblePadding * 2)));
        Rectangle bubbleRect, avatarRect, textRect, senderNameRect = Rectangle.Empty, timestampRect; // Khai báo các hình chữ nhật

        // Xác định vị trí các thành phần dựa trên người gửi
        if (message.IsSentByMe) // Nếu tin nhắn do tôi gửi
        {
            // Vị trí avatar (bên phải)
            avatarRect = new Rectangle(e.Bounds.Right - AvatarSize - 10, e.Bounds.Top + VerticalSpacing, AvatarSize, AvatarSize);
            // Vị trí bong bóng (bên trái avatar)
            bubbleRect = new Rectangle(avatarRect.Left - AvatarMargin - bubbleSize.Width, e.Bounds.Top + VerticalSpacing, bubbleSize.Width, bubbleSize.Height);
            // Vị trí văn bản bên trong bong bóng (thêm lề nhỏ)
            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth + 5, (int)totalTextHeight + 5);

            // Đo kích thước và xác định vị trí dấu thời gian
            SizeF timestampSizeF = e.Graphics.MeasureString(message.Timestamp.ToString("HH:mm"), new Font(e.Font.FontFamily, e.Font.Size * 0.7f));
            Size timestampSize = Size.Ceiling(timestampSizeF);
            timestampRect = new Rectangle(bubbleRect.Right - timestampSize.Width, bubbleRect.Bottom + 8, timestampSize.Width, timestampSize.Height);
        }
        else // Nếu tin nhắn do người khác gửi
        {
            // Vị trí avatar (bên trái)
            avatarRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + VerticalSpacing, AvatarSize, AvatarSize);
            int bubbleTopY = e.Bounds.Top + VerticalSpacing; // Vị trí Y ban đầu của bong bóng

            // Nếu có tên người gửi, đo kích thước và xác định vị trí
            if (!string.IsNullOrEmpty(message.SenderName))
            {
                using (Font senderNameFont = new Font(e.Font.FontFamily, e.Font.Size * 0.85f, FontStyle.Bold))
                {
                    SizeF senderNameSizeF = e.Graphics.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic);
                    Size senderNameSize = Size.Ceiling(senderNameSizeF);
                    senderNameRect = new Rectangle(avatarRect.Right + AvatarMargin, e.Bounds.Top + VerticalSpacing, senderNameSize.Width, 14);
                    bubbleTopY = senderNameRect.Bottom + VerticalSpacing; // Cập nhật vị trí Y của bong bóng
                }
            }

            // Vị trí bong bóng (bên phải avatar)
            bubbleRect = new Rectangle(avatarRect.Right + AvatarMargin, bubbleTopY, bubbleSize.Width, bubbleSize.Height);
            // Vị trí văn bản bên trong bong bóng (thêm lề nhỏ)
            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth + 5, (int)totalTextHeight + 5);

            // Đo kích thước và xác định vị trí dấu thời gian
            SizeF timestampSizeF = e.Graphics.MeasureString(message.Timestamp.ToString("HH:mm"), new Font(e.Font.FontFamily, e.Font.Size * 0.7f));
            Size timestampSize = Size.Ceiling(timestampSizeF);
            timestampRect = new Rectangle(bubbleRect.Left, bubbleRect.Bottom + 8, timestampSize.Width, timestampSize.Height);
        }

        // Vẽ bong bóng tin nhắn nếu kích thước hợp lệ
        if (bubbleRect.Width > 0 && bubbleRect.Height > 0)
        {
            // Tính bán kính bo tròn hiệu quả (không vượt quá một nửa chiều rộng hoặc chiều cao bong bóng)
            int effectiveRadius = Math.Min(MessageBubbleCornerRadius, Math.Min(bubbleRect.Width / 2, bubbleRect.Height / 2));
            // Tạo đường dẫn hình chữ nhật bo tròn
            using (GraphicsPath path = RoundedRectangle(bubbleRect, effectiveRadius))
            using (SolidBrush bubbleFillBrush = new SolidBrush(bubbleColor))
            {
                e.Graphics.FillPath(bubbleFillBrush, path); // Tô màu cho bong bóng
            }

            // Vẽ nội dung văn bản bên trong bong bóng nếu kích thước hợp lệ
            if (!string.IsNullOrEmpty(message.Content) && textRect.Width > 0 && textRect.Height > 0)
            {
                // Sử dụng StringFormat để vẽ
                using (StringFormat sfDraw = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
                {
                    sfDraw.Trimming = StringTrimming.Character; // Cắt ký tự khi vượt quá kích thước
                    sfDraw.Alignment = StringAlignment.Near; // Căn lề trái
                    sfDraw.LineAlignment = StringAlignment.Near; // Căn lề trên

                    // Nếu không có URL
                    if (message.Urls == null || !message.Urls.Any())
                    {
                        // Vẽ toàn bộ nội dung văn bản
                        using (SolidBrush defaultBrush = new SolidBrush(Color.Black))
                        {
                            e.Graphics.DrawString(message.Content, e.Font, defaultBrush, textRect, sfDraw);
                        }
                    }
                    else // Nếu có URL
                    {
                        float currentY = textRect.Y; // Vị trí Y hiện tại để vẽ văn bản
                        int currentTextIndex = 0; // Vị trí hiện tại trong chuỗi nội dung
                        var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList(); // Sắp xếp URL

                        // Lặp qua từng thông tin URL
                        foreach (var urlInfo in sortedUrls)
                        {
                            string url = urlInfo.Item1;
                            int urlStartIndex = urlInfo.Item2;
                            int urlLength = urlInfo.Item3;

                            // Vẽ văn bản trước URL
                            if (urlStartIndex > currentTextIndex)
                            {
                                string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                                if (!string.IsNullOrEmpty(textBeforeUrl))
                                {
                                    SizeF sizeBefore = e.Graphics.MeasureString(textBeforeUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfDraw);
                                    RectangleF beforeRect = new RectangleF(textRect.X, currentY, maxTextWidth, sizeBefore.Height); // Hình chữ nhật cho văn bản trước
                                    using (SolidBrush textBrush = new SolidBrush(Color.Black))
                                    {
                                        e.Graphics.DrawString(textBeforeUrl, e.Font, textBrush, beforeRect, sfDraw); // Vẽ văn bản trước
                                        Debug.WriteLine($"[DEBUG DrawItem] Vẽ văn bản trước URL: '{textBeforeUrl}', Rect: {beforeRect}");
                                    }
                                    currentY += sizeBefore.Height; // Cập nhật vị trí Y
                                }
                            }

                            // Vẽ URL (font gạch chân, màu xanh)
                            using (Font urlFont = new Font(e.Font, FontStyle.Underline))
                            using (SolidBrush urlBrush = new SolidBrush(Color.Blue))
                            {
                                SizeF sizeUrl = e.Graphics.MeasureString(url, urlFont, new SizeF(maxTextWidth, float.MaxValue), sfDraw);
                                RectangleF urlRect = new RectangleF(textRect.X, currentY, sizeUrl.Width, sizeUrl.Height); // Hình chữ nhật cho URL
                                e.Graphics.DrawString(url, urlFont, urlBrush, urlRect, sfDraw); // Vẽ URL
                                Debug.WriteLine($"[DEBUG DrawItem] Vẽ URL: '{url}', Rect: {urlRect}");
                                currentY += sizeUrl.Height; // Cập nhật vị trí Y
                            }

                            currentTextIndex = urlStartIndex + urlLength; // Cập nhật vị trí hiện tại
                        }

                        // Vẽ văn bản sau URL cuối cùng
                        if (currentTextIndex < message.Content.Length)
                        {
                            string textAfterUrl = message.Content.Substring(currentTextIndex);
                            if (!string.IsNullOrEmpty(textAfterUrl))
                            {
                                SizeF sizeAfter = e.Graphics.MeasureString(textAfterUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfDraw);
                                RectangleF afterRect = new RectangleF(textRect.X, currentY, maxTextWidth, sizeAfter.Height); // Hình chữ nhật cho văn bản sau
                                using (SolidBrush textBrush = new SolidBrush(Color.Black))
                                {
                                    e.Graphics.DrawString(textAfterUrl, e.Font, textBrush, afterRect, sfDraw); // Vẽ văn bản sau
                                    Debug.WriteLine($"[DEBUG DrawItem] Vẽ văn bản sau URL: '{textAfterUrl}', Rect: {afterRect}");
                                }
                            }
                        }
                    }
                }
            }
        }

        // Vẽ avatar (hình tròn với biểu tượng người)
        using (Brush avatarBrush = new SolidBrush(Color.FromArgb(200, 200, 200))) // Màu nền avatar
        using (Pen avatarPen = new Pen(Color.FromArgb(150, 150, 150), 1)) // Màu viền avatar
        {
            if (avatarRect.Width > 0 && avatarRect.Height > 0) // Nếu kích thước avatar hợp lệ
            {
                e.Graphics.FillEllipse(avatarBrush, avatarRect); // Tô màu hình tròn
                e.Graphics.DrawEllipse(avatarPen, avatarRect); // Vẽ viền hình tròn
                using (GraphicsPath humanPath = new GraphicsPath()) // Tạo đường dẫn cho biểu tượng người
                {
                    // Tính toán kích thước và vị trí đầu, thân biểu tượng
                    float headSize = AvatarSize * 0.4f;
                    float bodyWidth = AvatarSize * 0.7f;
                    float bodyHeight = AvatarSize * 0.5f;
                    float headX = avatarRect.X + (AvatarSize - headSize) / 2;
                    float headY = avatarRect.Y + AvatarSize * 0.15f;
                    float bodyX = avatarRect.X + (AvatarSize - bodyWidth) / 2;
                    float bodyY = avatarRect.Y + AvatarSize * 0.5f;
                    Rectangle headRect = new Rectangle((int)headX, (int)headY, Math.Max(1, (int)headSize), Math.Max(1, (int)headSize));
                    if (headRect.Width > 0 && headRect.Height > 0)
                    {
                        humanPath.AddEllipse(headRect); // Thêm hình elip cho đầu
                    }
                    RectangleF bodyArcRect = new RectangleF(bodyX, bodyY - bodyHeight / 2, bodyWidth, bodyHeight);
                    if (bodyArcRect.Width > 0 && bodyArcRect.Height > 0)
                    {
                        humanPath.AddArc(bodyArcRect, 0, 180); // Thêm cung tròn cho thân
                        humanPath.CloseFigure(); // Đóng hình
                    }
                    using (Brush humanBrush = new SolidBrush(Color.DarkGray)) // Màu biểu tượng người
                    {
                        if (humanPath.PointCount > 0)
                        {
                            e.Graphics.FillPath(humanBrush, humanPath); // Tô màu biểu tượng người
                        }
                    }
                }
            }
        }

        // Vẽ tên người gửi nếu tin nhắn không phải do tôi gửi và tên người gửi không rỗng
        if (!message.IsSentByMe && senderNameRect.Width > 0 && senderNameRect.Height > 0 && !string.IsNullOrEmpty(message.SenderName))
        {
            using (Font senderNameFont = new Font(e.Font.FontFamily, e.Font.Size * 0.85f, FontStyle.Bold)) // Font cho tên người gửi
            using (Brush senderNameBrush = new SolidBrush(Color.DimGray)) // Màu cho tên người gửi
            {
                e.Graphics.DrawString(message.SenderName, senderNameFont, senderNameBrush, senderNameRect, StringFormat.GenericTypographic); // Vẽ tên người gửi
            }
        }

        // Vẽ dấu thời gian
        using (Font timestampFont = new Font(e.Font.FontFamily, e.Font.Size * 0.7f)) // Font cho dấu thời gian
        {
            SizeF timestampSizeF = e.Graphics.MeasureString(message.Timestamp.ToString("HH:mm"), timestampFont, new SizeF(e.Bounds.Width, float.MaxValue), StringFormat.GenericTypographic); // Đo kích thước dấu thời gian
            Size timestampSize = Size.Ceiling(timestampSizeF); // Kích thước dấu thời gian
            if (timestampRect.Width > 0 && timestampRect.Height > 0) // Nếu kích thước dấu thời gian hợp lệ
            {
                e.Graphics.DrawString(message.Timestamp.ToString("HH:mm"), timestampFont, new SolidBrush(Color.Gray), timestampRect, StringFormat.GenericTypographic); // Vẽ dấu thời gian
            }
        }
    }

    // Phương thức tạo đường dẫn cho hình chữ nhật bo tròn
    private GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        GraphicsPath path = new GraphicsPath(); // Khởi tạo GraphicsPath
        if (radius <= 0) // Nếu bán kính nhỏ hơn hoặc bằng 0
        {
            path.AddRectangle(bounds); // Thêm hình chữ nhật thông thường
            return path; // Trả về đường dẫn
        }
        int diameter = radius * 2; // Tính đường kính
        Rectangle arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter); // Hình chữ nhật cho cung tròn

        path.AddArc(arc, 180, 90); // Thêm cung tròn phía trên bên trái
        arc.X = bounds.Right - diameter; // Di chuyển sang phải
        path.AddArc(arc, 270, 90); // Thêm cung tròn phía trên bên phải
        arc.Y = bounds.Bottom - diameter; // Di chuyển xuống dưới
        path.AddArc(arc, 0, 90); // Thêm cung tròn phía dưới bên phải
        arc.X = bounds.Left; // Di chuyển sang trái
        path.AddArc(arc, 90, 90); // Thêm cung tròn phía dưới bên trái
        path.CloseFigure(); // Đóng hình
        return path; // Trả về đường dẫn
    }
}
