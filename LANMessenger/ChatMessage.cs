using System;
using System.Collections.Generic; // Thêm dòng này
using System.Drawing; // Cần thêm dòng này nếu RectangleF chưa được nhận dạng

namespace Messenger
{
    // Lớp biểu diễn một tin nhắn trong cuộc trò chuyện
    public class ChatMessage
    {
        public string SenderName { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsSentByMe { get; set; }
        public List<Tuple<string, int, int>> Urls { get; set; }
        public RectangleF? TextRect { get; set; } // Lưu trữ textRect
    }
}