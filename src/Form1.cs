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
        private AutomationLogic _automation;
        private PdfGenerator _pdfGenerator;
        private CancellationTokenSource _cts;
        private List<string> _capturedImages;

        private Button btnStart;
        private Button btnStop;
        private Label lblInterval;
        private TextBox txtInterval;
        private Label lblPages;
        private TextBox txtPages;
        private Label lblOutput;
        private TextBox txtOutput;
        private CheckBox chkAutoDetect;
        private CheckBox chkStopAtLastPage;
        private CheckBox chkAlwaysOnTop;
        private Label lblDpi;
        private ComboBox cmbDpi;
        private TextBox txtLog;
        private Button btnSetCrop;
        private Button btnRefreshTitle;
        private Label lblCropLeft, lblCropTop, lblCropRight, lblCropBottom;
        private TextBox txtCropLeft, txtCropTop, txtCropRight, txtCropBottom;
        private Label lblCropLeftMax, lblCropTopMax, lblCropRightMax, lblCropBottomMax;
        private Rectangle _cropRect = Rectangle.Empty;
        private GuidelineOverlay _guidelineOverlay;

        private AppSettings _settings;

        public Form1()
        {
            InitializeComponent();
            InitializeCustomControls();
            _automation = new AutomationLogic();
            _pdfGenerator = new PdfGenerator();
            _capturedImages = new List<string>();
            
            _settings = AppSettings.Load();
            ApplySettingsToUI();

            // Create and show guideline overlay
            _guidelineOverlay = new GuidelineOverlay();
            _guidelineOverlay.Show();
            _guidelineOverlay.UpdateCropRect(_cropRect);

            this.FormClosing += Form1_FormClosing;
        }

        private void ApplySettingsToUI()
        {
            txtInterval.Text = _settings.Interval.ToString();
            txtPages.Text = _settings.PageCount.ToString();
            chkAutoDetect.Checked = _settings.AutoDetect;
            chkStopAtLastPage.Checked = _settings.StopAtLastPage;
            chkAlwaysOnTop.Checked = _settings.AlwaysOnTop;
            this.TopMost = _settings.AlwaysOnTop;
            if (_settings.DpiIndex >= 0 && _settings.DpiIndex < cmbDpi.Items.Count)
                cmbDpi.SelectedIndex = _settings.DpiIndex;
            
            _cropRect = _settings.CropRect;
            if (_cropRect != Rectangle.Empty)
            {
                Log($"Loaded crop area: {_cropRect}");
            }
            UpdateCropTextBoxes();
            UpdateCropLimitLabels();

            // Load book title on startup
            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle != IntPtr.Zero)
            {
                _automation.BringWindowToFront(kindleHandle); // Bring Kindle to front
                string bookTitle = _automation.GetBookTitleFromWindow(kindleHandle);
                if (!string.IsNullOrEmpty(bookTitle))
                {
                    txtOutput.Text = bookTitle + ".pdf";
                    Log($"Book title detected on startup: {bookTitle}");
                }
                else
                {
                    txtOutput.Text = "output.pdf";
                }
            }
            else
            {
                txtOutput.Text = "output.pdf";
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (int.TryParse(txtInterval.Text, out int interval)) _settings.Interval = interval;
            if (int.TryParse(txtPages.Text, out int pages)) _settings.PageCount = pages;
            _settings.AutoDetect = chkAutoDetect.Checked;
            _settings.StopAtLastPage = chkStopAtLastPage.Checked;
            _settings.AlwaysOnTop = chkAlwaysOnTop.Checked;
            _settings.DpiIndex = cmbDpi.SelectedIndex;
            _settings.CropRect = _cropRect;

            _settings.Save();
            
            // Cleanup guideline overlay
            _guidelineOverlay?.Close();
            _guidelineOverlay?.Dispose();
        }

        private void InitializeCustomControls()
        {
            this.Size = new Size(400, 700);
            this.Text = "Kindle to PDF Automation";

            int y = 20;
            
            lblInterval = new Label { Text = "Interval (ms):", Location = new Point(20, y), AutoSize = true };
            txtInterval = new TextBox { Text = "1000", Location = new Point(150, y - 3) };
            this.Controls.Add(lblInterval);
            this.Controls.Add(txtInterval);

            y += 40;
            lblPages = new Label { Text = "Page Count:", Location = new Point(20, y), AutoSize = true };
            txtPages = new TextBox { Text = "10", Location = new Point(150, y - 3) };
            this.Controls.Add(lblPages);
            this.Controls.Add(txtPages);

            y += 40;
            chkAutoDetect = new CheckBox { Text = "Auto-detect Page Turn", Location = new Point(20, y), AutoSize = true, Checked = true };
            this.Controls.Add(chkAutoDetect);

            y += 30;
            chkStopAtLastPage = new CheckBox { Text = "Stop at Last Page", Location = new Point(20, y), AutoSize = true, Checked = true };
            this.Controls.Add(chkStopAtLastPage);

            y += 30;
            chkAlwaysOnTop = new CheckBox { Text = "Always on Top", Location = new Point(20, y), AutoSize = true, Checked = true };
            chkAlwaysOnTop.CheckedChanged += (s, e) => { this.TopMost = chkAlwaysOnTop.Checked; };
            this.Controls.Add(chkAlwaysOnTop);

            y += 40;
            lblDpi = new Label { Text = "PDF DPI:", Location = new Point(20, y), AutoSize = true };
            cmbDpi = new ComboBox { Location = new Point(150, y - 3), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDpi.Items.AddRange(new object[] { "Default", "300", "450", "600" });
            cmbDpi.SelectedIndex = 0;
            this.Controls.Add(lblDpi);
            this.Controls.Add(cmbDpi);

            y += 40;
            btnSetCrop = new Button { Text = "Set Crop Area", Location = new Point(20, y), Size = new Size(120, 30) };
            btnSetCrop.Click += BtnSetCrop_Click;
            this.Controls.Add(btnSetCrop);

            y += 40;
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
            txtOutput = new TextBox { Text = "output.pdf", Location = new Point(150, y - 3), Width = 150 };
            btnRefreshTitle = new Button { Text = "更新", Location = new Point(310, y - 5), Size = new Size(50, 26) };
            btnRefreshTitle.Click += BtnRefreshTitle_Click;
            this.Controls.Add(lblOutput);
            this.Controls.Add(txtOutput);
            this.Controls.Add(btnRefreshTitle);

            y += 50;
            btnStart = new Button { Text = "Start", Location = new Point(50, y), Size = new Size(100, 40) };
            btnStart.Click += BtnStart_Click;
            this.Controls.Add(btnStart);

            btnStop = new Button { Text = "Stop", Location = new Point(160, y), Size = new Size(100, 40), Enabled = false };
            btnStop.Click += BtnStop_Click;
            this.Controls.Add(btnStop);

            btnAbort = new Button { Text = "Abort", Location = new Point(270, y), Size = new Size(80, 40), Enabled = false, BackColor = Color.LightPink };
            btnAbort.Click += BtnAbort_Click;
            this.Controls.Add(btnAbort);

            y += 60;
            txtLog = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Location = new Point(20, y), Size = new Size(340, 150), ReadOnly = true };
            this.Controls.Add(txtLog);
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(Log), message);
                return;
            }
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}");
        }

        private void BtnSetCrop_Click(object sender, EventArgs e)
        {
            this.Hide();
            using (OverlayForm overlay = new OverlayForm(_cropRect))
            {
                if (overlay.ShowDialog() == DialogResult.OK)
                {
                    _cropRect = overlay.CropRect;
                    Log($"Crop area set: {_cropRect}");
                    UpdateCropTextBoxes();
                    _guidelineOverlay?.UpdateCropRect(_cropRect);
                }
            }
            this.Show();
        }

        private void BtnRefreshTitle_Click(object sender, EventArgs e)
        {
            IntPtr kindleHandle = _automation.GetKindleWindow();
            if (kindleHandle == IntPtr.Zero)
            {
                Log("Error: Kindle window not found!");
                MessageBox.Show("Kindleウィンドウが見つかりません。Kindle for PCを起動してください。");
                return;
            }

            string bookTitle = _automation.GetBookTitleFromWindow(kindleHandle);
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

        private void TxtCrop_TextChanged(object sender, EventArgs e)
        {
            if (_updatingCropTextBoxes) return;

            if (int.TryParse(txtCropLeft.Text, out int left) &&
                int.TryParse(txtCropTop.Text, out int top) &&
                int.TryParse(txtCropRight.Text, out int right) &&
                int.TryParse(txtCropBottom.Text, out int bottom))
            {
                _cropRect = new Rectangle(left, top, right - left, bottom - top);
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
            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            lblCropLeftMax.Text = "0";
            lblCropTopMax.Text = "0";
            lblCropRightMax.Text = screenBounds.Width.ToString();
            lblCropBottomMax.Text = screenBounds.Height.ToString();
        }

        private async void BtnStart_Click(object sender, EventArgs e)
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
                string bookTitle = _automation.GetBookTitleFromWindow(kindleHandle);
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
                await Task.Run(() => RunAutomation(kindleHandle, interval, maxPages, tempDir, autoDetect, stopAtLast, startIndex, _cts.Token));
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

        private Button btnAbort;

        private async void BtnStop_Click(object sender, EventArgs e)
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

        private void BtnAbort_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to abort? All captured images will be discarded.", "Confirm Abort", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                Log("Aborted by user. Discarding captured images.");
                _capturedImages.Clear();
                _cts?.Cancel(); // Ensure any running task is cancelled if this is called during run (though it's mostly for pause)
                ResetUI();
            }
        }

        private void ResetUI()
        {
            btnStart.Text = "Start";
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnAbort.Enabled = false;
        }

        private void RunAutomation(IntPtr hWnd, int interval, int maxPages, string tempDir, bool autoDetect, bool stopAtLast, int startIndex, CancellationToken token)
        {
            Bitmap previousImage = null;
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

                _automation.SendPageTurn(hWnd);

                if (autoDetect)
                {
                    bool pageChanged = false;
                    int maxRetries = 40; 
                    int stableCount = 0;
                    Bitmap lastCheck = null;

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

        private async Task FinalizePdf()
        {
            if (_capturedImages.Count == 0)
            {
                Log("No images captured.");
                return;
            }

            Log("Generating PDF...");
            string outputPath = txtOutput.Text;
            if (string.IsNullOrWhiteSpace(outputPath)) outputPath = "output.pdf";

            double dpi = 0;
            string dpiStr = (string)Invoke(new Func<string>(() => cmbDpi.SelectedItem.ToString()));
            if (dpiStr != "Default" && double.TryParse(dpiStr, out double d)) dpi = d;

            await Task.Run(() => _pdfGenerator.CreatePdf(_capturedImages, outputPath, dpi));
            Log($"PDF saved to {outputPath}");
            MessageBox.Show($"PDF creation complete!\nSaved to: {outputPath}");
        }
    }
}
