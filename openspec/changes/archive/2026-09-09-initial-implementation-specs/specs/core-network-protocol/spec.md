## Purpose

Defines the UDP multicast protocol and network communication contract between Mic Helper servers and clients, including auto-discovery, heartbeat transmission, and state updates across isolated ports.

## ADDED Requirements

### Requirement: UDP Multicast Status Broadcasting
The server SHALL broadcast microphone mute status updates and heartbeat signals via UDP multicast to the designated multicast address (239.255.132.5) on the configured port.

#### Scenario: Server transmits mute state update
- **WHEN** the microphone mute state changes on the server PC
- **THEN** the server transmits a UDP multicast status packet containing the server identifier, sequence number, current state (Muted, Unmuted, Disconnected, or Paused), and the target microphone name

#### Scenario: Packet transmission on distinct port
- **WHEN** the server is configured with a custom port number
- **THEN** the server binds and broadcasts exclusively on that configured port, allowing multiple servers to operate independently on the same physical host

### Requirement: Event Burst Transmission
The server SHALL transmit an immediate burst of multiple duplicate packets whenever the microphone status transitions between muted, unmuted, disconnected, or paused to ensure delivery over unreliable UDP networks.

#### Scenario: State transition triggers burst delivery
- **WHEN** the monitored microphone switches from unmuted to muted
- **THEN** the server sends 3 consecutive datagrams spaced within 50 milliseconds to minimize packet loss latency on the local network

### Requirement: Heartbeat and Auto-Discovery
The server SHALL emit periodic heartbeat datagrams on its configured port to announce presence and current state to clients on the local network.

#### Scenario: Periodic heartbeat emission
- **WHEN** the server is running and not in a paused state
- **THEN** it emits a multicast heartbeat datagram once every 1.0 second containing server metadata, host IP, and current mute status

#### Scenario: Client auto-discovers active servers
- **WHEN** a client listens on the configured multicast port
- **THEN** it aggregates active servers by their IP and host metadata to populate the available servers selection list

### Requirement: Client Disconnect Watchdog
The client SHALL monitor the elapsed time since the last valid status or heartbeat packet received from the target server and transition to a disconnected state when the configured timeout expires.

#### Scenario: Server heartbeat timeout
- **WHEN** no packet is received from the target server IP for longer than the configured retry timeout (default 5 seconds)
- **THEN** the client marks the server connection as disconnected and triggers the disconnected state notifications

#### Scenario: Server connection restored
- **WHEN** a valid packet is received from the target server IP while in a disconnected state
- **THEN** the client immediately clears the disconnected status and applies the received microphone state

### Requirement: Server Pause and Disconnect Notification
The server SHALL transmit explicit state transition packets prior to entering paused state or dropping client connections.

#### Scenario: User pauses server
- **WHEN** the user initiates pause from the server application
- **THEN** the server broadcasts an explicit Paused status packet before ceasing regular heartbeat emissions

#### Scenario: Monitored microphone is unplugged
- **WHEN** the server detects the monitored microphone hardware has been disconnected
- **THEN** the server broadcasts a Disconnected status packet and halts active audio monitoring until the device returns
