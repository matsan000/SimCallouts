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
        private readonly Mp3Playback _mp3Playback;
        private readonly ElevenLabsSpeechEngine _elevenLabs;

        private readonly TextBox _txtSimBriefId = new();
        private readonly RoundedSwitch _chkBrowserImport = new();
        private readonly RoundedSwitch _chkWebDashboard = new();
        private readonly TextBox _txtDashboardPort = new();
        private readonly RoundedSlider _sldVolume = new();
        private readonly Label _lblVolumeValue = new();
        private readonly RoundedSwitch _chkV1 = new();
        private readonly RoundedSwitch _chkRotate = new();
        private readonly RoundedSwitch _chkPositiveRate = new();
        private readonly RoundedSwitch _chkThrustReduction = new();
        private readonly RoundedSwitch _chkAccel = new();
        private readonly RoundedSwitch _chkTenThousandFt = new();
        private readonly RoundedSwitch _chkTransitionAltitude = new();
        private readonly RoundedSwitch _chkTransitionLevel = new();
        private readonly RoundedSwitch _chkEightyKnots = new();
        private readonly RoundedSwitch _chkHundredKnots = new();
        private readonly RoundedSwitch _chkOneThousandFeet = new();
        private readonly RoundedSwitch _chkFiveHundredFeet = new();
        private readonly RoundedSwitch _chkMinimums = new();
        private readonly ComboBox _cmbVoice = new();
        private readonly RoundedButton _btnTestVoice = new();
        private readonly RoundedSwitch _chkUseRecordedSounds = new();
        private readonly Label _lblRecordedSoundsStatus = new();
        private readonly RoundedButton _btnTestRecordedSound = new();
        private readonly RoundedSwitch _chkUseElevenLabs = new();
        private readonly TextBox _txtElevenLabsApiKey = new();
        private readonly TextBox _txtElevenLabsVoiceId = new();
        private readonly RoundedButton _btnTestElevenLabs = new();
        private readonly Label _lblElevenLabsStatus = new();
        private readonly RoundedButton _btnSave = new();
        private readonly RoundedButton _btnCancel = new();

        public ConfigForm(Preferences preferences, SpeechSynthesizer speech, Mp3Playback mp3Playback, ElevenLabsSpeechEngine elevenLabs)
        {
            _preferences = preferences;
            _speech = speech;
            _mp3Playback = mp3Playback;
            _elevenLabs = elevenLabs;

            Text = "Settings";
            Font = new Font("Segoe UI", 10f);
            BackColor = UiStyle.BackgroundColor;
            ClientSize = new Size(480, 1180);
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
                RowCount = 8,
                BackColor = UiStyle.BackgroundColor
            };
            // Padding(36) + header(~37) + label(~24) + 40px input field + switch row(~54) +
            // note(~55, now three lines) = ~246, so 270 leaves enough room for everything.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
            // Padding(36) + header(~37) + slider row(~28) + margin(10) + note(~20) = ~131.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
            // Padding(36) + header(~37) + combo(40) + margin(10) + button(38) = ~161.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 175));
            // Padding(36) + header(~37) + switch row(~46) + status label(~20) + margin(8) +
            // button(38) = ~185.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
            // Padding(36) + header(~37) + switch row(~46) + two labelled 40px input fields
            // (~24 each) + margins + button(38) + status label(~20) = ~325.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 340));
            // Padding(36) + header(~37) + nine ~46px switch rows = ~487.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 500));
            // Padding(36) + header(~37) + four ~46px switch rows = ~257.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 275));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var cardSimBrief = UiStyle.CreateCard("SimBrief", out Panel simBriefContent);
            cardSimBrief.Margin = new Padding(0, 0, 0, 14);
            BuildSimBriefContent(simBriefContent);
            root.Controls.Add(cardSimBrief, 0, 0);

            var cardVolume = UiStyle.CreateCard("Volume", out Panel volumeContent);
            cardVolume.Margin = new Padding(0, 0, 0, 14);
            BuildVolumeContent(volumeContent);
            root.Controls.Add(cardVolume, 0, 1);

            var cardVoice = UiStyle.CreateCard("Voice", out Panel voiceContent);
            cardVoice.Margin = new Padding(0, 0, 0, 14);
            BuildVoiceContent(voiceContent);
            root.Controls.Add(cardVoice, 0, 2);

            var cardRecordedSounds = UiStyle.CreateCard("Recorded Sounds", out Panel recordedSoundsContent);
            cardRecordedSounds.Margin = new Padding(0, 0, 0, 14);
            BuildRecordedSoundsContent(recordedSoundsContent);
            root.Controls.Add(cardRecordedSounds, 0, 3);

            var cardElevenLabs = UiStyle.CreateCard("ElevenLabs API", out Panel elevenLabsContent);
            cardElevenLabs.Margin = new Padding(0, 0, 0, 14);
            BuildElevenLabsContent(elevenLabsContent);
            root.Controls.Add(cardElevenLabs, 0, 4);

            var cardDeparture = UiStyle.CreateCard("Departure Callouts", out Panel departureContent);
            cardDeparture.Margin = new Padding(0, 0, 0, 14);
            BuildDepartureCalloutsContent(departureContent);
            root.Controls.Add(cardDeparture, 0, 5);

            var cardArrival = UiStyle.CreateCard("Arrival Callouts", out Panel arrivalContent);
            cardArrival.Margin = new Padding(0, 0, 0, 14);
            BuildArrivalCalloutsContent(arrivalContent);
            root.Controls.Add(cardArrival, 0, 6);

            root.Controls.Add(BuildFooter(), 0, 7);

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
                RowCount = 7,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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

            var webDashboardRow = UiStyle.CreateSwitchRow(
                "Enable local web dashboard", _chkWebDashboard);
            webDashboardRow.Margin = new Padding(0, 14, 0, 6);

            var dashboardPortRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };
            dashboardPortRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            dashboardPortRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var lblDashboardPort = new Label
            {
                Text = "Port:",
                AutoSize = true,
                ForeColor = UiStyle.TextColor,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 8, 0)
            };
            _txtDashboardPort.Width = 80;
            _txtDashboardPort.Margin = new Padding(0);
            UiStyle.StyleTextBox(_txtDashboardPort);
            var dashboardPortField = UiStyle.CreateInputField(_txtDashboardPort);
            dashboardPortField.Width = 100;
            dashboardPortField.Anchor = AnchorStyles.Left;

            dashboardPortRow.Controls.Add(lblDashboardPort, 0, 0);
            dashboardPortRow.Controls.Add(dashboardPortField, 1, 0);

            var lblWebDashboardNote = new Label
            {
                // Same "here's the URL" framing as SimPrinter's own dashboard note - also
                // where RealEFB's Add Website App quick-add button points by default (see
                // WEBSITE_APP_QUICK_ADD in RealEFB's app.js), so changing this port means using
                // "Choose Image"/typing the URL by hand there instead.
                Text = "A read-only status page (connection, current flight, briefed V1/Rotate, " +
                       "recent callouts) at http://localhost:<port> - nothing here can trigger a " +
                       "callout or change a setting. Meant to be added as a Website App in " +
                       "RealEFB so this is visible without switching windows.",
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
            layout.Controls.Add(webDashboardRow, 0, 4);
            layout.Controls.Add(dashboardPortRow, 0, 5);
            layout.Controls.Add(lblWebDashboardNote, 0, 6);
            content.Controls.Add(layout);
        }

        // Applies to every playback engine at once (SAPI, recorded sounds, ElevenLabs) - live
        // as you drag, so the Test buttons in the cards below immediately reflect it; only
        // written to Preferences on Save, same as everything else in this dialog. 100% is the
        // original, unadjusted volume every engine already played at, so nothing changes for
        // existing users until they move the slider themselves. SAPI's own volume ceiling is
        // 100 regardless (Windows doesn't expose amplification past that for it), but the
        // recorded-sound/ElevenLabs engines can go up to 200% - true amplification, not just
        // "back to normal" - since that's what actually helps when a recording is too quiet
        // even at its original level.
        private void BuildVolumeContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var sliderRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                BackColor = UiStyle.CardBackgroundColor
            };
            sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            UiStyle.StyleSlider(_sldVolume);
            _sldVolume.Minimum = 0;
            _sldVolume.Maximum = 200;
            _sldVolume.Dock = DockStyle.Fill;
            _sldVolume.Margin = new Padding(0, 0, 12, 0);
            _sldVolume.ValueChanged += (_, _) =>
            {
                _lblVolumeValue.Text = $"{_sldVolume.Value}%";
                ApplyVolumeLive();
            };

            _lblVolumeValue.AutoSize = false;
            _lblVolumeValue.Text = "100%";
            _lblVolumeValue.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _lblVolumeValue.ForeColor = UiStyle.TextColor;
            _lblVolumeValue.Anchor = AnchorStyles.Right;
            _lblVolumeValue.Size = new Size(48, 28);
            _lblVolumeValue.TextAlign = ContentAlignment.MiddleRight;

            sliderRow.Controls.Add(_sldVolume, 0, 0);
            sliderRow.Controls.Add(_lblVolumeValue, 1, 0);

            var lblNote = new Label
            {
                Text = "100% is the original volume every sound already played at - turn it up if " +
                       "callouts are hard to hear, or down if they're too loud.",
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                Font = new Font("Segoe UI", 8f),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 8, 0, 0)
            };

            layout.Controls.Add(sliderRow, 0, 0);
            layout.Controls.Add(lblNote, 0, 1);
            content.Controls.Add(layout);
        }

        private void ApplyVolumeLive()
        {
            _speech.Volume = Math.Clamp(_sldVolume.Value, 0, 100);
            _mp3Playback.Volume = _sldVolume.Value / 100f;
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

        // Only covers the 13 fixed callouts (see RecordedSoundEngine) - briefings always use
        // whichever text-based engine (ElevenLabs or SAPI) is configured below, since they're
        // built from live flight data and can't be a single static recording.
        private void BuildRecordedSoundsContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var switchRow = UiStyle.CreateSwitchRow("Use recorded sound files for callouts", _chkUseRecordedSounds);
            switchRow.Margin = new Padding(0, 0, 0, 6);

            bool hasAll = RecordedSoundEngine.HasAllFiles;
            _lblRecordedSoundsStatus.AutoSize = true;
            _lblRecordedSoundsStatus.Font = new Font("Segoe UI", 8f);
            _lblRecordedSoundsStatus.Text = hasAll
                ? "All 13 callout files found in assets\\Sounds."
                : "Some callout files are missing from assets\\Sounds - those callouts will fall back to the voice below.";
            _lblRecordedSoundsStatus.ForeColor = hasAll ? UiStyle.SuccessColor : UiStyle.MutedTextColor;
            _lblRecordedSoundsStatus.MaximumSize = new Size(400, 0);
            _lblRecordedSoundsStatus.Margin = new Padding(2, 0, 0, 8);

            _btnTestRecordedSound.Text = "Test (\"V1, Rotate\")";
            _btnTestRecordedSound.AutoSize = false;
            _btnTestRecordedSound.Width = 160;
            _btnTestRecordedSound.Height = 38;
            _btnTestRecordedSound.Anchor = AnchorStyles.Left;
            _btnTestRecordedSound.Click += async (_, _) => await TestRecordedSoundsAsync();
            UiStyle.StyleSecondaryButton(_btnTestRecordedSound);

            layout.Controls.Add(switchRow, 0, 0);
            layout.Controls.Add(_lblRecordedSoundsStatus, 0, 1);
            layout.Controls.Add(_btnTestRecordedSound, 0, 2);

            content.Controls.Add(layout);
        }

        // Covers callouts and briefings alike - every generated clip is cached to disk (see
        // ElevenLabsSpeechEngine) so the same phrase is never paid for or waited on twice.
        private void BuildElevenLabsContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var switchRow = UiStyle.CreateSwitchRow("Use ElevenLabs for callouts and briefings", _chkUseElevenLabs);
            switchRow.Margin = new Padding(0, 0, 0, 10);

            var lblApiKey = new Label
            {
                Text = "API Key",
                AutoSize = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 6)
            };
            _txtElevenLabsApiKey.PasswordChar = '*';
            var apiKeyField = UiStyle.CreateInputField(_txtElevenLabsApiKey);
            apiKeyField.Dock = DockStyle.Top;
            apiKeyField.Margin = new Padding(0, 0, 0, 10);

            var lblVoiceId = new Label
            {
                Text = "Voice ID",
                AutoSize = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 6)
            };
            var voiceIdField = UiStyle.CreateInputField(_txtElevenLabsVoiceId);
            voiceIdField.Dock = DockStyle.Top;
            voiceIdField.Margin = new Padding(0, 0, 0, 10);

            _btnTestElevenLabs.Text = "Test (\"V1. Rotate.\")";
            _btnTestElevenLabs.AutoSize = false;
            _btnTestElevenLabs.Width = 180;
            _btnTestElevenLabs.Height = 38;
            _btnTestElevenLabs.Anchor = AnchorStyles.Left;
            _btnTestElevenLabs.Click += async (_, _) => await TestElevenLabsAsync();
            UiStyle.StyleSecondaryButton(_btnTestElevenLabs);

            _lblElevenLabsStatus.AutoSize = true;
            _lblElevenLabsStatus.Font = new Font("Segoe UI", 8f);
            _lblElevenLabsStatus.ForeColor = UiStyle.MutedTextColor;
            _lblElevenLabsStatus.MaximumSize = new Size(400, 0);
            _lblElevenLabsStatus.Margin = new Padding(2, 8, 0, 0);

            layout.Controls.Add(switchRow, 0, 0);
            layout.Controls.Add(lblApiKey, 0, 1);
            layout.Controls.Add(apiKeyField, 0, 2);
            layout.Controls.Add(lblVoiceId, 0, 3);
            layout.Controls.Add(voiceIdField, 0, 4);
            layout.Controls.Add(_btnTestElevenLabs, 0, 5);
            layout.Controls.Add(_lblElevenLabsStatus, 0, 6);

            content.Controls.Add(layout);
        }

        private async Task TestRecordedSoundsAsync()
        {
            if (RecordedSoundEngine.TryGetPath(Callout.V1, out string v1Path))
                await _mp3Playback.PlayFileAsync(v1Path);
            if (RecordedSoundEngine.TryGetPath(Callout.Rotate, out string rotatePath))
                await _mp3Playback.PlayFileAsync(rotatePath);
        }

        private async Task TestElevenLabsAsync()
        {
            string apiKey = _txtElevenLabsApiKey.Text.Trim();
            string voiceId = _txtElevenLabsVoiceId.Text.Trim();
            if (apiKey.Length == 0 || voiceId.Length == 0)
            {
                _lblElevenLabsStatus.ForeColor = UiStyle.ErrorColor;
                _lblElevenLabsStatus.Text = "Enter both an API key and a voice ID first.";
                return;
            }

            _lblElevenLabsStatus.ForeColor = UiStyle.MutedTextColor;
            _lblElevenLabsStatus.Text = "Generating...";

            string? path = await _elevenLabs.GetOrFetchAudioAsync(apiKey, voiceId, "V1. Rotate.");
            if (path != null)
            {
                _mp3Playback.PlayFile(path);
                _lblElevenLabsStatus.ForeColor = UiStyle.SuccessColor;
                _lblElevenLabsStatus.Text = "Done.";
            }
            else
            {
                _lblElevenLabsStatus.ForeColor = UiStyle.ErrorColor;
                _lblElevenLabsStatus.Text = "Could not reach ElevenLabs - check the API key, voice ID, and your connection.";
            }
        }

        private static void BuildCalloutSwitchRows(Panel content, (string Label, RoundedSwitch Switch)[] rows)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = rows.Length,
                Margin = new Padding(0)
            };
            for (int i = 0; i < rows.Length; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            for (int i = 0; i < rows.Length; i++)
            {
                var row = UiStyle.CreateSwitchRow(rows[i].Label, rows[i].Switch);
                layout.Controls.Add(row, 0, i);
            }

            content.Controls.Add(layout);
        }

        // Takeoff roll through the end of the climb - everything up to level-off at cruise.
        // 10,000 feet also covers the descent case (see its own crossing-direction logic in
        // CalloutTracker), but it's listed here since climbing through it happens first.
        private void BuildDepartureCalloutsContent(Panel content) => BuildCalloutSwitchRows(content, new (string, RoundedSwitch)[]
        {
            ("80 knots (takeoff roll)", _chkEightyKnots),
            ("100 knots (takeoff roll)", _chkHundredKnots),
            ("V1", _chkV1),
            ("Rotate", _chkRotate),
            ("Positive rate", _chkPositiveRate),
            ("Thrust reduction (\"Climb thrust\")", _chkThrustReduction),
            ("Acceleration altitude (\"Bug up\")", _chkAccel),
            ("Transition altitude", _chkTransitionAltitude),
            ("10,000 feet (climb and descent)", _chkTenThousandFt),
        });

        // Descent into landing. 10,000 feet's descent trigger shares the single toggle up in
        // Departure Callouts rather than appearing twice. The 1,000/500 feet gate calls are
        // AGL (radio altitude) and only count a descending crossing, so they don't also fire
        // climbing through those heights right after takeoff.
        private void BuildArrivalCalloutsContent(Panel content) => BuildCalloutSwitchRows(content, new (string, RoundedSwitch)[]
        {
            ("Transition level", _chkTransitionLevel),
            ("1,000 feet AGL (approach)", _chkOneThousandFeet),
            ("500 feet AGL (approach)", _chkFiveHundredFeet),
            ("Minimums (approach)", _chkMinimums),
        });

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
            _chkWebDashboard.Checked = _preferences.EnableWebDashboard;
            _txtDashboardPort.Text = _preferences.WebDashboardPort.ToString();

            // Label text and the live engine volume are set explicitly right after, rather
            // than relying on Value's ValueChanged event - that event doesn't fire when the
            // assigned value happens to equal Value's own starting default, which would
            // otherwise leave both out of sync with the loaded preference.
            _sldVolume.Value = Math.Clamp(_preferences.VolumePercent, _sldVolume.Minimum, _sldVolume.Maximum);
            _lblVolumeValue.Text = $"{_sldVolume.Value}%";
            ApplyVolumeLive();

            _chkV1.Checked = _preferences.EnableV1;
            _chkRotate.Checked = _preferences.EnableRotate;
            _chkPositiveRate.Checked = _preferences.EnablePositiveRate;
            _chkThrustReduction.Checked = _preferences.EnableThrustReduction;
            _chkAccel.Checked = _preferences.EnableAccel;
            _chkTenThousandFt.Checked = _preferences.EnableTenThousandFt;
            _chkTransitionAltitude.Checked = _preferences.EnableTransitionAltitude;
            _chkTransitionLevel.Checked = _preferences.EnableTransitionLevel;
            _chkEightyKnots.Checked = _preferences.EnableEightyKnots;
            _chkHundredKnots.Checked = _preferences.EnableHundredKnots;
            _chkOneThousandFeet.Checked = _preferences.EnableOneThousandFeet;
            _chkFiveHundredFeet.Checked = _preferences.EnableFiveHundredFeet;
            _chkMinimums.Checked = _preferences.EnableMinimums;

            if (!string.IsNullOrEmpty(_preferences.VoiceName) && _cmbVoice.Items.Contains(_preferences.VoiceName))
                _cmbVoice.SelectedItem = _preferences.VoiceName;
            else if (_cmbVoice.Items.Count > 0)
                _cmbVoice.SelectedIndex = 0;

            _chkUseRecordedSounds.Checked = _preferences.UseRecordedSounds;

            _chkUseElevenLabs.Checked = _preferences.UseElevenLabs;
            _txtElevenLabsApiKey.Text = _preferences.ElevenLabsApiKey ?? "";
            _txtElevenLabsVoiceId.Text = _preferences.ElevenLabsVoiceId ?? "";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            _preferences.SimBriefId = _txtSimBriefId.Text.Trim();
            _preferences.EnableBrowserImport = _chkBrowserImport.Checked;
            _preferences.EnableWebDashboard = _chkWebDashboard.Checked;
            if (int.TryParse(_txtDashboardPort.Text, out int dashboardPort) && dashboardPort is > 0 and <= 65535)
                _preferences.WebDashboardPort = dashboardPort;

            _preferences.VolumePercent = _sldVolume.Value;

            _preferences.EnableV1 = _chkV1.Checked;
            _preferences.EnableRotate = _chkRotate.Checked;
            _preferences.EnablePositiveRate = _chkPositiveRate.Checked;
            _preferences.EnableThrustReduction = _chkThrustReduction.Checked;
            _preferences.EnableAccel = _chkAccel.Checked;
            _preferences.EnableTenThousandFt = _chkTenThousandFt.Checked;
            _preferences.EnableTransitionAltitude = _chkTransitionAltitude.Checked;
            _preferences.EnableTransitionLevel = _chkTransitionLevel.Checked;
            _preferences.EnableEightyKnots = _chkEightyKnots.Checked;
            _preferences.EnableHundredKnots = _chkHundredKnots.Checked;
            _preferences.EnableOneThousandFeet = _chkOneThousandFeet.Checked;
            _preferences.EnableFiveHundredFeet = _chkFiveHundredFeet.Checked;
            _preferences.EnableMinimums = _chkMinimums.Checked;

            _preferences.VoiceName = _cmbVoice.SelectedItem as string;

            _preferences.UseRecordedSounds = _chkUseRecordedSounds.Checked;

            _preferences.UseElevenLabs = _chkUseElevenLabs.Checked;
            _preferences.ElevenLabsApiKey = _txtElevenLabsApiKey.Text.Trim();
            _preferences.ElevenLabsVoiceId = _txtElevenLabsVoiceId.Text.Trim();

            _preferences.Save();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
