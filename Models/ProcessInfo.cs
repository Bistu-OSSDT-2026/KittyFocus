namespace KittyFocus.Models
{
    /// <summary>
    /// 当前顶层活动窗口的快照信息。
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>进程名（小写，不含路径，可含 .exe）。</summary>
        public string ProcessName { get; set; }

        /// <summary>窗口标题。</summary>
        public string WindowTitle { get; set; }

        /// <summary>进程 ID。</summary>
        public int ProcessId { get; set; }
    }
}
