## MODIFIED Requirements

### Requirement: Context Menu Navigation
The client application SHALL provide a right-click context menu on the system tray icon with Pause/Resume, Settings, Donate, and Exit options, with Donate positioned immediately below Settings.

#### Scenario: Toggle pause and resume
- **WHEN** the user clicks Pause or Resume in the context menu
- **THEN** the client toggles its paused state, updates the tray icon, hides overlay if paused, saves the paused setting immediately, and suspends or resumes server packet consumption

#### Scenario: Open settings dialog
- **WHEN** the user clicks Settings in the context menu
- **THEN** the movable settings window opens and the overlay switches into interactive WYSIWYG controller mode

#### Scenario: Open donation webpage
- **WHEN** the user clicks Donate in the context menu
- **THEN** the application opens the donation URL from the embedded DONATE resource in the default web browser

#### Scenario: Exit application
- **WHEN** the user clicks Exit in the context menu
- **THEN** the client destroys the overlay window, removes the system tray icon, and exits cleanly
