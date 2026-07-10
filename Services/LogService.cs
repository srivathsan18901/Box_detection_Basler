namespace VisioNeo_3D.Services
{
    public class LogService
    {
        private readonly RichTextBox _logBox;

        public LogService(RichTextBox logBox)
        {
            _logBox = logBox;
        }

        public void Log(string message, Color color)
        {
            if (_logBox.InvokeRequired)
            {
                _logBox.Invoke(new Action(() => Log(message, color)));
                return;
            }

            _logBox.SelectionColor = color;
            _logBox.AppendText(
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + " > " + message
                + Environment.NewLine
            );

            _logBox.ScrollToCaret();
        }
    }
}