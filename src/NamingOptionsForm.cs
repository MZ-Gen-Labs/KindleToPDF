using System;
using System.Drawing;
using System.Windows.Forms;

namespace KindleToPDF
{
    public class NamingOptionsForm : Form
    {
        private AppSettings _settings;
        private RadioButton rbOverwrite = null!;
        private RadioButton rbSequential = null!;
        private ComboBox cmbSeqType = null!;
        private Panel pnlSeqOptions = null!;
        
        // Number Options
        private Panel pnlNumber = null!;
        private NumericUpDown numStartNumber = null!;
        private NumericUpDown numDigits = null!;

        // Alphabet Options
        private Panel pnlAlphabet = null!;
        private TextBox txtStartChar = null!;

        // DateTime Options
        private Panel pnlDateTime = null!;
        private ComboBox cmbDateTimeFormat = null!;

        private Button btnOK = null!;
        private Button btnCancel = null!;

        public NamingOptionsForm(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            this.TopMost = true; // Ensure it stays on top
            try
            {
                LoadSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Reset to defaults to allow form to open
                _settings.SeqType = SequentialType.Number;
                LoadSettings();
            }
        }

        private void InitializeComponent()
        {
            this.Text = "File Naming Options";
            this.Size = new Size(350, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            int y = 20;
            int x = 20;

            // Mode
            Label lblMode = new Label { Text = "Naming Mode:", Location = new Point(x, y), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            this.Controls.Add(lblMode);
            y += 25;

            rbOverwrite = new RadioButton { Text = "Overwrite existing files", Location = new Point(x + 10, y), AutoSize = true };
            rbOverwrite.CheckedChanged += (s, e) => UpdateUI();
            this.Controls.Add(rbOverwrite);
            y += 25;

            rbSequential = new RadioButton { Text = "Create sequential files (Rename)", Location = new Point(x + 10, y), AutoSize = true };
            rbSequential.CheckedChanged += (s, e) => UpdateUI();
            this.Controls.Add(rbSequential);
            y += 30;

            // Sequential Options
            pnlSeqOptions = new Panel { Location = new Point(x + 20, y), Size = new Size(280, 150) };
            this.Controls.Add(pnlSeqOptions);

            Label lblType = new Label { Text = "Sequential Type:", Location = new Point(0, 0), AutoSize = true };
            pnlSeqOptions.Controls.Add(lblType);

            cmbSeqType = new ComboBox { Location = new Point(100, -3), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSeqType.Items.AddRange(Enum.GetNames(typeof(SequentialType)));
            cmbSeqType.SelectedIndexChanged += (s, e) => UpdateSeqUI();
            pnlSeqOptions.Controls.Add(cmbSeqType);

            // Number Panel
            pnlNumber = new Panel { Location = new Point(0, 30), Size = new Size(280, 100), Visible = false };
            pnlSeqOptions.Controls.Add(pnlNumber);
            
            Label lblStartNum = new Label { Text = "Start Number:", Location = new Point(0, 5), AutoSize = true };
            pnlNumber.Controls.Add(lblStartNum);
            numStartNumber = new NumericUpDown { Location = new Point(100, 3), Minimum = 0, Maximum = 999999 };
            pnlNumber.Controls.Add(numStartNumber);

            Label lblDigits = new Label { Text = "Digits (Pad):", Location = new Point(0, 35), AutoSize = true };
            pnlNumber.Controls.Add(lblDigits);
            numDigits = new NumericUpDown { Location = new Point(100, 33), Minimum = 1, Maximum = 10 };
            pnlNumber.Controls.Add(numDigits);

            // Alphabet Panel
            pnlAlphabet = new Panel { Location = new Point(0, 30), Size = new Size(280, 100), Visible = false };
            pnlSeqOptions.Controls.Add(pnlAlphabet);

            Label lblStartChar = new Label { Text = "Start Char:", Location = new Point(0, 5), AutoSize = true };
            pnlAlphabet.Controls.Add(lblStartChar);
            txtStartChar = new TextBox { Location = new Point(100, 3), Width = 50, MaxLength = 1 };
            pnlAlphabet.Controls.Add(txtStartChar);

            // DateTime Panel
            pnlDateTime = new Panel { Location = new Point(0, 30), Size = new Size(280, 100), Visible = false };
            pnlSeqOptions.Controls.Add(pnlDateTime);

            Label lblFormat = new Label { Text = "Format:", Location = new Point(0, 5), AutoSize = true };
            pnlDateTime.Controls.Add(lblFormat);
            cmbDateTimeFormat = new ComboBox { Location = new Point(100, 3), Width = 150, DropDownStyle = ComboBoxStyle.DropDown }; // Allow custom
            cmbDateTimeFormat.Items.AddRange(new object[] { "yyyyMMdd", "yyyyMMdd_HHmmss", "yyyy-MM-dd", "yyyy-MM-dd_HH-mm-ss" });
            pnlDateTime.Controls.Add(cmbDateTimeFormat);

            // Buttons
            btnCancel = new Button { Text = "Cancel", Location = new Point(230, 270), DialogResult = DialogResult.Cancel };
            this.Controls.Add(btnCancel);

            btnOK = new Button { Text = "OK", Location = new Point(140, 270), DialogResult = DialogResult.OK };
            btnOK.Click += BtnOK_Click;
            this.Controls.Add(btnOK);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void LoadSettings()
        {
            if (_settings.Mode == FileNameMode.Overwrite) rbOverwrite.Checked = true;
            else rbSequential.Checked = true;

            if ((int)_settings.SeqType >= 0 && (int)_settings.SeqType < cmbSeqType.Items.Count)
            {
                cmbSeqType.SelectedIndex = (int)_settings.SeqType;
            }
            else
            {
                cmbSeqType.SelectedIndex = 0; // Default to Number
            }
            numStartNumber.Value = _settings.StartNumber;
            numDigits.Value = _settings.NumberDigits;
            txtStartChar.Text = _settings.StartChar;
            cmbDateTimeFormat.Text = _settings.DateTimeFormat;

            UpdateUI();
        }

        private void UpdateUI()
        {
            pnlSeqOptions.Enabled = rbSequential.Checked;
            UpdateSeqUI();
        }

        private void UpdateSeqUI()
        {
            pnlNumber.Visible = false;
            pnlAlphabet.Visible = false;
            pnlDateTime.Visible = false;

            if (cmbSeqType.SelectedIndex == (int)SequentialType.Number) pnlNumber.Visible = true;
            else if (cmbSeqType.SelectedIndex == (int)SequentialType.Alphabet) pnlAlphabet.Visible = true;
            else if (cmbSeqType.SelectedIndex == (int)SequentialType.DateTime) pnlDateTime.Visible = true;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            _settings.Mode = rbOverwrite.Checked ? FileNameMode.Overwrite : FileNameMode.Sequential;
            _settings.SeqType = (SequentialType)cmbSeqType.SelectedIndex;
            _settings.StartNumber = (int)numStartNumber.Value;
            _settings.NumberDigits = (int)numDigits.Value;
            _settings.StartChar = txtStartChar.Text;
            _settings.DateTimeFormat = cmbDateTimeFormat.Text;
            
            this.Close();
        }
    }
}
