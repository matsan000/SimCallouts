using System.Speech.Synthesis;

namespace SimCallouts
{
    /// <summary>
    /// Settings dialog: SimBrief ID and voice selection. Edits the shared Preferences
    /// instance in place, but only on Save - Cancel leaves it untouched.
    /// </summary>
    public class ConfigForm : Form
    {
        private readonly Preferences _preferences;
        private readonly SpeechSynthesizer _speech;

        private readonly TextBox _txtSimBriefId = new();
        private readonly RoundedSwitch _chkBrowserImport = new();
        private readonly RoundedSwitch _chkV1 = new();
        private readonly RoundedSwitch _chkRotate = new();
        private readonly RoundedSwitch _chkPositiveRate = new();
        private readonly RoundedSwitch _chkThrustReduction = new();
        private readonly RoundedSwitch _chkAccel = new();
        private readonly RoundedSwitch _chkTenThousandFt = new();
        private readonly RoundedSwitch _chkTransitionAltitude = new();
        private readonly RoundedSwitch _chkTransitionLevel = new();
        private readonly ComboBox _cmbVoice = new();
        private readonly RoundedButton _btnTestVoice = new();
        private readonly RoundedButton _btnSave = new();
        private readonly RoundedButton _btnCancel = new();

        public ConfigForm(Preferences preferences, SpeechSynthesizer speech)
        {
            _preferences = preferences;
            _speech = speech;

            Text = "Settings";
            Font = new Font("Segoe UI", 10f);
            BackColor = UiStyle.BackgroundColor;
            ClientSize = new Size(480, 730);
            MinimumSize = new Size(440, 320);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Padding = new Padding(20);

            BuildUi();
            ApplyPreferencesToUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = UiStyle.BackgroundColor
            };
            // Padding(36) + header(~37) + label(~24) + 40px input field + switch row(~54) +
            // note(~55, now three lines) = ~246, so 270 leaves enough room for everything.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
            // Padding(36) + header(~37) + combo(40) + margin(10) + button(38) = ~161.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 175));
            // Padding(36) + header(~37) + eight ~46px switch rows = ~441.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 460));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var cardSimBrief = UiStyle.CreateCard("SimBrief", out Panel simBriefContent);
            cardSimBrief.Margin = new Padding(0, 0, 0, 14);
            BuildSimBriefContent(simBriefContent);
            root.Controls.Add(cardSimBrief, 0, 0);

            var cardVoice = UiStyle.CreateCard("Voice", out Panel voiceContent);
            cardVoice.Margin = new Padding(0, 0, 0, 14);
            BuildVoiceContent(voiceContent);
            root.Controls.Add(cardVoice, 0, 1);

            var cardCallouts = UiStyle.CreateCard("Callouts", out Panel calloutsContent);
            cardCallouts.Margin = new Padding(0, 0, 0, 14);
            BuildCalloutsContent(calloutsContent);
            root.Controls.Add(cardCallouts, 0, 2);

            root.Controls.Add(BuildFooter(), 0, 3);

            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiStyle.BackgroundColor
            };
            scrollHost.Controls.Add(root);

            Controls.Add(scrollHost);
        }

        private void BuildSimBriefContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblId = new Label
            {
                Text = "SimBrief username or pilot ID",
                AutoSize = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 8)
            };

            var field = UiStyle.CreateInputField(_txtSimBriefId);
            field.Dock = DockStyle.Top;
            field.Margin = new Padding(0, 0, 0, 14);

            var importRow = UiStyle.CreateSwitchRow(
                "Auto-fill V1/Rotate from SimBrief performance calculations", _chkBrowserImport);
            importRow.Margin = new Padding(0, 0, 0, 6);

            var lblImportNote = new Label
            {
                Text = "Requires the SimPrinter browser extension in Firefox, with \"SimCallouts\" " +
                       "picked as the send-to target on SimBrief's calculator - SimPrinter itself " +
                       "doesn't need to be running.",
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                Font = new Font("Segoe UI", 8f),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 0)
            };

            layout.Controls.Add(lblId, 0, 0);
            layout.Controls.Add(field, 0, 1);
            layout.Controls.Add(importRow, 0, 2);
            layout.Controls.Add(lblImportNote, 0, 3);
            content.Controls.Add(layout);
        }

        private void BuildVoiceContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            UiStyle.StyleComboBox(_cmbVoice);
            _cmbVoice.Dock = DockStyle.Fill;
            _cmbVoice.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbVoice.Margin = new Padding(0, 0, 0, 10);
            foreach (var voice in _speech.GetInstalledVoices())
            {
                if (voice.Enabled)
                    _cmbVoice.Items.Add(voice.VoiceInfo.Name);
            }
            _cmbVoice.SelectedIndexChanged += (_, _) =>
            {
                if (_cmbVoice.SelectedItem is string name) _speech.SelectVoice(name);
            };

            _btnTestVoice.Text = "Test Voice";
            _btnTestVoice.AutoSize = false;
            _btnTestVoice.Width = 140;
            _btnTestVoice.Height = 38;
            _btnTestVoice.Anchor = AnchorStyles.Left;
            _btnTestVoice.Click += (_, _) =>
            {
                _speech.SpeakAsyncCancelAll();
                _speech.SpeakAsync("V1. Rotate.");
            };
            UiStyle.StyleSecondaryButton(_btnTestVoice);

            layout.Controls.Add(_cmbVoice, 0, 0);
            layout.Controls.Add(_btnTestVoice, 0, 1);

            content.Controls.Add(layout);
        }

        private void BuildCalloutsContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                Margin = new Padding(0)
            };
            for (int i = 0; i < 8; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var rows = new (string Label, RoundedSwitch Switch)[]
            {
                ("V1", _chkV1),
                ("Rotate", _chkRotate),
                ("Positive rate", _chkPositiveRate),
                ("Thrust reduction (\"Climb thrust\")", _chkThrustReduction),
                ("Acceleration altitude (\"Bug up\")", _chkAccel),
                ("10,000 feet", _chkTenThousandFt),
                ("Transition altitude", _chkTransitionAltitude),
                ("Transition level", _chkTransitionLevel),
            };

            for (int i = 0; i < rows.Length; i++)
            {
                var row = UiStyle.CreateSwitchRow(rows[i].Label, rows[i].Switch);
                layout.Controls.Add(row, 0, i);
            }

            content.Controls.Add(layout);
        }

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _btnCancel.Text = "Cancel";
            _btnCancel.AutoSize = false;
            _btnCancel.Width = 110;
            _btnCancel.Height = 38;
            _btnCancel.Margin = new Padding(0, 0, 10, 0);
            _btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            UiStyle.StyleSecondaryButton(_btnCancel, UiStyle.BackgroundColor);

            _btnSave.Text = "Save";
            _btnSave.AutoSize = false;
            _btnSave.Width = 110;
            _btnSave.Height = 38;
            _btnSave.Margin = new Padding(0);
            _btnSave.Click += BtnSave_Click;
            UiStyle.StylePrimaryButton(_btnSave, UiStyle.BackgroundColor);

            footer.Controls.Add(_btnCancel, 1, 0);
            footer.Controls.Add(_btnSave, 2, 0);

            return footer;
        }

        private void ApplyPreferencesToUi()
        {
            _txtSimBriefId.Text = _preferences.SimBriefId;
            _chkBrowserImport.Checked = _preferences.EnableBrowserImport;

            _chkV1.Checked = _preferences.EnableV1;
            _chkRotate.Checked = _preferences.EnableRotate;
            _chkPositiveRate.Checked = _preferences.EnablePositiveRate;
            _chkThrustReduction.Checked = _preferences.EnableThrustReduction;
            _chkAccel.Checked = _preferences.EnableAccel;
            _chkTenThousandFt.Checked = _preferences.EnableTenThousandFt;
            _chkTransitionAltitude.Checked = _preferences.EnableTransitionAltitude;
            _chkTransitionLevel.Checked = _preferences.EnableTransitionLevel;

            if (!string.IsNullOrEmpty(_preferences.VoiceName) && _cmbVoice.Items.Contains(_preferences.VoiceName))
                _cmbVoice.SelectedItem = _preferences.VoiceName;
            else if (_cmbVoice.Items.Count > 0)
                _cmbVoice.SelectedIndex = 0;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            _preferences.SimBriefId = _txtSimBriefId.Text.Trim();
            _preferences.EnableBrowserImport = _chkBrowserImport.Checked;

            _preferences.EnableV1 = _chkV1.Checked;
            _preferences.EnableRotate = _chkRotate.Checked;
            _preferences.EnablePositiveRate = _chkPositiveRate.Checked;
            _preferences.EnableThrustReduction = _chkThrustReduction.Checked;
            _preferences.EnableAccel = _chkAccel.Checked;
            _preferences.EnableTenThousandFt = _chkTenThousandFt.Checked;
            _preferences.EnableTransitionAltitude = _chkTransitionAltitude.Checked;
            _preferences.EnableTransitionLevel = _chkTransitionLevel.Checked;

            _preferences.VoiceName = _cmbVoice.SelectedItem as string;
            _preferences.Save();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
