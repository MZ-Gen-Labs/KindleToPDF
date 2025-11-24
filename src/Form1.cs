using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KindleToPDF
{
    public partial class Form1 : Form
    {
        private AutomationLogic _automation = null!;
        private PdfGenerator _pdfGenerator = null!;
        private CancellationTokenSource _cts = null!;
        private List<string> _capturedImages = null!;

        private Button btnStart = null!;
        private Button btnStop = null!;
        private Button btnTop = null!;
        private Button btnPrev = null!;
        private Button btnNext = null!;
        private Button btnBottom = null!;
        private Button btnFullScreen = null!;
        private Label lblInterval = null!;
        private TextBox txtInterval = null!;
        private Label lblPages = null!;
        private TextBox txtPages = null!;
#pragma warning disable CS0414 // Field is assigned but its value is never used - false positive, used in InitializeCustomControls
        private Label lblOutput = null!;
#pragma warning restore CS0414
        private TextBox txtOutput = null!;
        private CheckBox chkAutoDetect = null!;
        private CheckBox chkStopAtLastPage = null!;
        private CheckBox chkAlwaysOnTop = null!;
        private Label lblDpi = null!;
        private ComboBox cmbDpi = null!;
        private Label lblDirection = null!;
        private ComboBox cmbDirection = null!;
        private TextBox txtLog = null!;
        private Button btnSetCrop = null!;
#pragma warning disable CS0414 // Field is assigned but its value is never used - false positive, these are used in event handlers
        private Button btnRefreshTitle = null!;
        private Button btnNamingOptions = null!;
#pragma warning restore CS0414
        private ComboBox cmbCropPatterns = null!;
        private NumericUpDown numMaxPatterns = null!;
        private Label lblCropLeft = null!, lblCropTop = null!, lblCropRight = null!, lblCropBottom = null!;
        private TextBox txtCropLeft = null!, txtCropTop = null!, txtCropRight = null!, txtCropBottom = null!;
        private Label lblCropLeftMax = null!, lblCropTopMax = null!, lblCropRightMax = null!, lblCropBottomMax = null!;
        private Rectangle _cropRect = Rectangle.Empty;
        private GuidelineOverlay _guidelineOverlay = null!;

        // Manual Capture Mode Controls
        private RadioButton rbModeContinuous = null!;
        private RadioButton rbModeManual = null!;
        private Button btnCapture = null!;
        private Button btnRemoveLast = null!;
        private Button btnClearAll = null!;
        private Button btnCreatePdf = null!;
        private Label lblCaptureCount = null!;

        // Compression Controls
        private Label lblColorMode = null!;
        private ComboBox cmbColorMode = null!;
        private Label lblJpegQuality = null!;
        private TrackBar trkJpegQuality = null!;
        private Label lblJpegQualityValue = null!;

        private AppSettings _settings = null!;

        public Form1()
        {
            try
            {
                File.AppendAllText("debug_log.txt", "Form1 Ctor: Start\n");
                _settings = AppSettings.Load();
                File.AppendAllText("debug_log.txt", "Form1 Ctor: Settings Loaded\n");
                InitializeComponent();
                File.AppendAllText("debug_log.txt", "Form1 Ctor: InitializeComponent Done\n");
                InitializeCustomControls();
                File.AppendAllText("debug_log.txt", "Form1 Ctor: InitializeCustomControls Done\n");
                _automation = new AutomationLogic();
                _pdfGenerator = new PdfGenerator();
                _capturedImages = new List<string>();
                
                ApplySettingsToUI();
                File.AppendAllText("debug_log.txt", "Form1 Ctor: ApplySettingsToUI Done\n");

                // Create and show guideline overlay
                _guidelineOverlay = new GuidelineOverlay();
                _guidelineOverlay.Show();
                _guidelineOverlay.UpdateCropRect(_cropRect);
                File.AppendAllText("debug_log.txt", "Form1 Ctor: Overlay Done\n");

                this.FormClosing += Form1_FormClosing;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Startup Error: {ex.Message}\nStack Trace: {ex.StackTrace}";
                try { File.AppendAllText("debug_log.txt", "ERROR: " + errorMsg + "\n"); } catch { }
                try { File.WriteAllText("startup_error_detailed.txt", errorMsg); } catch { }
                MessageBox.Show(errorMsg, "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Ensure we don't leave a zombie process if possible, though rethrowing might be enough
                throw;
            }
        }

        private void ApplySettingsToUI()
        {
            try
            {
                if (txtInterval != null) txtInterval.Text = _settings.Interval.ToString();
                if (txtPages != null) txtPages.Text = _settings.PageCount.ToString();
                if (chkAutoDetect != null) chkAutoDetect.Checked = _settings.AutoDetect;
                if (chkStopAtLastPage != null) chkStopAtLastPage.Checked = _settings.StopAtLastPage;
                if (chkAlwaysOnTop != null) 
                {
                    chkAlwaysOnTop.Checked = _settings.AlwaysOnTop;
                    this.TopMost = _settings.AlwaysOnTop;
                }
                
                if (cmbDpi != null && _settings.DpiIndex >= 0 && _settings.DpiIndex < cmbDpi.Items.Count)
                    cmbDpi.SelectedIndex = _settings.DpiIndex;
                
                if (cmbDirection != null)
                {
                    if (_settings.PageDirection >= 0 && _settings.PageDirection < cmbDirection.Items.Count)
                        cmbDirection.SelectedIndex = _settings.PageDirection;
                    else
                        cmbDirection.SelectedIndex = 0; // Default R2L
                }
                
                // Load Capture Mode
                if (rbModeContinuous != null && rbModeManual != null)
                {
                    if (_settings.CaptureMode == CaptureMode.Manual)
                        rbModeManual.Checked = true;
                    else
                        rbModeContinuous.Checked = true;
                    UpdateModeUI();
                }
                
                // Load Compression Settings
                if (cmbColorMode != null)
                {
                    int colorModeIndex = _settings.ColorMode switch
                    {
                        ImageColorMode.Monochrome => 0,
                        ImageColorMode.Grayscale => 1,
                        ImageColorMode.Indexed256 => 2,
                        ImageColorMode.HighColor => 3,
                        ImageColorMode.FullColor => 4,
                        _ => 1
                    };
                    cmbColorMode.SelectedIndex = colorModeIndex;
                }
                
                if (trkJpegQuality != null && lblJpegQualityValue != null)
                {
                    trkJpegQuality.Value = _settings.JpegQuality;
                    lblJpegQualityValue.Text = _settings.JpegQuality.ToString();
                }
                
                _cropRect = _settings.CropRect;
                if (_cropRect != Rectangle.Empty)
                {
                    Log($"Loaded crop area: {_cropRect}");
                }

                // Initialize Pattern UI
                if (numMaxPatterns != null) numMaxPatterns.Value = _settings.MaxPatterns;
                UpdatePatternComboBox();
                if (cmbCropPatterns != null)
                {
                    if (_settings.SelectedPatternIndex >= 0 && _settings.SelectedPatternIndex < cmbCropPatterns.Items.Count)
                    {
                        cmbCropPatterns.SelectedIndex = _settings.SelectedPatternIndex;
                    }
                    else if (cmbCropPatterns.Items.Count > 0)
                    {
                        cmbCropPatterns.SelectedIndex = 0;
                    }
                }
                
                // Load initial crop rect
                _cropRect = _settings.CropRect;
                UpdateCropTextBoxes();
                UpdateCropLimitLabels();

                // Load book title on startup
                if (_automation != null)
                {
                    IntPtr kindleHandle = _automation.GetKindleWindow();
                    if (kindleHandle != IntPtr.Zero)
                    {
                        _automation.BringWindowToFront(kindleHandle); // Bring Kindle to front
                        string? bookTitle = _automation.GetBookTitleFromWindow(kindleHandle);
                        if (!string.IsNullOrEmpty(bookTitle) && txtOutput != null)
                        {
                            txtOutput.Text = bookTitle + ".pdf";
                            Log($"Book title detected on startup: {bookTitle}");
                        }
                        else if (txtOutput != null)
                        {
                            txtOutput.Text = "output.pdf";
                        }
                    }
                    else if (txtOutput != null)
                    {
                        txtOutput.Text = "output.pdf";
                    }
                }
                
                // Update compression UI visibility based on loaded settings
                UpdateCompressionUI();
            }
            catch (Exception ex)
            {
                Log($"Error in ApplySettingsToUI: {ex.Message}");
            }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (int.TryParse(txtInterval.Text, out int interval)) _settings.Interval = interval;
            if (int.TryParse(txtPages.Text, out int pages)) _settings.PageCount = pages;
            _settings.AutoDetect = chkAutoDetect.Checked;
            _settings.StopAtLastPage = chkStopAtLastPage.Checked;
            _settings.AlwaysOnTop = chkAlwaysOnTop.Checked;
            _settings.DpiIndex = cmbDpi.SelectedIndex;
            _settings.PageDirection = cmbDirection.SelectedIndex;
            _settings.CropRect = _cropRect;
            _settings.CaptureMode = rbModeManual.Checked ? CaptureMode.Manual : CaptureMode.Continuous;

            _settings.Save();
            
            // Cleanup guideline overlay
            _guidelineOverlay?.Close();
            _guidelineOverlay?.Dispose();
        }

        private void InitializeCustomControls()
        {
            this.Size = new Size(450, 900); // Increased height for new controls
            this.Text = "Kindle to PDF Automation";

            int y = 15;

            // Capture Mode Selection
            Label lblMode = new Label { Text = "Capture Mode:", Location = new Point(20, y), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            this.Controls.Add(lblMode);
            y += 22;

            rbModeContinuous = new RadioButton { Text = "Continuous Auto Capture", Location = new Point(30, y), AutoSize = true, Checked = true };
            rbModeContinuous.CheckedChanged += (s, e) => UpdateModeUI();
            this.Controls.Add(rbModeContinuous);
            y += 22;

            rbModeManual = new RadioButton { Text = "Manual Selection Capture", Location = new Point(30, y), AutoSize = true };
            rbModeManual.CheckedChanged += (s, e) => UpdateModeUI();
            this.Controls.Add(rbModeManual);
            y += 28;

            // Separator
            Label lblSeparator1 = new Label { Text = "━━━━━━━━━━━━━━━━━━━━━━━━━━━", Location = new Point(20, y), AutoSize = true, ForeColor = Color.Gray };
            this.Controls.Add(lblSeparator1);
            y += 22;

            // 1. Auto-detect Page Turn (Moved up)
            chkAutoDetect = new CheckBox { Text = "Auto-detect Page Turn", Location = new Point(20, y), AutoSize = true, Checked = true };
            chkAutoDetect.CheckedChanged += (s, e) => 
            {
                txtInterval.Enabled = !chkAutoDetect.Checked;
                lblInterval.Enabled = !chkAutoDetect.Checked;
            };
            this.Controls.Add(chkAutoDetect);

            y += 25;
            // 2. Interval (Moved down)
            lblInterval = new Label { Text = "Interval (ms):", Location = new Point(20, y), AutoSize = true };
            txtInterval = new TextBox { Text = "1000", Location = new Point(150, y - 3) };
            // Initialize enabled state
            txtInterval.Enabled = !chkAutoDetect.Checked;
            lblInterval.Enabled = !chkAutoDetect.Checked;
            this.Controls.Add(lblInterval);
            this.Controls.Add(txtInterval);

            y += 30;
            // 3. Stop at Last Page (Moved up)
            chkStopAtLastPage = new CheckBox { Text = "Stop at Last Page", Location = new Point(20, y), AutoSize = true, Checked = true };
            chkStopAtLastPage.CheckedChanged += (s, e) =>
            {
                txtPages.Enabled = !chkStopAtLastPage.Checked;
                lblPages.Enabled = !chkStopAtLastPage.Checked;
            };
            this.Controls.Add(chkStopAtLastPage);

            y += 25;
            // 4. Page Count (Moved down)
            lblPages = new Label { Text = "Page Count:", Location = new Point(20, y), AutoSize = true };
            txtPages = new TextBox { Text = "10", Location = new Point(150, y - 3) };
            // Initialize enabled state
            txtPages.Enabled = !chkStopAtLastPage.Checked;
            lblPages.Enabled = !chkStopAtLastPage.Checked;
            this.Controls.Add(lblPages);
            this.Controls.Add(txtPages);

            y += 30;
            chkAlwaysOnTop = new CheckBox { Text = "Always on Top", Location = new Point(20, y), AutoSize = true, Checked = true };
            chkAlwaysOnTop.CheckedChanged += (s, e) => { this.TopMost = chkAlwaysOnTop.Checked; };
            this.Controls.Add(chkAlwaysOnTop);

            y += 30;
            lblDpi = new Label { Text = "PDF DPI:", Location = new Point(20, y), AutoSize = true };
            cmbDpi = new ComboBox { Location = new Point(150, y - 3), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDpi.Items.AddRange(new object[] { "Default", "300", "450", "600" });
            cmbDpi.SelectedIndex = 0;
            this.Controls.Add(lblDpi);
            this.Controls.Add(cmbDpi);

            y += 30;
            lblDirection = new Label { Text = "Page Direction:", Location = new Point(20, y), AutoSize = true };
            cmbDirection = new ComboBox { Location = new Point(150, y - 3), DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            cmbDirection.Items.AddRange(new object[] { "Right to Left (JP)", "Left to Right (EN)" });
            cmbDirection.SelectedIndex = 0;
            this.Controls.Add(lblDirection);
            this.Controls.Add(cmbDirection);

            y += 30;
            lblColorMode = new Label { Text = "Image Quality:", Location = new Point(20, y), AutoSize = true };
            cmbColorMode = new ComboBox { Location = new Point(150, y - 3), DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            cmbColorMode.Items.AddRange(new object[] { "Monochrome (1-bit)", "Grayscale (8-bit)", "256 Colors", "High Color (16-bit)", "Full Color (24-bit)" });
            cmbColorMode.SelectedIndex = 1; // Default to Grayscale
            cmbColorMode.SelectedIndexChanged += CmbColorMode_SelectedIndexChanged;
            this.Controls.Add(lblColorMode);
            this.Controls.Add(cmbColorMode);

            y += 30;
            lblJpegQuality = new Label { Text = "JPEG Quality:", Location = new Point(20, y), AutoSize = true };
            lblJpegQualityValue = new Label { Text = "80", Location = new Point(340, y), AutoSize = true };
            this.Controls.Add(lblJpegQuality);
            this.Controls.Add(lblJpegQualityValue);

            y += 22;
            trkJpegQuality = new TrackBar { Location = new Point(20, y), Width = 300, Minimum = 60, Maximum = 100, Value = 80, TickFrequency = 10 };
            trkJpegQuality.ValueChanged += (s, e) => 
            {
                lblJpegQualityValue.Text = trkJpegQuality.Value.ToString();
                _settings.JpegQuality = trkJpegQuality.Value;
            };
            this.Controls.Add(trkJpegQuality);

            // JPEG quality controls visibility will be set in ApplySettingsToUI

            y += 35;
            btnTop = new Button { Text = "<< Top", Location = new Point(20, y), Size = new Size(80, 30) };
            btnTop.Click += BtnTop_Click;
            this.Controls.Add(btnTop);

            btnPrev = new Button { Text = "< Prev", Location = new Point(110, y), Size = new Size(80, 30) };
            btnPrev.Click += BtnPrev_Click;
            this.Controls.Add(btnPrev);

            btnNext = new Button { Text = "Next >", Location = new Point(200, y), Size = new Size(80, 30) };
            btnNext.Click += BtnNext_Click;
            this.Controls.Add(btnNext);

            btnFullScreen = new Button { Text = "Full Screen", Location = new Point(20, y + 35), Size = new Size(80, 30) };
            btnFullScreen.Click += BtnFullScreen_Click;
            this.Controls.Add(btnFullScreen);

            y += 35;
            btnBottom = new Button { Text = ">> Bottom", Location = new Point(290, y - 35), Size = new Size(80, 30) };
            btnBottom.Click += BtnBottom_Click;
            this.Controls.Add(btnBottom);

            y += 35;
            btnSetCrop = new Button { Text = "Set Crop Area", Location = new Point(20, y), Size = new Size(120, 30) };
            btnSetCrop.Click += BtnSetCrop_Click;
            this.Controls.Add(btnSetCrop);

            y += 35;
            Label lblPattern = new Label { Text = "Pattern:", Location = new Point(20, y), AutoSize = true };
            this.Controls.Add(lblPattern);

            cmbCropPatterns = new ComboBox { Location = new Point(80, y - 3), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCropPatterns.SelectedIndexChanged += CmbCropPatterns_SelectedIndexChanged;
            this.Controls.Add(cmbCropPatterns);

            Label lblMaxPatterns = new Label { Text = "Max:", Location = new Point(200, y), AutoSize = true };
            this.Controls.Add(lblMaxPatterns);

            numMaxPatterns = new NumericUpDown { Location = new Point(240, y - 3), Width = 50, Minimum = 1, Maximum = 20, Value = 5 };
            numMaxPatterns.ValueChanged += NumMaxPatterns_ValueChanged;
            this.Controls.Add(numMaxPatterns);

            y += 28;
            Label lblCrop = new Label { Text = "Crop (px):", Location = new Point(20, y), AutoSize = true };
            this.Controls.Add(lblCrop);
            
            lblCropLeft = new Label { Text = "L:", Location = new Point(20, y + 25), AutoSize = true };
            txtCropLeft = new TextBox { Text = "0", Location = new Point(40, y + 22), Width = 50 };
            txtCropLeft.TextChanged += TxtCrop_TextChanged;
            lblCropLeftMax = new Label { Text = "0", Location = new Point(40, y + 45), Width = 50, ForeColor = Color.Gray, Font = new Font(this.Font.FontFamily, 7) };
            this.Controls.Add(lblCropLeft);
            this.Controls.Add(txtCropLeft);
            this.Controls.Add(lblCropLeftMax);

            lblCropTop = new Label { Text = "T:", Location = new Point(100, y + 25), AutoSize = true };
            txtCropTop = new TextBox { Text = "0", Location = new Point(120, y + 22), Width = 50 };
            txtCropTop.TextChanged += TxtCrop_TextChanged;
            lblCropTopMax = new Label { Text = "0", Location = new Point(120, y + 45), Width = 50, ForeColor = Color.Gray, Font = new Font(this.Font.FontFamily, 7) };
            this.Controls.Add(lblCropTop);
            this.Controls.Add(txtCropTop);
            this.Controls.Add(lblCropTopMax);

            lblCropRight = new Label { Text = "R:", Location = new Point(180, y + 25), AutoSize = true };
            txtCropRight = new TextBox { Text = "0", Location = new Point(200, y + 22), Width = 50 };
            txtCropRight.TextChanged += TxtCrop_TextChanged;
            lblCropRightMax = new Label { Text = "", Location = new Point(200, y + 45), Width = 50, ForeColor = Color.Gray, Font = new Font(this.Font.FontFamily, 7) };
            this.Controls.Add(lblCropRight);
            this.Controls.Add(txtCropRight);
            this.Controls.Add(lblCropRightMax);

            lblCropBottom = new Label { Text = "B:", Location = new Point(260, y + 25), AutoSize = true };
            txtCropBottom = new TextBox { Text = "0", Location = new Point(280, y + 22), Width = 50 };
            txtCropBottom.TextChanged += TxtCrop_TextChanged;
            lblCropBottomMax = new Label { Text = "", Location = new Point(280, y + 45), Width = 50, ForeColor = Color.Gray, Font = new Font(this.Font.FontFamily, 7) };
            this.Controls.Add(lblCropBottom);
            this.Controls.Add(txtCropBottom);
            this.Controls.Add(lblCropBottomMax);

            y += 75;
            lblOutput = new Label { Text = "Output PDF:", Location = new Point(20, y), AutoSize = true };
            txtOutput = new TextBox { Text = "output.pdf", Location = new Point(100, y - 3), Width = 150 };
            this.Controls.Add(lblOutput);
            this.Controls.Add(txtOutput);

            btnRefreshTitle = new Button { Text = "Refresh", Location = new Point(260, y - 5), Size = new Size(60, 25) };
            btnRefreshTitle.Click += BtnRefreshTitle_Click;
            this.Controls.Add(btnRefreshTitle);

            btnNamingOptions = new Button { Text = "Options", Location = new Point(330, y - 5), Size = new Size(60, 25) };
            btnNamingOptions.Click += BtnNamingOptions_Click;
            this.Controls.Add(btnNamingOptions);

            y += 35;
            btnStart = new Button { Text = "Start", Location = new Point(50, y), Size = new Size(100, 40) };
            btnStart.Click += BtnStart_Click;
            this.Controls.Add(btnStart);

            btnStop = new Button { Text = "Stop", Location = new Point(160, y), Size = new Size(100, 40), Enabled = false };
            btnStop.Click += BtnStop_Click;
            this.Controls.Add(btnStop);

            btnAbort = new Button { Text = "Abort", Location = new Point(270, y), Size = new Size(80, 40), Enabled = false, BackColor = Color.LightPink };
            btnAbort.Click += BtnAbort_Click;
            this.Controls.Add(btnAbort);

            y += 45;
            // Manual Capture Mode Controls
            Label lblSeparator2 = new Label { Text = "━━━━━━━━━━━━━━━━━━━━━━━━━━━", Location = new Point(20, y), AutoSize = true, ForeColor = Color.Gray };
            this.Controls.Add(lblSeparator2);
            y += 22;

            lblCaptureCount = new Label { Text = "Captured: 0 pages", Location = new Point(20, y), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            this.Controls.Add(lblCaptureCount);
            y += 28;

            btnCapture = new Button { Text = "Capture Page", Location = new Point(20, y), Size = new Size(100, 35) };
            btnCapture.Click += BtnCapture_Click;
            this.Controls.Add(btnCapture);

            btnRemoveLast = new Button { Text = "Remove Last", Location = new Point(130, y), Size = new Size(100, 35) };
            btnRemoveLast.Click += BtnRemoveLast_Click;
            this.Controls.Add(btnRemoveLast);

            y += 38;
            btnClearAll = new Button { Text = "Clear All", Location = new Point(20, y), Size = new Size(100, 35) };
            btnClearAll.Click += BtnClearAll_Click;
            this.Controls.Add(btnClearAll);

            btnCreatePdf = new Button { Text = "Create PDF", Location = new Point(130, y), Size = new Size(100, 35), BackColor = Color.LightGreen };
            btnCreatePdf.Click += BtnCreatePdf_Click;
            this.Controls.Add(btnCreatePdf);

            y += 40;
            txtLog = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Location = new Point(20, y), Size = new Size(340, 150), ReadOnly = true };
            this.Controls.Add(txtLog);
        }

        private void Log(string message)
        {
            if (txtLog == null || txtLog.IsDisposed) return;
            if (InvokeRequired)
            {
                try { Invoke(new Action<string>(Log), message); } catch { }
                return;
            }
            try { txtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}"); } catch { }
        }

        private void CmbColorMode_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Map combo box index to ImageColorMode enum
            _settings.ColorMode = cmbColorMode.SelectedIndex switch
            {
                0 => ImageColorMode.Monochrome,
                1 => ImageColorMode.Grayscale,
                2 => ImageColorMode.Indexed256,
                3 => ImageColorMode.HighColor,
                4 => ImageColorMode.FullColor,
                _ => ImageColorMode.Grayscale
            };

            UpdateCompressionUI();
        }

        private void UpdateCompressionUI()
        {
            // Show JPEG quality controls only for HighColor and FullColor modes
            bool showJpegQuality = _settings.ColorMode == ImageColorMode.HighColor || _settings.ColorMode == ImageColorMode.FullColor;
            
            if (lblJpegQuality != null) lblJpegQuality.Visible = showJpegQuality;
            if (trkJpegQuality != null) trkJpegQuality.Visible = showJpegQuality;
            if (lblJpegQualityValue != null) lblJpegQualityValue.Visible = showJpegQuality;
        }

        private void UpdateModeUI()
        {
            bool isContinuous = rbModeContinuous.Checked;

            // Enable/disable continuous mode controls
            chkAutoDetect.Enabled = isContinuous;
            lblInterval.Enabled = isContinuous && !chkAutoDetect.Checked;
            txtInterval.Enabled = isContinuous && !chkAutoDetect.Checked;
            chkStopAtLastPage.Enabled = isContinuous;
            lblPages.Enabled = isContinuous && !chkStopAtLastPage.Checked;
            txtPages.Enabled = isContinuous && !chkStopAtLastPage.Checked;
            btnStart.Enabled = isContinuous;
            btnStop.Enabled = isContinuous && btnStop.Enabled; // Preserve state if running
            btnAbort.Enabled = isContinuous && btnAbort.Enabled; // Preserve state if running

            // Enable/disable manual mode controls
            btnCapture.Enabled = !isContinuous;
            btnRemoveLast.Enabled = !isContinuous && _capturedImages.Count > 0;
            btnClearAll.Enabled = !isContinuous && _capturedImages.Count > 0;
            btnCreatePdf.Enabled = !isContinuous && _capturedImages.Count > 0;
            lblCaptureCount.Visible = !isContinuous;
        }

        private void UpdateCaptureCount()
        {
            lblCaptureCount.Text = $"Captured: {_capturedImages.Count} pages";
            btnRemoveLast.Enabled = rbModeManual.Checked && _capturedImages.Count > 0;
            btnClearAll.Enabled = rbModeManual.Checked && _capturedImages.Count > 0;
            btnCreatePdf.Enabled = rbModeManual.Checked && _capturedImages.Count > 0;
        }

        private void BtnCapture_Click(object? sender, EventArgs e)
        {
            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle == IntPtr.Zero)
            {
                Log("Error: Kindle window not found!");
                MessageBox.Show("Please open Kindle for PC first.");
                return;
            }

            // Hide main window and overlay to prevent them from being captured
            this.Hide();
            _guidelineOverlay?.Hide();

            try
            {
                // Wait a moment for windows to hide
                Thread.Sleep(200);

                Rectangle bounds = _automation.GetWindowBounds(kindleHandle);
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    Log("Error: Invalid window bounds");
                    return;
                }

                Bitmap rawImage = _automation.CaptureWindow(bounds);
                Bitmap finalImage;

                if (_cropRect != Rectangle.Empty)
                {
                    int relX = _cropRect.X - bounds.X;
                    int relY = _cropRect.Y - bounds.Y;
                    Rectangle relativeCrop = new Rectangle(relX, relY, _cropRect.Width, _cropRect.Height);
                    finalImage = _automation.CropBitmap(rawImage, relativeCrop);
                    rawImage.Dispose();
                }
                else
                {
                    finalImage = rawImage;
                }

                string tempDir = Path.Combine(Path.GetTempPath(), "KindleToPDF_Temp");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                string imgPath = Path.Combine(tempDir, $"page_{_capturedImages.Count:D4}.png");
                finalImage.Save(imgPath, ImageFormat.Png);
                finalImage.Dispose();

                _capturedImages.Add(imgPath);
                UpdateCaptureCount();
                Log($"Captured page {_capturedImages.Count}");
            }
            catch (Exception ex)
            {
                Log($"Error capturing page: {ex.Message}");
                MessageBox.Show($"Failed to capture page: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Restore window visibility
                this.Show();
                _guidelineOverlay?.Show();
            }
        }

        private void BtnRemoveLast_Click(object? sender, EventArgs e)
        {
            if (_capturedImages.Count == 0) return;

            string lastImage = _capturedImages[_capturedImages.Count - 1];
            _capturedImages.RemoveAt(_capturedImages.Count - 1);

            try
            {
                if (File.Exists(lastImage))
                    File.Delete(lastImage);
            }
            catch { }

            UpdateCaptureCount();
            Log($"Removed last capture. Remaining: {_capturedImages.Count}");
        }

        private void BtnClearAll_Click(object? sender, EventArgs e)
        {
            if (_capturedImages.Count == 0) return;

            if (MessageBox.Show($"Clear all {_capturedImages.Count} captured pages?", "Confirm Clear", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                foreach (string img in _capturedImages)
                {
                    try
                    {
                        if (File.Exists(img))
                            File.Delete(img);
                    }
                    catch { }
                }

                _capturedImages.Clear();
                UpdateCaptureCount();
                Log("Cleared all captures");
            }
        }

        private async void BtnCreatePdf_Click(object? sender, EventArgs e)
        {
            if (_capturedImages.Count == 0)
            {
                MessageBox.Show("No pages captured yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await FinalizePdf();
            _capturedImages.Clear();
            UpdateCaptureCount();
        }

        private void BtnSetCrop_Click(object? sender, EventArgs e)
        {
            this.Hide();
            using (OverlayForm overlay = new OverlayForm(_cropRect))
            {
                if (overlay.ShowDialog() == DialogResult.OK)
                {
                    _cropRect = overlay.CropRect;
                    _settings.CropRect = _cropRect; // Sync to current pattern
                    Log($"Crop area set: {_cropRect}");
                    UpdateCropTextBoxes();
                    _guidelineOverlay?.UpdateCropRect(_cropRect);
                }
            }
            this.Show();
        }

        private void BtnRefreshTitle_Click(object? sender, EventArgs e)
        {
            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle == IntPtr.Zero)
            {
                Log("Error: Kindle window not found!");
                MessageBox.Show("Kindleウィンドウが見つかりません。Kindle for PCを起動してください。");
                return;
            }

            string? bookTitle = _automation.GetBookTitleFromWindow(kindleHandle);
            if (!string.IsNullOrEmpty(bookTitle))
            {
                txtOutput.Text = bookTitle + ".pdf";
                Log($"Book title updated: {bookTitle}");
                UpdateCropLimitLabels();
            }
            else
            {
                Log("Could not extract book title from Kindle window.");
                MessageBox.Show("書籍タイトルを取得できませんでした。");
            }
        }

        private bool _updatingCropTextBoxes = false;

        private void TxtCrop_TextChanged(object? sender, EventArgs e)
        {
            if (_updatingCropTextBoxes) return;

            if (int.TryParse(txtCropLeft.Text, out int left) &&
                int.TryParse(txtCropTop.Text, out int top) &&
                int.TryParse(txtCropRight.Text, out int right) &&
                int.TryParse(txtCropBottom.Text, out int bottom))
            {
                _cropRect = new Rectangle(left, top, right - left, bottom - top);
                _settings.CropRect = _cropRect; // Sync to current pattern
                _guidelineOverlay?.UpdateCropRect(_cropRect);
                Log($"Crop area updated from numeric input: {_cropRect}");
            }
        }

        private void UpdateCropTextBoxes()
        {
            _updatingCropTextBoxes = true;
            if (_cropRect != Rectangle.Empty)
            {
                txtCropLeft.Text = _cropRect.Left.ToString();
                txtCropTop.Text = _cropRect.Top.ToString();
                txtCropRight.Text = _cropRect.Right.ToString();
                txtCropBottom.Text = _cropRect.Bottom.ToString();
            }
            else
            {
                txtCropLeft.Text = "0";
                txtCropTop.Text = "0";
                txtCropRight.Text = "0";
                txtCropBottom.Text = "0";
            }
            _updatingCropTextBoxes = false;
        }

        private void UpdateCropLimitLabels()
        {
            Rectangle screenBounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
            lblCropLeftMax.Text = "0";
            lblCropTopMax.Text = "0";
            lblCropRightMax.Text = screenBounds.Width.ToString();
            lblCropBottomMax.Text = screenBounds.Height.ToString();
        }

        private void CmbCropPatterns_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbCropPatterns.SelectedIndex < 0) return;

            // Save current to settings (actually it's already saved via property setter if we kept them in sync)
            // But we need to switch the index in settings
            _settings.SelectedPatternIndex = cmbCropPatterns.SelectedIndex;
            
            // Load new rect
            _cropRect = _settings.CropRect;
            UpdateCropTextBoxes();
            _guidelineOverlay?.UpdateCropRect(_cropRect);
            Log($"Switched to Pattern {cmbCropPatterns.SelectedIndex + 1}: {_cropRect}");
        }

        private void NumMaxPatterns_ValueChanged(object? sender, EventArgs e)
        {
            _settings.MaxPatterns = (int)numMaxPatterns.Value;
            _settings.EnsurePatterns();
            UpdatePatternComboBox();
        }

        private void UpdatePatternComboBox()
        {
            int currentSel = cmbCropPatterns.SelectedIndex;
            cmbCropPatterns.Items.Clear();
            for (int i = 0; i < _settings.MaxPatterns; i++)
            {
                cmbCropPatterns.Items.Add($"Pattern {i + 1}");
            }
            
            if (currentSel >= 0 && currentSel < cmbCropPatterns.Items.Count)
            {
                cmbCropPatterns.SelectedIndex = currentSel;
            }
            else if (cmbCropPatterns.Items.Count > 0)
            {
                cmbCropPatterns.SelectedIndex = 0;
            }
        }

        private void BtnNamingOptions_Click(object? sender, EventArgs e)
        {
            // Temporarily disable TopMost and hide overlay to prevent Z-order issues
            bool wasTopMost = this.TopMost;
            this.TopMost = false;
            _guidelineOverlay?.Hide();

            try
            {
                using (var form = new NamingOptionsForm(_settings))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        _settings.Save();
                        Log("Naming options updated.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error opening options: {ex.Message}");
                MessageBox.Show($"Failed to open options: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Restore state
                this.TopMost = wasTopMost;
                _guidelineOverlay?.Show();
            }
        }

        private async void BtnStart_Click(object? sender, EventArgs e)
        {
            if (btnStart.Text == "Resume")
            {
                // Resume logic: handled by restarting RunAutomation with current state
            }
            else
            {
                _capturedImages.Clear();
            }

            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle == IntPtr.Zero)
            {
                Log("Error: Kindle window not found!");
                MessageBox.Show("Please open Kindle for PC first.");
                return;
            }

            // Get book title and set as default PDF filename (only on first start, not resume)
            if (btnStart.Text != "Resume")
            {
                string? bookTitle = _automation.GetBookTitleFromWindow(kindleHandle);
                if (!string.IsNullOrEmpty(bookTitle))
                {
                    txtOutput.Text = bookTitle + ".pdf";
                    Log($"Book title detected: {bookTitle}");
                }
            }

            if (!int.TryParse(txtInterval.Text, out int interval)) interval = 1000;
            if (!int.TryParse(txtPages.Text, out int maxPages)) maxPages = 10;
            bool autoDetect = chkAutoDetect.Checked;
            bool stopAtLast = chkStopAtLastPage.Checked;
            bool isRightToLeft = cmbDirection.SelectedIndex == 0;

            if (_cts == null || _cts.IsCancellationRequested) _cts = new CancellationTokenSource();
            
            string tempDir = Path.Combine(Path.GetTempPath(), "KindleToPDF_Temp");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            btnStart.Enabled = false;
            btnStop.Enabled = true;
            btnAbort.Enabled = true;
            Log("Starting automation... Press DELETE to pause.");

            // Hide guideline overlay during capture
            _guidelineOverlay?.Hide();

            this.Hide();
            await Task.Delay(500);

            try
            {
                int startIndex = _capturedImages.Count;
                await Task.Run(() => RunAutomation(kindleHandle, interval, maxPages, tempDir, autoDetect, stopAtLast, startIndex, _cts.Token, isRightToLeft));
            }
            catch (OperationCanceledException)
            {
                Log("Automation stopped/paused.");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
            finally
            {
                this.Show();
                this.TopMost = chkAlwaysOnTop.Checked;
                
                // Show guideline overlay again
                _guidelineOverlay?.Show();
                
                if (_cts.IsCancellationRequested)
                {
                    // Stopped manually via Stop button (or Abort, but Abort handles UI reset itself if called from UI thread... wait, if Abort cancels token, we come here)
                    // If Abort was clicked, we might have already cleared images.
                    
                    if (btnAbort.Enabled && _capturedImages.Count == 0) 
                    {
                         // Likely aborted. UI reset is handled by BtnAbort_Click? 
                         // No, BtnAbort_Click calls ResetUI.
                         // But if we are in this finally block, we might overwrite UI state?
                         // Let's check if we are already reset?
                         if (btnStart.Text == "Start" && btnStart.Enabled) 
                         {
                             // Already reset, do nothing
                         }
                         else
                         {
                             // Stopped manually via Stop button
                             await FinalizePdf();
                             ResetUI();
                         }
                    }
                    else
                    {
                         // Stopped manually via Stop button
                         await FinalizePdf();
                         ResetUI();
                    }
                }
                else
                {
                    // Paused (ESC) or Finished
                    if (_capturedImages.Count >= maxPages && !stopAtLast)
                    {
                        // Finished normally (page count reached)
                        await FinalizePdf();
                        ResetUI();
                    }
                    else if (stopAtLast && _capturedImages.Count > 0) // Heuristic for stopAtLast finish
                    {
                         // Paused or Finished logic...
                         // If we are here, we are NOT cancelled.
                         // If we detected last page, we actually cancel the token inside RunAutomation now?
                         // Let's check RunAutomation logic.
                         // Yes: _cts.Cancel(); break;
                         // So if last page detected, we go to "if (_cts.IsCancellationRequested)" block above.
                         
                         // So if we are here, it means we broke loop via DELETE key (Pause).
                         
                         Log("Paused. Press Resume to continue, Stop to finish, or Abort to discard.");
                         btnStart.Text = "Resume";
                         btnStart.Enabled = true;
                         btnStop.Enabled = true;
                         btnAbort.Enabled = true;
                    }
                    else
                    {
                        Log("Paused. Press Resume to continue, Stop to finish, or Abort to discard.");
                        btnStart.Text = "Resume";
                        btnStart.Enabled = true;
                        btnStop.Enabled = true;
                        btnAbort.Enabled = true;
                    }
                }
            }
        }

        private Button btnAbort = null!;

        private async void BtnStop_Click(object? sender, EventArgs e)
        {
            if (btnStart.Text == "Resume")
            {
                Log("Stopping from paused state...");
                await FinalizePdf();
                ResetUI();
            }
            else
            {
                _cts?.Cancel();
            }
        }

        private void BtnAbort_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to abort? All captured images will be discarded.", "Confirm Abort", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                Log("Aborted by user. Discarding captured images.");
                _capturedImages.Clear();
                _cts?.Cancel(); // Ensure any running task is cancelled if this is called during run (though it's mostly for pause)
                ResetUI();
            }
        }

        private void BtnTop_Click(object? sender, EventArgs e)
        {
            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle != IntPtr.Zero)
            {
                _automation.BringWindowToFront(kindleHandle);
                _automation.SendHome(kindleHandle);
                Log("Sent Home key.");
            }
            else
            {
                Log("Kindle window not found.");
            }
        }

        private void BtnPrev_Click(object? sender, EventArgs e)
        {
            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle != IntPtr.Zero)
            {
                _automation.BringWindowToFront(kindleHandle);
                bool isRightToLeft = cmbDirection.SelectedIndex == 0;
                _automation.SendPrevPage(kindleHandle, isRightToLeft);
                Log("Sent Prev Page command.");
            }
            else
            {
                Log("Kindle window not found.");
            }
        }

        private void BtnNext_Click(object? sender, EventArgs e)
        {
            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle != IntPtr.Zero)
            {
                _automation.BringWindowToFront(kindleHandle);
                bool isRightToLeft = cmbDirection.SelectedIndex == 0;
                _automation.SendNextPage(kindleHandle, isRightToLeft);
                Log("Sent Next Page command.");
            }
            else
            {
                Log("Kindle window not found.");
            }
        }

        private void BtnFullScreen_Click(object? sender, EventArgs e)
        {
            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle != IntPtr.Zero)
            {
                _automation.BringWindowToFront(kindleHandle);
                _automation.ToggleFullScreen(kindleHandle);
                Log("Toggled Full Screen (F11).");
            }
            else
            {
                Log("Kindle window not found.");
            }
        }

        private void BtnBottom_Click(object? sender, EventArgs e)
        {
            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle != IntPtr.Zero)
            {
                _automation.BringWindowToFront(kindleHandle);
                Log("Attempting to go to last page...");
                // Run in task to avoid freezing UI during UIA operations
                Task.Run(() => 
                {
                    try
                    {
                        _automation.GoToLastPage(kindleHandle);
                        Invoke(new Action(() => Log("Sent Go to Last Page command.")));
                    }
                    catch (Exception ex)
                    {
                        Invoke(new Action(() => Log($"Error navigating to last page: {ex.Message}")));
                    }
                });
            }
            else
            {
                Log("Kindle window not found.");
            }
        }

        private void ResetUI()
        {
            btnStart.Text = "Start";
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnAbort.Enabled = false;
        }

        private void RunAutomation(IntPtr hWnd, int interval, int maxPages, string tempDir, bool autoDetect, bool stopAtLast, int startIndex, CancellationToken token, bool isRightToLeft)
        {
            Bitmap? previousImage = null;
            const int VK_DELETE = 0x2E;

            for (int i = startIndex; i < maxPages || stopAtLast; i++)
            {
                if (_automation.IsKeyDown(VK_DELETE))
                {
                    break; // Pause
                }
                token.ThrowIfCancellationRequested();

                Rectangle bounds = _automation.GetWindowBounds(hWnd);
                if (bounds.Width <= 0 || bounds.Height <= 0) throw new Exception("Invalid window bounds");

                Bitmap rawImage = _automation.CaptureWindow(bounds);
                Bitmap currentImage;

                if (_cropRect != Rectangle.Empty)
                {
                    Rectangle windowRect = bounds;
                    Rectangle screenCrop = _cropRect;
                    
                    int relX = screenCrop.X - windowRect.X;
                    int relY = screenCrop.Y - windowRect.Y;
                    Rectangle relativeCrop = new Rectangle(relX, relY, screenCrop.Width, screenCrop.Height);
                    
                    currentImage = _automation.CropBitmap(rawImage, relativeCrop);
                    rawImage.Dispose();
                }
                else
                {
                    currentImage = rawImage;
                }
                
                if (stopAtLast && previousImage != null)
                {
                    if (_automation.AreImagesSame(previousImage, currentImage))
                    {
                        Log("Last page detected (no change). Stopping.");
                        currentImage.Dispose();
                        // If we stop here, we should probably cancel the token to signal "Done"?
                        // Or just break.
                        _cts.Cancel(); // Signal "Done" (Stop)
                        break;
                    }
                }
                
                if (previousImage != null) previousImage.Dispose();
                previousImage = (Bitmap)currentImage.Clone();

                string imgPath = Path.Combine(tempDir, $"page_{i:D4}.png");
                currentImage.Save(imgPath, ImageFormat.Png);
                _capturedImages.Add(imgPath);
                currentImage.Dispose();

                Log($"Captured page {i + 1}");

                if (!stopAtLast && i >= maxPages - 1) break;

                _automation.SendPageTurn(hWnd, isRightToLeft);

                if (autoDetect)
                {
                    bool pageChanged = false;
                    int maxRetries = 40; 
                    int stableCount = 0;
                    Bitmap? lastCheck = null;

                    for (int r = 0; r < maxRetries; r++)
                    {
                        Thread.Sleep(100);
                        if (_automation.IsKeyDown(VK_DELETE)) { break; } // Will be caught next loop
                        token.ThrowIfCancellationRequested();

                        Bitmap currentCheck = _automation.CaptureWindow(bounds);
                        
                        if (lastCheck != null)
                        {
                            if (_automation.AreImagesSame(lastCheck, currentCheck))
                            {
                                stableCount++;
                            }
                            else
                            {
                                stableCount = 0;
                            }
                            lastCheck.Dispose();
                        }
                        lastCheck = currentCheck;

                        if (stableCount >= 2)
                        {
                            // Check if we actually moved from previous page (unless it's the very first page, but previousImage is null then? No, we set it above)
                            // Wait, previousImage is set BEFORE page turn.
                            // So we compare currentCheck with previousImage.
                            if (!_automation.AreImagesSame(previousImage, currentCheck))
                            {
                                pageChanged = true;
                                lastCheck.Dispose();
                                break;
                            }
                        }
                    }
                    if (lastCheck != null) lastCheck.Dispose();

                    if (!pageChanged)
                    {
                        Log("Warning: Page turn not detected (timeout or no change).");
                    }
                }
                else
                {
                    for(int t=0; t<interval; t+=100)
                    {
                        Thread.Sleep(Math.Min(100, interval - t));
                        if (_automation.IsKeyDown(VK_DELETE)) { break; }
                        token.ThrowIfCancellationRequested();
                    }
                }
            }
            if (previousImage != null) previousImage.Dispose();
        }

        private string GetOutputFilePath(string rawPath)
        {
            string dir = Path.GetDirectoryName(rawPath) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(rawPath);
            string ext = Path.GetExtension(rawPath);
            
            if (_settings.Mode == FileNameMode.Overwrite)
            {
                return rawPath;
            }
            
            // Sequential Mode
            string newPath = rawPath;
            
            // If file doesn't exist, we can just use it? 
            // Spec says "Create sequential files (Rename)". 
            // If "Number" type, we might want to enforce the number format even if the base file doesn't exist?
            // Or only if it exists?
            // Usually sequential means "find the next available name".
            // But the user requirements imply a specific format: [BookName]_[Number].pdf
            
            // Let's implement the logic to generate the name based on the pattern, 
            // and if it exists, increment until we find a free one (for Number/Alphabet).
            // For DateTime, we just use the current time.
            
            switch (_settings.SeqType)
            {
                case SequentialType.Number:
                    int currentNum = _settings.StartNumber;
                    while (true)
                    {
                        string suffix = currentNum.ToString("D" + _settings.NumberDigits);
                        newPath = Path.Combine(dir, $"{fileName}_{suffix}{ext}");
                        if (!File.Exists(newPath)) break;
                        currentNum++;
                    }
                    break;
                    
                case SequentialType.Alphabet:
                    string currentChar = _settings.StartChar;
                    while (true)
                    {
                        newPath = Path.Combine(dir, $"{fileName}_{currentChar}{ext}");
                        if (!File.Exists(newPath)) break;
                        currentChar = IncrementAlphabet(currentChar);
                    }
                    break;
                    
                case SequentialType.DateTime:
                    string dateStr = DateTime.Now.ToString(_settings.DateTimeFormat);
                    newPath = Path.Combine(dir, $"{fileName}_{dateStr}{ext}");
                    // If DateTime collision (unlikely with seconds), we might append a number or just overwrite?
                    // Let's assume overwrite for same second, or append counter if really needed.
                    // For now, simple DateTime append.
                    break;
            }
            
            return newPath;
        }

        private string IncrementAlphabet(string s)
        {
            // Simple increment for last char
            if (string.IsNullOrEmpty(s)) return "a";
            char last = s[s.Length - 1];
            if (last == 'z') return s + "a";
            if (last == 'Z') return s + "A";
            return s.Substring(0, s.Length - 1) + (char)(last + 1);
        }

        private async Task FinalizePdf()
        {
            if (_capturedImages.Count == 0)
            {
                Log("No images captured. PDF creation skipped.");
                return;
            }

            Log("Creating PDF...");
            string baseOutputPath = txtOutput.Text;
            if (!Path.IsPathRooted(baseOutputPath))
            {
                baseOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, baseOutputPath);
            }
            
            string finalOutputPath = GetOutputFilePath(baseOutputPath);

            double dpi = 0;
            string dpiStr = (string)(Invoke(new Func<string>(() => cmbDpi.SelectedItem?.ToString() ?? "Default")) ?? "Default");
            if (dpiStr != "Default" && double.TryParse(dpiStr, out double d)) dpi = d;

            ImageColorMode colorMode = _settings.ColorMode;
            int jpegQuality = _settings.JpegQuality;

            await Task.Run(() => _pdfGenerator.CreatePdf(_capturedImages, finalOutputPath, dpi, colorMode, jpegQuality));
            Log($"PDF saved to {finalOutputPath}");
            MessageBox.Show($"PDF creation complete!\nSaved to: {finalOutputPath}");
        }
    }
}
