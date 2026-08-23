# Changelog

All notable changes to SimCallouts are documented here.

## [1.2.0] - 2026-08-24

### Added
- **Recorded sound files** as a voice option: drop your own MP3 for each of the 13 fixed
  callouts into `assets/Sounds` and enable it in Settings -> Recorded Sounds. Settings shows
  whether all 13 files were found, and a Test button plays "V1" then "Rotate" back to back.
- **ElevenLabs API** as a voice option: paste your own API key and voice ID in Settings ->
  ElevenLabs API for realistic AI narration of both callouts and departure/arrival briefings.
  Every generated clip is cached to disk (`%APPDATA%\SimCallouts\ElevenLabsCache`), keyed by
  the exact text and voice, so the same phrase is only ever generated - and billed - once.
- Settings -> ElevenLabs API has its own Test button that generates (or reuses the cached)
  "V1. Rotate." and plays it back, with status text on success/failure.

### Changed
- Voice selection is now layered: recorded files win for a callout when enabled and present,
  ElevenLabs is used next when configured, and the built-in SAPI voice remains the always-
  available fallback for anything the other two can't cover (a missing recording, no
  ElevenLabs key, or a briefing when only recorded sounds are enabled).

## [1.1.0] - Split callouts into Departure/Arrival, new callouts

### Added
- 80 knots / 100 knots takeoff-roll cross-check callouts (off by default).
- 1,000 feet / 500 feet AGL approach gate calls (off by default).
- Minimums callout, off a user-entered AGL height (off by default - most aircraft call their
  own off the FMC/radio altimeter).
- Go-around re-arm: if an approach gate call already fired and the aircraft climbs back above
  2,500 ft AGL, those calls become available again for a second approach in the same flight.

### Changed
- 10,000 feet now fires descending through it too, not just while climbing.
- Settings -> Callouts is split into separate "Departure Callouts" and "Arrival Callouts"
  cards for readability.

### Fixed
- The SimBrief auto-fill note in Settings incorrectly said SimPrinter needed to be running;
  it only requires the Firefox extension.

## [1.0.1] - MSI/screenshot fix

### Fixed
- Settings dialog's auto-fill note text corrected (see 1.1.0 above for the same fix carried
  forward) and the settings screenshot replaced with one showing the full dialog.

## [1.0.0] - Initial release

Speaks realistic takeoff/climb callouts (V1, Rotate, Positive rate, Climb thrust, Bug up,
10,000 feet, transition altitude/level) off live SimConnect data, with:
- Per-callout on/off toggles.
- SimBrief flight plan import.
- Spoken departure/arrival briefings.
- V1/Rotate auto-fill via the SimPrinter browser extension.
