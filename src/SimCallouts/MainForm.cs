using System.Globalization;
using System.Speech.Synthesis;

namespace SimCallouts
{
    public class MainForm : Form
    {
        private readonly RoundedButton _btnImportFlight = new();
        private readonly RoundedButton _btnDepartureBriefing = new();
        private readonly RoundedButton _btnArrivalBriefing = new();
        private readonly RoundedButton _btnSettings = new();
        private readonly TextBox _txtV1 = new();
        private readonly TextBox _txtRotate = new();
        private readonly TextBox _txtThrustReductionAlt = new();
        private readonly TextBox _txtAccelAlt = new();
        private readonly TextBox _txtTransitionAlt = new();
        private readonly TextBox _txtTransitionLevel = new();
        private readonly TextBox _txtMinimums = new();
        private readonly RoundedButton _btnSave = new();
        private readonly Label _lblSaveStatus = new();
        private readonly Label _lblStatus = new();
        private readonly Label _lblConnection = new();

        private readonly Preferences _preferences = Preferences.Load();
        private readonly SimConnectClient _simConnect = new();
        private readonly CalloutTracker _tracker = new();
        private readonly SpeechSynthesizer _speech = new();
        private readonly LocalImportServer _importServer = new();
        private readonly Mp3Playback _mp3Playback = new();
        private readonly ElevenLabsSpeechEngine _elevenLabs = new();

        private SimBriefFlightPlan? _currentPlan;
        private Panel _cardSpeeds = null!;
        private Panel _speedsContent = null!;

        public MainForm()
        {
            Text = "SimCallouts";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            Font = new Font("Segoe UI", 10f);
            BackColor = UiStyle.BackgroundColor;
            Size = new Size(580, 880);
            MinimumSize = new Size(380, 400);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            Padding = new Padding(20);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            LoadPreferencesIntoUi();
            StartSimConnect();
            StartImportServer();

            // _lblStatus is AutoSize, which measures its full text on one line regardless of
            // window width - without a cap it just runs past the window edge instead of
            // wrapping. Recomputing MaximumSize on every resize keeps it wrapping within
            // whatever width is actually available, at any window size.
            Resize += (_, _) => UpdateStatusLabelWrapWidth();
            UpdateStatusLabelWrapWidth();
        }

        private void UpdateStatusLabelWrapWidth()
        {
            int available = ClientSize.Width - Padding.Horizontal - 4;
            _lblStatus.MaximumSize = new Size(Math.Max(100, available), 0);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _simConnect.Dispose();
            _speech.Dispose();
            _importServer.Dispose();
            _mp3Playback.Dispose();
            base.OnFormClosed(e);
        }

        // ============================== SimConnect / tracker wiring ==============================

        /// <summary>
        /// SimPrinter's browser extension only talks to SimPrinter's own localhost port -
        /// SimCallouts can't also bind that exact port, so SimPrinter instead relays whatever
        /// SimBrief performance text it receives on to this port. When that arrives, pull V1
        /// and VR out of it and fill them straight into the fields (and Save immediately, so
        /// the tracker picks the new values up right away without a manual Save click).
        /// </summary>
        private void StartImportServer()
        {
            _importServer.OnTextReceived += text => BeginInvoke(new Action(() => ApplyImportedPerformanceText(text)));

            if (_preferences.EnableBrowserImport)
                _importServer.Start();
        }

        private void ApplyImportedPerformanceText(string text)
        {
            if (!PerformanceCalcParser.TryParseVSpeeds(text, out double v1, out double vr))
            {
                _lblStatus.ForeColor = UiStyle.ErrorColor;
                _lblStatus.Text = "Received a SimBrief calculation but couldn't find V1/VR in it.";
                return;
            }

            if (v1 > 0)
            {
                _txtV1.Text = v1.ToString(CultureInfo.InvariantCulture);
                _preferences.V1Kts = v1;
            }
            if (vr > 0)
            {
                _txtRotate.Text = vr.ToString(CultureInfo.InvariantCulture);
                _preferences.RotateKts = vr;
            }

            _tracker.Configure(_preferences.V1Kts, _preferences.RotateKts, _preferences.ThrustReductionAltFt,
                _preferences.AccelAltFt, _preferences.TransitionAltFt, _preferences.TransitionLevelFt,
                _preferences.MinimumsAglFt);
            _preferences.Save();

            _lblSaveStatus.ForeColor = UiStyle.SuccessColor;
            _lblSaveStatus.Text = "Saved.";
            _lblStatus.ForeColor = UiStyle.SuccessColor;
            _lblStatus.Text = $"Filled V1/VR from SimBrief: V1 {v1:0}, VR {vr:0}.";
        }

        private void StartSimConnect()
        {
            _simConnect.Connected += () => BeginInvoke(new Action(() =>
            {
                _lblConnection.Text = "Connected to simulator.";
                _lblConnection.ForeColor = UiStyle.SuccessColor;
            }));
            _simConnect.Disconnected += () => BeginInvoke(new Action(() =>
            {
                _lblConnection.Text = "Not connected - waiting for the simulator...";
                _lblConnection.ForeColor = UiStyle.MutedTextColor;
            }));
            _simConnect.FlightStateUpdated += state => BeginInvoke(new Action(() => _tracker.Update(state)));

            _tracker.CalloutReached += callout => BeginInvoke(new Action(() => Speak(callout)));

            _lblConnection.Text = "Not connected - waiting for the simulator...";
            _lblConnection.ForeColor = UiStyle.MutedTextColor;
        }

        private void Speak(Callout callout)
        {
            string text = callout switch
            {
                Callout.V1 => "V1",
                Callout.Rotate => "Rotate",
                Callout.PositiveRate => "Positive rate",
                Callout.ThrustReduction => "Climb thrust",
                Callout.Accel => "Bug up",
                Callout.TenThousandFt => "10,000 feet",
                Callout.TransitionAltitude => "Passing transition altitude",
                Callout.TransitionLevel => "Passing transition level",
                Callout.EightyKnots => "80 knots",
                Callout.HundredKnots => "100 knots",
                Callout.OneThousandFeet => "1,000 feet",
                Callout.FiveHundredFeet => "500 feet",
                Callout.Minimums => "Minimums",
                _ => ""
            };
            if (text.Length == 0) return;

            if (_preferences.UseRecordedSounds && RecordedSoundEngine.TryGetPath(callout, out string path))
            {
                _mp3Playback.PlayFile(path);
                return;
            }
            SpeakText(text);
        }

        // Routes to ElevenLabs (with caching) when configured, otherwise the classic SAPI
        // voice - used for anything recorded sound files can't cover: briefings, and any
        // callout whose file is missing even with UseRecordedSounds on.
        private void SpeakText(string text)
        {
            if (_preferences.UseElevenLabs
                && !string.IsNullOrWhiteSpace(_preferences.ElevenLabsApiKey)
                && !string.IsNullOrWhiteSpace(_preferences.ElevenLabsVoiceId))
            {
                _ = SpeakElevenLabsAsync(text);
                return;
            }
            _speech.SpeakAsyncCancelAll();
            _speech.SpeakAsync(text);
        }

        private async Task SpeakElevenLabsAsync(string text)
        {
            string? path = await _elevenLabs.GetOrFetchAudioAsync(
                _preferences.ElevenLabsApiKey!, _preferences.ElevenLabsVoiceId!, text);

            if (path != null)
            {
                _mp3Playback.PlayFile(path);
            }
            else
            {
                // API call failed (bad key, no network, rate limit, etc.) - still say
                // something rather than going silent.
                _speech.SpeakAsyncCancelAll();
                _speech.SpeakAsync(text);
            }
        }

        // ============================== UI construction ==============================

        private void BuildUi()
        {
            var footer = BuildFooter();
            footer.Dock = DockStyle.Bottom;
            Controls.Add(footer);

            // AutoSize + Dock=Top (not Fill) so contentRoot's height is its own natural total,
            // handed to the AutoScroll host below - shrinking the window past that natural
            // size scrolls instead of clipping the Save button or status lines, which is what
            // actually lets the window go small without corrupting the layout.
            var contentRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiStyle.BackgroundColor
            };
            contentRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            contentRoot.Controls.Add(BuildTopPanel(), 0, 0);

            // CreateCard defaults to Dock=Fill, which is ambiguous when placed in an AutoSize
            // row - RefreshSpeedsCard sets an explicit Height sized to however many rows are
            // actually visible right now, since that count changes as callouts get toggled.
            _cardSpeeds = UiStyle.CreateCard("Callout Settings", out _speedsContent);
            _cardSpeeds.Dock = DockStyle.Top;
            _cardSpeeds.Margin = new Padding(0, 14, 0, 14);
            // Subscribed once here rather than in RefreshSpeedsCard - that method re-adds
            // this same _btnSave instance to a rebuilt layout every time callouts are
            // toggled, and re-subscribing there would stack duplicate handlers.
            _btnSave.Click += BtnSave_Click;
            contentRoot.Controls.Add(_cardSpeeds, 0, 1);

            // Padding(36) + header(~37) + connection line(~25) = ~98.
            var cardStatus = UiStyle.CreateCard("Live Status", out var statusContent);
            cardStatus.Dock = DockStyle.Top;
            cardStatus.Height = 110;
            BuildStatusContent(statusContent);
            contentRoot.Controls.Add(cardStatus, 0, 2);

            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiStyle.BackgroundColor
            };
            scrollHost.Controls.Add(contentRoot);

            Controls.Add(scrollHost);
        }

        private Control BuildTopPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = UiStyle.BackgroundColor,
                Margin = new Padding(0)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _btnImportFlight.Text = "Import Flight";
            _btnImportFlight.AutoSize = false;
            _btnImportFlight.Width = 160;
            _btnImportFlight.Height = 40;
            _btnImportFlight.Anchor = AnchorStyles.Left;
            _btnImportFlight.Margin = new Padding(0, 0, 0, 10);
            _btnImportFlight.Click += BtnImportFlight_Click;
            UiStyle.StylePrimaryButton(_btnImportFlight, UiStyle.BackgroundColor);

            var briefingRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            briefingRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            briefingRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _btnDepartureBriefing.Text = "Departure Briefing";
            _btnDepartureBriefing.AutoSize = false;
            _btnDepartureBriefing.Width = 170;
            _btnDepartureBriefing.Height = 38;
            _btnDepartureBriefing.Margin = new Padding(0, 0, 10, 0);
            _btnDepartureBriefing.Click += BtnDepartureBriefing_Click;
            UiStyle.StyleSecondaryButton(_btnDepartureBriefing, UiStyle.BackgroundColor);

            _btnArrivalBriefing.Text = "Arrival Briefing";
            _btnArrivalBriefing.AutoSize = false;
            _btnArrivalBriefing.Width = 170;
            _btnArrivalBriefing.Height = 38;
            _btnArrivalBriefing.Margin = new Padding(0);
            _btnArrivalBriefing.Click += BtnArrivalBriefing_Click;
            UiStyle.StyleSecondaryButton(_btnArrivalBriefing, UiStyle.BackgroundColor);

            briefingRow.Controls.Add(_btnDepartureBriefing, 0, 0);
            briefingRow.Controls.Add(_btnArrivalBriefing, 1, 0);
            SetBriefingButtonsEnabled(false);

            _lblStatus.AutoSize = true;
            _lblStatus.Text = "Set up your SimBrief ID in Settings, then import your latest flight.";
            _lblStatus.ForeColor = UiStyle.MutedTextColor;
            _lblStatus.Margin = new Padding(2, 0, 0, 0);

            panel.Controls.Add(_btnImportFlight, 0, 0);
            panel.Controls.Add(briefingRow, 0, 1);
            panel.Controls.Add(_lblStatus, 0, 2);

            return panel;
        }

        private void SetBriefingButtonsEnabled(bool enabled)
        {
            _btnDepartureBriefing.Enabled = enabled;
            _btnArrivalBriefing.Enabled = enabled;
        }

        /// <summary>
        /// Rebuilds the V-speed/altitude input rows to only show the ones whose callout is
        /// currently enabled in Settings - called on startup and again whenever Settings is
        /// saved, since toggling a callout off there should make its field disappear here
        /// immediately rather than just leave an inert, confusing input box behind.
        /// </summary>
        private void RefreshSpeedsCard()
        {
            var rows = new (string Label, TextBox Box, bool Enabled)[]
            {
                ("V1", _txtV1, _preferences.EnableV1),
                ("VR (Rotate)", _txtRotate, _preferences.EnableRotate),
                ("T/R Alt (ft)", _txtThrustReductionAlt, _preferences.EnableThrustReduction),
                ("Accel Alt (ft)", _txtAccelAlt, _preferences.EnableAccel),
                ("Trans Alt (ft)", _txtTransitionAlt, _preferences.EnableTransitionAltitude),
                ("Trans Level (ft)", _txtTransitionLevel, _preferences.EnableTransitionLevel),
                ("Minimums (ft)", _txtMinimums, _preferences.EnableMinimums),
            };
            var visibleRows = rows.Where(r => r.Enabled).ToArray();

            _speedsContent.Controls.Clear();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = visibleRows.Length + 1,
                Margin = new Padding(0)
            };
            for (int i = 0; i < visibleRows.Length; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            for (int i = 0; i < visibleRows.Length; i++)
                layout.Controls.Add(BuildLabeledInput(visibleRows[i].Label, visibleRows[i].Box), 0, i);

            var saveRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };
            saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _btnSave.Text = "Save";
            _btnSave.AutoSize = false;
            _btnSave.Width = 120;
            _btnSave.Height = 38;
            _btnSave.Margin = new Padding(0, 0, 12, 0);
            UiStyle.StylePrimaryButton(_btnSave);

            _lblSaveStatus.Dock = DockStyle.Fill;
            _lblSaveStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblSaveStatus.ForeColor = UiStyle.MutedTextColor;

            saveRow.Controls.Add(_btnSave, 0, 0);
            saveRow.Controls.Add(_lblSaveStatus, 1, 0);

            layout.Controls.Add(saveRow, 0, visibleRows.Length);

            _speedsContent.Controls.Add(layout);

            // Padding(36) + header(~37) + 54px per visible row + the save row (~48).
            _cardSpeeds.Height = 73 + visibleRows.Length * 54 + 58;
        }

        private static Control BuildLabeledInput(string labelText, TextBox textBox)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 10)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var label = new Label
            {
                Text = labelText,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiStyle.TextColor
            };

            textBox.TextAlign = HorizontalAlignment.Center;
            textBox.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            textBox.KeyPress += NumericOnly_KeyPress;
            var field = UiStyle.CreateInputField(textBox);
            field.Dock = DockStyle.Fill;

            row.Controls.Add(label, 0, 0);
            row.Controls.Add(field, 1, 0);
            return row;
        }

        private static void NumericOnly_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            var textBox = (TextBox)sender!;
            if (char.IsDigit(e.KeyChar)) return;
            if (e.KeyChar == '.' && !textBox.Text.Contains('.')) return;
            e.Handled = true;
        }

        private void BuildStatusContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Margin = new Padding(0)
            };

            _lblConnection.AutoSize = true;
            _lblConnection.Margin = new Padding(0);

            layout.Controls.Add(_lblConnection, 0, 0);

            content.Controls.Add(layout);
        }

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _btnSettings.Text = "Settings";
            _btnSettings.AutoSize = false;
            _btnSettings.Width = 120;
            _btnSettings.Height = 38;
            _btnSettings.Margin = new Padding(0);
            _btnSettings.Anchor = AnchorStyles.Left;
            _btnSettings.Click += BtnSettings_Click;
            UiStyle.StyleSecondaryButton(_btnSettings, UiStyle.BackgroundColor);

            footer.Controls.Add(_btnSettings, 0, 0);

            return footer;
        }

        // ============================== Preferences / actions ==============================

        private void LoadPreferencesIntoUi()
        {
            _txtV1.Text = _preferences.V1Kts > 0 ? _preferences.V1Kts.ToString(CultureInfo.InvariantCulture) : "";
            _txtRotate.Text = _preferences.RotateKts > 0 ? _preferences.RotateKts.ToString(CultureInfo.InvariantCulture) : "";
            _txtThrustReductionAlt.Text = _preferences.ThrustReductionAltFt > 0
                ? _preferences.ThrustReductionAltFt.ToString(CultureInfo.InvariantCulture) : "";
            _txtAccelAlt.Text = _preferences.AccelAltFt > 0
                ? _preferences.AccelAltFt.ToString(CultureInfo.InvariantCulture) : "";
            _txtTransitionAlt.Text = _preferences.TransitionAltFt > 0
                ? _preferences.TransitionAltFt.ToString(CultureInfo.InvariantCulture) : "";
            _txtTransitionLevel.Text = _preferences.TransitionLevelFt > 0
                ? _preferences.TransitionLevelFt.ToString(CultureInfo.InvariantCulture) : "";
            _txtMinimums.Text = _preferences.MinimumsAglFt > 0
                ? _preferences.MinimumsAglFt.ToString(CultureInfo.InvariantCulture) : "";
            _tracker.Configure(_preferences.V1Kts, _preferences.RotateKts,
                _preferences.ThrustReductionAltFt, _preferences.AccelAltFt,
                _preferences.TransitionAltFt, _preferences.TransitionLevelFt, _preferences.MinimumsAglFt);
            _tracker.ConfigureEnabled(_preferences.EnableV1, _preferences.EnableRotate,
                _preferences.EnablePositiveRate, _preferences.EnableThrustReduction, _preferences.EnableAccel,
                _preferences.EnableTenThousandFt, _preferences.EnableTransitionAltitude, _preferences.EnableTransitionLevel,
                _preferences.EnableEightyKnots, _preferences.EnableHundredKnots,
                _preferences.EnableOneThousandFeet, _preferences.EnableFiveHundredFeet,
                _preferences.EnableMinimums);

            RefreshSpeedsCard();

            if (!string.IsNullOrEmpty(_preferences.VoiceName))
            {
                try { _speech.SelectVoice(_preferences.VoiceName); }
                catch (ArgumentException) { /* voice no longer installed - keep the default */ }
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            double v1 = ParseKts(_txtV1.Text);
            double vr = ParseKts(_txtRotate.Text);
            double thrustReductionAlt = ParseKts(_txtThrustReductionAlt.Text);
            double accelAlt = ParseKts(_txtAccelAlt.Text);
            double transitionAlt = ParseKts(_txtTransitionAlt.Text);
            double transitionLevel = ParseKts(_txtTransitionLevel.Text);
            double minimums = ParseKts(_txtMinimums.Text);

            _tracker.Configure(v1, vr, thrustReductionAlt, accelAlt, transitionAlt, transitionLevel, minimums);
            _preferences.V1Kts = v1;
            _preferences.RotateKts = vr;
            _preferences.ThrustReductionAltFt = thrustReductionAlt;
            _preferences.AccelAltFt = accelAlt;
            _preferences.TransitionAltFt = transitionAlt;
            _preferences.TransitionLevelFt = transitionLevel;
            _preferences.MinimumsAglFt = minimums;
            _preferences.Save();

            _lblSaveStatus.ForeColor = UiStyle.SuccessColor;
            _lblSaveStatus.Text = "Saved.";
        }

        private static double ParseKts(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            using var dlg = new ConfigForm(_preferences, _speech, _mp3Playback, _elevenLabs);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                if (_preferences.EnableBrowserImport)
                    _importServer.Start();
                else
                    _importServer.Stop();

                _tracker.ConfigureEnabled(_preferences.EnableV1, _preferences.EnableRotate,
                    _preferences.EnablePositiveRate, _preferences.EnableThrustReduction, _preferences.EnableAccel,
                    _preferences.EnableTenThousandFt, _preferences.EnableTransitionAltitude, _preferences.EnableTransitionLevel,
                    _preferences.EnableEightyKnots, _preferences.EnableHundredKnots,
                    _preferences.EnableOneThousandFeet, _preferences.EnableFiveHundredFeet,
                    _preferences.EnableMinimums);
                RefreshSpeedsCard();

                _lblStatus.ForeColor = UiStyle.SuccessColor;
                _lblStatus.Text = "Settings saved.";
            }
        }

        private async void BtnImportFlight_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_preferences.SimBriefId))
            {
                MessageBox.Show(this, "Please enter your SimBrief username or pilot ID in Settings first.",
                    "Settings Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnImportFlight.Enabled = false;
            SetBriefingButtonsEnabled(false);
            _lblStatus.ForeColor = UiStyle.MutedTextColor;
            _lblStatus.Text = "Loading latest flight plan from SimBrief...";

            try
            {
                _currentPlan = await SimBriefClient.FetchLatestAsync(_preferences.SimBriefId);
                SetBriefingButtonsEnabled(true);
                _lblStatus.ForeColor = UiStyle.SuccessColor;
                _lblStatus.Text = $"Loaded: {_currentPlan.OriginIcao} -> {_currentPlan.DestIcao} ({_currentPlan.Callsign})";
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = UiStyle.ErrorColor;
                _lblStatus.Text = "Failed to load flight plan.";
                MessageBox.Show(this, ex.Message, "SimBrief Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnImportFlight.Enabled = true;
            }
        }

        private void BtnDepartureBriefing_Click(object? sender, EventArgs e)
        {
            if (_currentPlan == null) return;
            string text = BriefingBuilder.BuildDeparture(_currentPlan, _preferences);
            SpeakText(text);
            _lblStatus.ForeColor = UiStyle.SuccessColor;
            _lblStatus.Text = "Speaking departure briefing...";
        }

        private void BtnArrivalBriefing_Click(object? sender, EventArgs e)
        {
            if (_currentPlan == null) return;
            string text = BriefingBuilder.BuildArrival(_currentPlan, _preferences);
            SpeakText(text);
            _lblStatus.ForeColor = UiStyle.SuccessColor;
            _lblStatus.Text = "Speaking arrival briefing...";
        }
    }
}
