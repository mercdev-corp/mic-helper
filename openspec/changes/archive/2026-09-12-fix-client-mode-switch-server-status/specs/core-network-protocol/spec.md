## MODIFIED Requirements

### Requirement: Client Disconnect Watchdog
The client SHALL monitor the elapsed time since the last valid status or heartbeat packet received from the target server and transition to a disconnected state when the configured timeout expires.

#### Scenario: Server heartbeat timeout
- **WHEN** no packet is received from the target server IP for longer than the configured retry timeout (default 5 seconds)
- **THEN** the client marks the server connection as disconnected and triggers the disconnected state notifications

#### Scenario: Server connection restored
- **WHEN** a valid packet is received from the target server IP while in a disconnected state
- **THEN** the client immediately clears the disconnected status and applies the received microphone state

#### Scenario: Listener stopped clears connection state
- **WHEN** the client stops the UDP listener
- **THEN** the listener immediately resets its connection state to disconnected so that subsequent activations detect incoming server connections freshly
