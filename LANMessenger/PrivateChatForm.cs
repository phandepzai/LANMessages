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

public partial class PrivateChatForm : Form
{
    private string recipientUserName;
    private TcpClient tcpClient;
    private ListBox lbPrivateChatMessages;
    private TextBox txtMessageInput;
    private Panel pnlInputArea;
    private ContextMenuStrip messageContextMenu;
    private ToolStripMenuItem copyMessageMenuItem;
    private string myName;

    private const int MessageBubblePadding = 12;
    private const int MessageBubbleCornerRadius = 18;
    private const int AvatarSize = 32;
    private const int AvatarMargin = 8;
    private const int TimestampHeight = 15;
    private const int SenderNameHeight = 14;
    private const int VerticalSpacing = 8;
    private Color SentMessageColor = Color.FromArgb(136, 219, 136);
    private Color ReceivedMessageColor = Color.FromArgb(220, 220, 220);
    private Color BackgroundColor = Color.FromArgb(240, 242, 245);
    private Color ChatAreaBackgroundColor = Color.White;
    private Color SelectedItemColor = Color.FromArgb(250, 250, 250);
    private const int MaxBubbleWidth = 250;

    private List<Tuple<string, int, int>> FindUrlsInText(string text)
    {
        List<Tuple<string, int, int>> urls = new List<Tuple<string, int, int>>();
        Regex urlRegex = new Regex(@"\b(?:https?://|www\.)?[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*\.[a-zA-Z]{2,}(?:[/\w- .?%&=#]*)?\b", RegexOptions.IgnoreCase);
        MatchCollection matches = urlRegex.Matches(text);
        foreach (Match match in matches)
        {
            urls.Add(Tuple.Create(match.Value, match.Index, match.Length));
            Debug.WriteLine($"[DEBUG FindUrlsInText] Tìm thấy URL: '{match.Value}' tại vị trí {match.Index}, độ dài {match.Length}");
        }
        return urls;
    }

    public PrivateChatForm(string recipientUserName, TcpClient tcpClient, string myName)
    {
        this.recipientUserName = recipientUserName;
        this.tcpClient = tcpClient;
        this.myName = myName;
        InitializeMessageContextMenu();
        InitializeComponent();
        this.Text = $"Chat riêng với {recipientUserName}";
        try
        {
            this.Icon = new Icon(typeof(Messenger.ChatForm), "icon.ico");
        }
        catch { }
        LoadHistoryFromFile();
    }

    private void InitializeComponent()
    {
        this.lbPrivateChatMessages = new ListBox();
        this.txtMessageInput = new TextBox();
        this.pnlInputArea = new Panel();
        this.SuspendLayout();
        this.pnlInputArea.SuspendLayout();

        this.txtMessageInput.Text = "Nhập tin nhắn ...";
        this.txtMessageInput.ForeColor = Color.Gray;
        this.txtMessageInput.GotFocus += TxtMessageInput_GotFocus;
        this.txtMessageInput.LostFocus += TxtMessageInput_LostFocus;
        this.txtMessageInput.TextChanged += TxtMessageInput_TextChanged;

        this.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
        this.BackColor = BackgroundColor;

        this.lbPrivateChatMessages.FormattingEnabled = true;
        this.lbPrivateChatMessages.Location = new Point(10, 10);
        this.lbPrivateChatMessages.Name = "lbPrivateChatMessages";
        this.lbPrivateChatMessages.Size = new Size(380, 460);
        this.lbPrivateChatMessages.TabIndex = 0;
        this.lbPrivateChatMessages.DrawMode = DrawMode.OwnerDrawVariable;
        this.lbPrivateChatMessages.DrawItem += LbPrivateChatMessages_DrawItem;
        this.lbPrivateChatMessages.MeasureItem += LbPrivateChatMessages_MeasureItem;
        this.lbPrivateChatMessages.ContextMenuStrip = messageContextMenu;
        this.lbPrivateChatMessages.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.lbPrivateChatMessages.BackColor = ChatAreaBackgroundColor;
        this.lbPrivateChatMessages.BorderStyle = BorderStyle.None;
        this.lbPrivateChatMessages.MouseDown += LbPrivateChatMessages_MouseDown;
        this.lbPrivateChatMessages.MouseMove += LbPrivateChatMessages_MouseMove;

        this.pnlInputArea.BorderStyle = BorderStyle.None;
        this.pnlInputArea.Location = new Point(10, this.lbPrivateChatMessages.Bottom + 10);
        this.pnlInputArea.Name = "pnlInputArea";
        this.pnlInputArea.Size = new Size(this.lbPrivateChatMessages.Width, 60);
        this.pnlInputArea.TabIndex = 1;
        this.pnlInputArea.Controls.Add(this.txtMessageInput);
        this.pnlInputArea.Dock = DockStyle.Bottom;
        this.pnlInputArea.BackColor = Color.PaleGoldenrod;
        this.pnlInputArea.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        this.pnlInputArea.BorderStyle = BorderStyle.None;

        this.txtMessageInput.BorderStyle = BorderStyle.None;
        this.txtMessageInput.Location = new Point(1, 1);
        this.txtMessageInput.Multiline = true;
        this.txtMessageInput.Name = "txtMessageInput";
        this.txtMessageInput.Size = new Size(this.pnlInputArea.Width - 2, txtMessageInput.Font.Height * 4 + 6);
        this.txtMessageInput.TabIndex = 0;
        this.txtMessageInput.Font = new Font("Segoe UI", 9.75F);
        this.txtMessageInput.BackColor = Color.FromArgb(255, 255, 225);
        this.txtMessageInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        this.txtMessageInput.AcceptsReturn = true;
        this.txtMessageInput.ScrollBars = ScrollBars.None;
        this.txtMessageInput.WordWrap = true;
        this.txtMessageInput.KeyDown += new KeyEventHandler(this.TxtMessageInput_KeyDown);

        this.ClientSize = new Size(400, 550);
        this.Controls.Add(this.pnlInputArea);
        this.Controls.Add(this.lbPrivateChatMessages);
        this.Name = "PrivateChatForm";
        this.MinimumSize = new Size(400, 350);
        this.ResumeLayout(false);
        this.pnlInputArea.ResumeLayout(false);
        this.pnlInputArea.PerformLayout();
    }

    private void InitializeMessageContextMenu()
    {
        messageContextMenu = new ContextMenuStrip();
        copyMessageMenuItem = new ToolStripMenuItem("Sao chép");
        copyMessageMenuItem.Click += CopyMessageMenuItem_Click;
        messageContextMenu.Items.Add(copyMessageMenuItem);
        messageContextMenu.Opening += MessageContextMenu_Opening;
    }

    private List<Tuple<RectangleF, string>> CalculateUrlRegions(ChatMessage message, Rectangle itemBounds, Graphics g, Font font, float maxTextWidth)
    {
        var urlRegions = new List<Tuple<RectangleF, string>>();
        float totalTextHeight = 0;
        float totalTextWidth = 0;

        using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
        {
            sf.Trimming = StringTrimming.Character;

            if (!string.IsNullOrEmpty(message.Content))
            {
                int currentTextIndex = 0;
                var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList();
                foreach (var urlInfo in sortedUrls)
                {
                    string url = urlInfo.Item1;
                    int urlStartIndex = urlInfo.Item2;
                    int urlLength = urlInfo.Item3;

                    if (urlStartIndex > currentTextIndex)
                    {
                        string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                        if (!string.IsNullOrEmpty(textBeforeUrl))
                        {
                            SizeF sizeBefore = g.MeasureString(textBeforeUrl, font, new SizeF(maxTextWidth, float.MaxValue), sf);
                            totalTextHeight += sizeBefore.Height;
                            totalTextWidth = Math.Max(totalTextWidth, sizeBefore.Width);
                        }
                    }

                    SizeF sizeUrl;
                    using (Font urlFont = new Font(font, FontStyle.Underline))
                    {
                        sizeUrl = g.MeasureString(url, urlFont, new SizeF(maxTextWidth, float.MaxValue), sf);
                    }
                    totalTextHeight += sizeUrl.Height;
                    totalTextWidth = Math.Max(totalTextWidth, sizeUrl.Width);
                    currentTextIndex = urlStartIndex + urlLength;
                }

                if (currentTextIndex < message.Content.Length)
                {
                    string textAfterUrl = message.Content.Substring(currentTextIndex);
                    if (!string.IsNullOrEmpty(textAfterUrl))
                    {
                        SizeF sizeAfter = g.MeasureString(textAfterUrl, font, new SizeF(maxTextWidth, float.MaxValue), sf);
                        totalTextHeight += sizeAfter.Height;
                        totalTextWidth = Math.Max(totalTextWidth, sizeAfter.Width);
                    }
                }
            }
        }

        int bubbleWidth = Math.Min(MaxBubbleWidth, Math.Max(20, (int)totalTextWidth + MessageBubblePadding * 2));
        Size bubbleSize = new Size(bubbleWidth, Math.Max(20, (int)(totalTextHeight + MessageBubblePadding * 2)));
        Rectangle bubbleRect, textRect;

        if (message.IsSentByMe)
        {
            Rectangle avatarRect = new Rectangle(itemBounds.Right - AvatarSize - 10, itemBounds.Top + VerticalSpacing, AvatarSize, AvatarSize);
            bubbleRect = new Rectangle(avatarRect.Left - AvatarMargin - bubbleSize.Width, itemBounds.Top + VerticalSpacing, bubbleSize.Width, bubbleSize.Height);
            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth, (int)totalTextHeight);
        }
        else
        {
            Rectangle avatarRect = new Rectangle(itemBounds.Left + 10, itemBounds.Top + VerticalSpacing, AvatarSize, AvatarSize);
            int bubbleTopY = itemBounds.Top + VerticalSpacing;
            if (!string.IsNullOrEmpty(message.SenderName))
            {
                using (Font senderNameFont = new Font(font.FontFamily, font.Size * 0.85f, FontStyle.Bold))
                {
                    SizeF senderNameSizeF = g.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic);
                    bubbleTopY += (int)Math.Ceiling(senderNameSizeF.Height) + VerticalSpacing;
                }
            }
            bubbleRect = new Rectangle(avatarRect.Right + AvatarMargin, bubbleTopY, bubbleSize.Width, bubbleSize.Height);
            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth, (int)totalTextHeight);
        }

        using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
        {
            sf.Trimming = StringTrimming.Character;
            sf.Alignment = StringAlignment.Near;
            sf.LineAlignment = StringAlignment.Near;

            float currentY = textRect.Y - itemBounds.Y;
            int currentTextIndex = 0;
            var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList();

            foreach (var urlInfo in sortedUrls)
            {
                string url = urlInfo.Item1;
                int urlStartIndex = urlInfo.Item2;
                int urlLength = urlInfo.Item3;

                if (urlStartIndex > currentTextIndex)
                {
                    string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                    if (!string.IsNullOrEmpty(textBeforeUrl))
                    {
                        SizeF sizeBefore = g.MeasureString(textBeforeUrl, font, new SizeF(maxTextWidth, float.MaxValue), sf);
                        currentY += sizeBefore.Height;
                    }
                }

                SizeF sizeUrl;
                using (Font urlFont = new Font(font, FontStyle.Underline))
                {
                    sizeUrl = g.MeasureString(url, urlFont, new SizeF(maxTextWidth, float.MaxValue), sf);
                    RectangleF urlRect = new RectangleF(textRect.X - itemBounds.X, currentY, sizeUrl.Width, sizeUrl.Height);
                    urlRegions.Add(Tuple.Create(urlRect, url));
                }

                currentY += sizeUrl.Height;
                currentTextIndex = urlStartIndex + urlLength;
            }
        }

        return urlRegions;
    }

    private void LbPrivateChatMessages_MouseMove(object sender, MouseEventArgs e)
    {
        ListBox listBox = sender as ListBox;
        if (listBox == null) return;

        Debug.WriteLine($"[GỠ LỖI MouseMove] Di chuyển chuột tại: X={e.X}, Y={e.Y}");

        int index = listBox.IndexFromPoint(e.Location);
        Debug.WriteLine($"[GỠ LỖI MouseMove] Chỉ số: {index}, Vị trí: {e.Location}");

        if (index < 0 || index >= listBox.Items.Count)
        {
            listBox.Cursor = Cursors.Default;
            Debug.WriteLine($"[GỠ LỖI MouseMove] Không có mục nào tại vị trí, Con trỏ được đặt thành Mặc định");
            return;
        }

        if (listBox.Items[index] is ChatMessage message && message.Urls != null && message.Urls.Any())
        {
            Rectangle itemBounds = listBox.GetItemRectangle(index);
            PointF relativeLocation = new PointF(e.X - itemBounds.X, e.Y - itemBounds.Y);
            float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2);
            if (maxTextWidth < 1) maxTextWidth = 1;

            Debug.WriteLine($"[GỠ LỖI MouseMove] Chỉ số: {index}, Ranh giới mục: {itemBounds}, Vị trí tương đối: {relativeLocation}");

            using (Graphics g = listBox.CreateGraphics())
            {
                var urlRegions = CalculateUrlRegions(message, itemBounds, g, listBox.Font, maxTextWidth);
                bool isOverUrl = false;
                foreach (var urlInfo in urlRegions)
                {
                    RectangleF urlRect = urlInfo.Item1;
                    string url = urlInfo.Item2;
                    Debug.WriteLine($"[GỠ LỖI MouseMove] Kiểm tra URL: '{url}', Hình chữ nhật: {urlRect}");
                    if (urlRect.Contains(relativeLocation))
                    {
                        isOverUrl = true;
                        Debug.WriteLine($"[GỠ LỖI MouseMove] Chuột di chuyển qua URL: {url}, Hình chữ nhật: {urlRect}");
                        break;
                    }
                }
                listBox.Cursor = isOverUrl ? Cursors.Hand : Cursors.Default;
                Debug.WriteLine($"[GỠ LỖI MouseMove] Con trỏ được đặt thành: {(isOverUrl ? "Bàn tay" : "Mặc định")}");
            }
        }
        else
        {
            listBox.Cursor = Cursors.Default;
            Debug.WriteLine($"[GỠ LỖI MouseMove] Không có Tin nhắn hợp lệ hoặc URL, Con trỏ được đặt thành Mặc định");
        }
    }

    private void TxtMessageInput_GotFocus(object sender, EventArgs e)
    {
        if (txtMessageInput.Text == "Nhập tin nhắn ...")
        {
            txtMessageInput.Text = "";
            txtMessageInput.ForeColor = Color.Black;
        }
    }

    private void TxtMessageInput_LostFocus(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtMessageInput.Text))
        {
            txtMessageInput.Text = "Nhập tin nhắn ...";
            txtMessageInput.ForeColor = Color.Gray;
        }
    }

    private void TxtMessageInput_TextChanged(object sender, EventArgs e)
    {
        int lineCount = txtMessageInput.GetLineFromCharIndex(txtMessageInput.TextLength) + 1;
        txtMessageInput.ScrollBars = lineCount > 3 ? ScrollBars.Vertical : ScrollBars.None;
    }

    private void TxtMessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendMessage();
        }
        else if (e.KeyCode == Keys.Enter && e.Shift)
        {
            int caretPosition = txtMessageInput.SelectionStart;
            txtMessageInput.Text = txtMessageInput.Text.Insert(caretPosition, Environment.NewLine);
            txtMessageInput.SelectionStart = caretPosition + Environment.NewLine.Length;
            txtMessageInput.SelectionLength = 0;
            txtMessageInput.ScrollToCaret();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void MessageContextMenu_Opening(object sender, CancelEventArgs e)
    {
        copyMessageMenuItem.Enabled = lbPrivateChatMessages.SelectedIndex >= 0 && lbPrivateChatMessages.SelectedItem is ChatMessage;
        if (!copyMessageMenuItem.Enabled)
        {
            e.Cancel = true;
        }
    }

    private void CopyMessageMenuItem_Click(object sender, EventArgs e)
    {
        if (lbPrivateChatMessages.SelectedItem is ChatMessage selectedMessage)
        {
            Clipboard.SetText(selectedMessage.Content);
           // MessageBox.Show("Đã sao chép tin nhắn vào clipboard.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information); //Thông báo sau khi bấm sao chép tin nhắn
        }
    }

    private void LbPrivateChatMessages_MouseDown(object sender, MouseEventArgs e)
    {
        ListBox listBox = sender as ListBox;
        if (listBox == null) return;

        Debug.WriteLine($"[GỠ LỖI MouseDown] Nhấp chuột tại: X={e.X}, Y={e.Y}");

        if (e.Button == MouseButtons.Right)
        {
            int index = listBox.IndexFromPoint(e.Location);
            Debug.WriteLine($"[GỠ LỖI MouseDown] Phát hiện nhấp chuột phải, Chỉ số: {index}");
            if (index != ListBox.NoMatches)
            {
                listBox.SelectedIndex = index;
            }
            else
            {
                listBox.SelectedIndex = -1;
            }
        }
        else if (e.Button == MouseButtons.Left)
        {
            int index = listBox.IndexFromPoint(e.Location);
            Debug.WriteLine($"[GỠ LỖI MouseDown] Phát hiện nhấp chuột trái, Chỉ số: {index}, Vị trí: {e.Location}");

            if (index != ListBox.NoMatches && index < listBox.Items.Count)
            {
                object item = listBox.Items[index];
                if (item is ChatMessage message && message.Urls != null && message.Urls.Any())
                {
                    Rectangle itemBounds = listBox.GetItemRectangle(index);
                    PointF relativeClickLocation = new PointF(e.X - itemBounds.X, e.Y - itemBounds.Y);
                    float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2);
                    if (maxTextWidth < 1) maxTextWidth = 1;

                    Debug.WriteLine($"[GỠ LỖI MouseDown] Chỉ số: {index}, Ranh giới mục: {itemBounds}, Vị trí nhấp tương đối: {relativeClickLocation}");

                    using (Graphics g = listBox.CreateGraphics())
                    {
                        var urlRegions = CalculateUrlRegions(message, itemBounds, g, listBox.Font, maxTextWidth);
                        foreach (var urlInfo in urlRegions)
                        {
                            RectangleF urlRect = urlInfo.Item1;
                            string url = urlInfo.Item2;
                            Debug.WriteLine($"[GỠ LỖI MouseDown] Kiểm tra URL: '{url}', Hình chữ nhật: {urlRect}");
                            if (urlRect.Contains(relativeClickLocation))
                            {
                                Debug.WriteLine($"[GỠ LỖI MouseDown] Nhấp trúng URL: {url}, Hình chữ nhật: {urlRect}");
                                try
                                {
                                    if (!string.IsNullOrEmpty(url))
                                    {
                                        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            url = "https://" + url;
                                        }
                                        ProcessStartInfo psi = new ProcessStartInfo
                                        {
                                            FileName = url,
                                            UseShellExecute = true
                                        };
                                        Process.Start(psi);
                                        Debug.WriteLine($"[GỠ LỖI MouseDown] Đã mở URL: {url}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[GỠ LỖI MouseDown] Lỗi khi mở URL: {ex.Message}");
                                    MessageBox.Show($"Không thể mở URL: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                break;
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

    public void AddMessage(ChatMessage message)
    {
        // Loại bỏ ký tự xuống dòng hoặc ký tự không mong muốn
        message.Content = message.Content.TrimEnd('\n', '\r');
        Debug.WriteLine($"[DEBUG AddMessage] Nội dung tin nhắn sau khi làm sạch: '{message.Content}'");

        // Ensure URLs are detected for received messages
        if (!message.IsSentByMe && (message.Urls == null || !message.Urls.Any()))
        {
            message.Urls = FindUrlsInText(message.Content);
            Debug.WriteLine($"[DEBUG AddMessage] Đã phát hiện URL cho tin nhắn đã nhận: {message.Urls.Count} URL được tìm thấy");
        }

        if (lbPrivateChatMessages.InvokeRequired)
        {
            lbPrivateChatMessages.Invoke((MethodInvoker)delegate
            {
                lbPrivateChatMessages.Items.Add(message);
                if (lbPrivateChatMessages.Items.Count > 0)
                {
                    lbPrivateChatMessages.TopIndex = lbPrivateChatMessages.Items.Count - 1;
                }
                lbPrivateChatMessages.Invalidate();
            });
        }
        else
        {
            lbPrivateChatMessages.Items.Add(message);
            if (lbPrivateChatMessages.Items.Count > 0)
            {
                lbPrivateChatMessages.TopIndex = lbPrivateChatMessages.Items.Count - 1;
            }
            lbPrivateChatMessages.Invalidate();
        }
        Debug.WriteLine($"[CHAT] {message.SenderName}: {message.Content}");
    }

    public void SaveMessageToFile(ChatMessage message)
    {
        try
        {
            string commonHistoryDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChatHistory");
            if (!Directory.Exists(commonHistoryDirectory))
            {
                Directory.CreateDirectory(commonHistoryDirectory);
            }
            string filePath = Path.Combine(commonHistoryDirectory, GetPrivateChatHistoryFileName(myName, recipientUserName));
            string formattedMessage = $"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}] {message.SenderName}: {message.Content}";
            File.AppendAllText(filePath, formattedMessage + Environment.NewLine);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi lưu lịch sử trò chuyện: {ex.Message}", "Lỗi tệp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadHistoryFromFile()
    {
        try
        {
            string commonHistoryDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChatHistory");
            string filePath = Path.Combine(commonHistoryDirectory, GetPrivateChatHistoryFileName(myName, recipientUserName));
            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    try
                    {
                        int timestampEndIndex = line.IndexOf(']');
                        int senderNameEndIndex = line.IndexOf(':', timestampEndIndex + 2);
                        if (timestampEndIndex > 0 && senderNameEndIndex > timestampEndIndex)
                        {
                            string timestampString = line.Substring(1, timestampEndIndex - 1);
                            string senderName = line.Substring(timestampEndIndex + 2, senderNameEndIndex - (timestampEndIndex + 2)).Trim();
                            string content = line.Substring(senderNameEndIndex + 2).Trim();
                            if (DateTime.TryParseExact(timestampString, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out DateTime timestamp))
                            {
                                var loadedMessage = new ChatMessage
                                {
                                    SenderName = senderName,
                                    Content = content,
                                    Timestamp = timestamp,
                                    IsSentByMe = (senderName == myName),
                                    Urls = FindUrlsInText(content) // Phát hiện URL cho các tin nhắn đã tải
                                };
                                AddMessage(loadedMessage);
                            }
                        }
                    }
                    catch (Exception lineEx)
                    {
                        Console.WriteLine($"Lỗi khi đọc lịch sử chat dòng: {lineEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi khi tải lịch sử trò chuyện riêng tư: {ex.Message}");
        }
    }

    private static string GetPrivateChatHistoryFileName(string user1, string user2)
    {
        string[] users = { user1, user2 };
        Array.Sort(users);
        return $"{users[0]}_{users[1]}.txt";
    }

    private async void SendMessage()
    {
        string messageContent = txtMessageInput.Text.Trim();
        if (!string.IsNullOrWhiteSpace(messageContent) && messageContent != "Nhập tin nhắn ...")
        {
            var message = new ChatMessage
            {
                SenderName = myName,
                Content = messageContent,
                Timestamp = DateTime.Now,
                IsSentByMe = true,
                Urls = FindUrlsInText(messageContent)
            };

            AddMessage(message);
            SaveMessageToFile(message);

            if (tcpClient.Connected)
            {
                try
                {
                    string formattedMessage = $"{myName}|{messageContent}\n";
                    Debug.WriteLine($"[DEBUG SendMessage] Gửi: '{formattedMessage}' (Độ dài: {formattedMessage.Length})");
                    byte[] buffer = Encoding.UTF8.GetBytes(formattedMessage);
                    Debug.WriteLine($"[DEBUG SendMessage] Kích thước bộ đệm: {buffer.Length} bytes");
                    NetworkStream stream = tcpClient.GetStream();
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    await stream.FlushAsync();
                    Debug.WriteLine($"[DEBUG SendMessage] Gửi tin nhắn thành công: '{formattedMessage}'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DEBUG SendMessage] Lỗi khi gửi tin nhắn: {ex.Message}");
                    MessageBox.Show($"Lỗi gửi tin nhắn: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                Debug.WriteLine($"[DEBUG SendMessage] TcpClient không được kết nối");
                MessageBox.Show("Không thể gửi tin nhắn: Mất kết nối với server.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            txtMessageInput.Clear();
        }
    }

    private void LbPrivateChatMessages_MeasureItem(object sender, MeasureItemEventArgs e)
    {
        ListBox listBox = sender as ListBox;
        if (listBox == null) return;

        if (e.Index < 0 || e.Index >= listBox.Items.Count)
        {
            e.ItemHeight = 20;
            return;
        }

        object item = listBox.Items[e.Index];
        if (!(item is ChatMessage message))
        {
            e.ItemHeight = 20;
            return;
        }

        int timestampMargin = 8;
        float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2);
        if (maxTextWidth < 1) maxTextWidth = 1;

        float totalTextHeight = 0;
        float totalTextWidth = 0;
        float measuredSenderNameHeight = 0;
        float measuredTimestampHeight = 0;

        using (Graphics g = listBox.CreateGraphics())
        using (StringFormat sf = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
        {
            sf.Trimming = StringTrimming.Character;

            if (!string.IsNullOrEmpty(message.Content))
            {
                if (message.Urls == null || !message.Urls.Any())
                {
                    SizeF textSizeF = g.MeasureString(message.Content, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf);
                    totalTextHeight = textSizeF.Height;
                    totalTextWidth = textSizeF.Width;
                    Debug.WriteLine($"[MeasureItem] Nội dung: '{message.Content}', Chiều rộng đo: {totalTextWidth}, Chiều cao đo: {totalTextHeight}");
                }
                else
                {
                    int currentTextIndex = 0;
                    var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList();
                    foreach (var urlInfo in sortedUrls)
                    {
                        string url = urlInfo.Item1;
                        int urlStartIndex = urlInfo.Item2;
                        int urlLength = urlInfo.Item3;

                        if (urlStartIndex > currentTextIndex)
                        {
                            string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                            if (!string.IsNullOrEmpty(textBeforeUrl))
                            {
                                SizeF sizeBefore = g.MeasureString(textBeforeUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf);
                                totalTextHeight += sizeBefore.Height;
                                totalTextWidth = Math.Max(totalTextWidth, sizeBefore.Width);
                                Debug.WriteLine($"[MeasureItem] Văn bản trước URL: '{textBeforeUrl}', Chiều rộng: {sizeBefore.Width}, Chiều cao: {sizeBefore.Height}");
                            }
                        }

                        SizeF sizeUrl = g.MeasureString(url, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf);
                        totalTextHeight += sizeUrl.Height;
                        totalTextWidth = Math.Max(totalTextWidth, sizeUrl.Width);
                        Debug.WriteLine($"[MeasureItem] URL: '{url}', Chiều rộng: {sizeUrl.Width}, Chiều cao: {sizeUrl.Height}");
                        currentTextIndex = urlStartIndex + urlLength;
                    }

                    if (currentTextIndex < message.Content.Length)
                    {
                        string textAfterUrl = message.Content.Substring(currentTextIndex);
                        if (!string.IsNullOrEmpty(textAfterUrl))
                        {
                            SizeF sizeAfter = g.MeasureString(textAfterUrl, listBox.Font, new SizeF(maxTextWidth, float.MaxValue), sf);
                            totalTextHeight += sizeAfter.Height;
                            totalTextWidth = Math.Max(totalTextWidth, sizeAfter.Width);
                            Debug.WriteLine($"[MeasureItem] Văn bản sau URL: '{textAfterUrl}', Chiều rộng: {sizeAfter.Width}, Chiều cao: {sizeAfter.Height}");
                        }
                    }
                }
            }

            if (!message.IsSentByMe && !string.IsNullOrEmpty(message.SenderName))
            {
                using (Font senderNameFont = new Font(listBox.Font.FontFamily, listBox.Font.Size * 0.85f, FontStyle.Bold))
                {
                    measuredSenderNameHeight = g.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic).Height;
                }
            }

            using (Font timestampFont = new Font(listBox.Font.FontFamily, listBox.Font.Size * 0.7f))
            {
                measuredTimestampHeight = g.MeasureString(message.Timestamp.ToString("HH:mm"), timestampFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic).Height;
            }
        }

        float bubbleContentHeight = totalTextHeight + MessageBubblePadding * 2;
        float effectiveBubbleHeight = Math.Max(20, bubbleContentHeight);

        int totalHeight = 0;
        if (!message.IsSentByMe)
        {
            totalHeight = VerticalSpacing + (int)Math.Ceiling(measuredSenderNameHeight) + VerticalSpacing + (int)Math.Ceiling(effectiveBubbleHeight) + timestampMargin + (int)Math.Ceiling(measuredTimestampHeight) + VerticalSpacing;
        }
        else
        {
            totalHeight = VerticalSpacing + (int)Math.Ceiling(effectiveBubbleHeight) + timestampMargin + (int)Math.Ceiling(measuredTimestampHeight) + VerticalSpacing;
        }

        int minHeight = AvatarSize + VerticalSpacing * 2;
        e.ItemHeight = Math.Max(totalHeight, minHeight);
        if (e.ItemHeight < 1) e.ItemHeight = 1;
    }

    private void LbPrivateChatMessages_DrawItem(object sender, DrawItemEventArgs e)
    {
        ListBox listBox = sender as ListBox;
        if (listBox == null) return;

        if (e.Index < 0 || e.Index >= listBox.Items.Count)
            return;

        object item = listBox.Items[e.Index];
        if (!(item is ChatMessage message))
        {
            e.DrawBackground();
            e.Graphics.DrawString(item.ToString(), e.Font, new SolidBrush(e.ForeColor), e.Bounds, StringFormat.GenericDefault);
            e.DrawFocusRectangle();
            return;
        }

        Debug.WriteLine($"[DEBUG DrawItem] Nội dung tin nhắn: '{message.Content}'");

        if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
        {
            using (SolidBrush backBrush = new SolidBrush(SelectedItemColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }
        }
        else
        {
            using (SolidBrush backBrush = new SolidBrush(e.BackColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }
        }

        Color bubbleColor = message.IsSentByMe ? SentMessageColor : ReceivedMessageColor;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        float maxTextWidth = MaxBubbleWidth - (MessageBubblePadding * 2);
        if (maxTextWidth < 1) maxTextWidth = 1;

        float totalTextHeight = 0;
        float totalTextWidth = 0;
        if (!string.IsNullOrEmpty(message.Content))
        {
            using (StringFormat sfMeasure = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
            {
                sfMeasure.Trimming = StringTrimming.Character;
                if (message.Urls == null || !message.Urls.Any())
                {
                    SizeF textSizeF = e.Graphics.MeasureString(message.Content, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure);
                    totalTextHeight = textSizeF.Height;
                    totalTextWidth = textSizeF.Width;
                }
                else
                {
                    int currentTextIndex = 0;
                    var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList();
                    foreach (var urlInfo in sortedUrls)
                    {
                        string url = urlInfo.Item1;
                        int urlStartIndex = urlInfo.Item2;
                        int urlLength = urlInfo.Item3;

                        if (urlStartIndex > currentTextIndex)
                        {
                            string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                            if (!string.IsNullOrEmpty(textBeforeUrl))
                            {
                                SizeF sizeBefore = e.Graphics.MeasureString(textBeforeUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure);
                                totalTextHeight += sizeBefore.Height;
                                totalTextWidth = Math.Max(totalTextWidth, sizeBefore.Width);
                            }
                        }

                        SizeF sizeUrl = e.Graphics.MeasureString(url, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure);
                        totalTextHeight += sizeUrl.Height;
                        totalTextWidth = Math.Max(totalTextWidth, sizeUrl.Width);
                        currentTextIndex = urlStartIndex + urlLength;
                    }

                    if (currentTextIndex < message.Content.Length)
                    {
                        string textAfterUrl = message.Content.Substring(currentTextIndex);
                        if (!string.IsNullOrEmpty(textAfterUrl))
                        {
                            SizeF sizeAfter = e.Graphics.MeasureString(textAfterUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfMeasure);
                            totalTextHeight += sizeAfter.Height;
                            totalTextWidth = Math.Max(totalTextWidth, sizeAfter.Width);
                        }
                    }
                }
            }
        }

        int bubbleWidth = Math.Min(MaxBubbleWidth, Math.Max(20, (int)totalTextWidth + MessageBubblePadding * 2));
        Size bubbleSize = new Size(bubbleWidth, Math.Max(20, (int)(totalTextHeight + MessageBubblePadding * 2)));
        Rectangle bubbleRect, avatarRect, textRect, senderNameRect = Rectangle.Empty, timestampRect;

        if (message.IsSentByMe)
        {
            avatarRect = new Rectangle(e.Bounds.Right - AvatarSize - 10, e.Bounds.Top + VerticalSpacing, AvatarSize, AvatarSize);
            bubbleRect = new Rectangle(avatarRect.Left - AvatarMargin - bubbleSize.Width, e.Bounds.Top + VerticalSpacing, bubbleSize.Width, bubbleSize.Height);
            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth + 5, (int)totalTextHeight + 5); // Thêm lề nhỏ

            SizeF timestampSizeF = e.Graphics.MeasureString(message.Timestamp.ToString("HH:mm"), new Font(e.Font.FontFamily, e.Font.Size * 0.7f));
            Size timestampSize = Size.Ceiling(timestampSizeF);
            timestampRect = new Rectangle(bubbleRect.Right - timestampSize.Width, bubbleRect.Bottom + 8, timestampSize.Width, timestampSize.Height);
        }
        else
        {
            avatarRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + VerticalSpacing, AvatarSize, AvatarSize);
            int bubbleTopY = e.Bounds.Top + VerticalSpacing;

            if (!string.IsNullOrEmpty(message.SenderName))
            {
                using (Font senderNameFont = new Font(e.Font.FontFamily, e.Font.Size * 0.85f, FontStyle.Bold))
                {
                    SizeF senderNameSizeF = e.Graphics.MeasureString(message.SenderName, senderNameFont, new SizeF(maxTextWidth, float.MaxValue), StringFormat.GenericTypographic);
                    Size senderNameSize = Size.Ceiling(senderNameSizeF);
                    senderNameRect = new Rectangle(avatarRect.Right + AvatarMargin, e.Bounds.Top + VerticalSpacing, senderNameSize.Width, 14);
                    bubbleTopY = senderNameRect.Bottom + VerticalSpacing;
                }
            }

            bubbleRect = new Rectangle(avatarRect.Right + AvatarMargin, bubbleTopY, bubbleSize.Width, bubbleSize.Height);
            textRect = new Rectangle(bubbleRect.Left + MessageBubblePadding, bubbleRect.Top + MessageBubblePadding, (int)totalTextWidth + 5, (int)totalTextHeight + 5); // Thêm lề nhỏ

            SizeF timestampSizeF = e.Graphics.MeasureString(message.Timestamp.ToString("HH:mm"), new Font(e.Font.FontFamily, e.Font.Size * 0.7f));
            Size timestampSize = Size.Ceiling(timestampSizeF);
            timestampRect = new Rectangle(bubbleRect.Left, bubbleRect.Bottom + 8, timestampSize.Width, timestampSize.Height);
        }

        if (bubbleRect.Width > 0 && bubbleRect.Height > 0)
        {
            int effectiveRadius = Math.Min(MessageBubbleCornerRadius, Math.Min(bubbleRect.Width / 2, bubbleRect.Height / 2));
            using (GraphicsPath path = RoundedRectangle(bubbleRect, effectiveRadius))
            using (SolidBrush bubbleFillBrush = new SolidBrush(bubbleColor))
            {
                e.Graphics.FillPath(bubbleFillBrush, path);
            }

            if (!string.IsNullOrEmpty(message.Content) && textRect.Width > 0 && textRect.Height > 0)
            {
                using (StringFormat sfDraw = new StringFormat(StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces))
                {
                    sfDraw.Trimming = StringTrimming.Character;
                    sfDraw.Alignment = StringAlignment.Near;
                    sfDraw.LineAlignment = StringAlignment.Near;

                    if (message.Urls == null || !message.Urls.Any())
                    {
                        using (SolidBrush defaultBrush = new SolidBrush(Color.Black))
                        {
                            e.Graphics.DrawString(message.Content, e.Font, defaultBrush, textRect, sfDraw);
                        }
                    }
                    else
                    {
                        float currentY = textRect.Y;
                        int currentTextIndex = 0;
                        var sortedUrls = message.Urls.OrderBy(url => url.Item2).ToList();

                        foreach (var urlInfo in sortedUrls)
                        {
                            string url = urlInfo.Item1;
                            int urlStartIndex = urlInfo.Item2;
                            int urlLength = urlInfo.Item3;

                            if (urlStartIndex > currentTextIndex)
                            {
                                string textBeforeUrl = message.Content.Substring(currentTextIndex, urlStartIndex - currentTextIndex);
                                if (!string.IsNullOrEmpty(textBeforeUrl))
                                {
                                    SizeF sizeBefore = e.Graphics.MeasureString(textBeforeUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfDraw);
                                    RectangleF beforeRect = new RectangleF(textRect.X, currentY, maxTextWidth, sizeBefore.Height);
                                    using (SolidBrush textBrush = new SolidBrush(Color.Black))
                                    {
                                        e.Graphics.DrawString(textBeforeUrl, e.Font, textBrush, beforeRect, sfDraw);
                                        Debug.WriteLine($"[DEBUG DrawItem] Vẽ văn bản trước URL: '{textBeforeUrl}', Rect: {beforeRect}");
                                    }
                                    currentY += sizeBefore.Height;
                                }
                            }

                            using (Font urlFont = new Font(e.Font, FontStyle.Underline))
                            using (SolidBrush urlBrush = new SolidBrush(Color.Blue))
                            {
                                SizeF sizeUrl = e.Graphics.MeasureString(url, urlFont, new SizeF(maxTextWidth, float.MaxValue), sfDraw);
                                RectangleF urlRect = new RectangleF(textRect.X, currentY, sizeUrl.Width, sizeUrl.Height);
                                e.Graphics.DrawString(url, urlFont, urlBrush, urlRect, sfDraw);
                                Debug.WriteLine($"[DEBUG DrawItem] Vẽ URL: '{url}', Rect: {urlRect}");
                                currentY += sizeUrl.Height;
                            }

                            currentTextIndex = urlStartIndex + urlLength;
                        }

                        if (currentTextIndex < message.Content.Length)
                        {
                            string textAfterUrl = message.Content.Substring(currentTextIndex);
                            if (!string.IsNullOrEmpty(textAfterUrl))
                            {
                                SizeF sizeAfter = e.Graphics.MeasureString(textAfterUrl, e.Font, new SizeF(maxTextWidth, float.MaxValue), sfDraw);
                                RectangleF afterRect = new RectangleF(textRect.X, currentY, maxTextWidth, sizeAfter.Height);
                                using (SolidBrush textBrush = new SolidBrush(Color.Black))
                                {
                                    e.Graphics.DrawString(textAfterUrl, e.Font, textBrush, afterRect, sfDraw);
                                    Debug.WriteLine($"[DEBUG DrawItem] Vẽ văn bản sau URL: '{textAfterUrl}', Rect: {afterRect}");
                                }
                            }
                        }
                    }
                }
            }
        }

        using (Brush avatarBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
        using (Pen avatarPen = new Pen(Color.FromArgb(150, 150, 150), 1))
        {
            if (avatarRect.Width > 0 && avatarRect.Height > 0)
            {
                e.Graphics.FillEllipse(avatarBrush, avatarRect);
                e.Graphics.DrawEllipse(avatarPen, avatarRect);
                using (GraphicsPath humanPath = new GraphicsPath())
                {
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
                        humanPath.AddEllipse(headRect);
                    }
                    RectangleF bodyArcRect = new RectangleF(bodyX, bodyY - bodyHeight / 2, bodyWidth, bodyHeight);
                    if (bodyArcRect.Width > 0 && bodyArcRect.Height > 0)
                    {
                        humanPath.AddArc(bodyArcRect, 0, 180);
                        humanPath.CloseFigure();
                    }
                    using (Brush humanBrush = new SolidBrush(Color.DarkGray))
                    {
                        if (humanPath.PointCount > 0)
                        {
                            e.Graphics.FillPath(humanBrush, humanPath);
                        }
                    }
                }
            }
        }

        if (!message.IsSentByMe && senderNameRect.Width > 0 && senderNameRect.Height > 0 && !string.IsNullOrEmpty(message.SenderName))
        {
            using (Font senderNameFont = new Font(e.Font.FontFamily, e.Font.Size * 0.85f, FontStyle.Bold))
            using (Brush senderNameBrush = new SolidBrush(Color.DimGray))
            {
                e.Graphics.DrawString(message.SenderName, senderNameFont, senderNameBrush, senderNameRect, StringFormat.GenericTypographic);
            }
        }

        using (Font timestampFont = new Font(e.Font.FontFamily, e.Font.Size * 0.7f))
        {
            SizeF timestampSizeF = e.Graphics.MeasureString(message.Timestamp.ToString("HH:mm"), timestampFont, new SizeF(e.Bounds.Width, float.MaxValue), StringFormat.GenericTypographic);
            Size timestampSize = Size.Ceiling(timestampSizeF);
            if (timestampRect.Width > 0 && timestampRect.Height > 0)
            {
                e.Graphics.DrawString(message.Timestamp.ToString("HH:mm"), timestampFont, new SolidBrush(Color.Gray), timestampRect, StringFormat.GenericTypographic);
            }
        }
    }

    private GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        int diameter = radius * 2;
        Rectangle arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}