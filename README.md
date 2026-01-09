# TPLink.SwitchClient

A .NET library for managing TP-Link switches via their web interface.

## Description

This library provides a client for interacting with TP-Link managed switches through their web-based administration interface. It handles authentication, session management, and provides a high-level API for common switch management tasks.

## Features

- Port management (enable/disable, speed/duplex configuration)
- Port statistics monitoring (packets transmitted/received)
- VLAN configuration (802.1Q)
- Port PVID assignment
- Cable diagnostics testing
- Session management with automatic login

## Usage

```csharp
using TPLink.SwitchClient;

var options = new SwitchClientOptions
{
    SwitchWebAddress = "http://192.168.1.5",
    Username = "admin",
    Password = "admin"
};

var webClient = new WebClient(options);
var manager = new SwitchManager(webClient);

// Login to the switch
await manager.Login();

// Get port status
var ports = await manager.GetPortStatus();

// Enable/disable a port
await manager.SetPortState(portNumber: 1, enable: true);

// Get VLANs
var vlans = await manager.GetVlans();

// Run cable diagnostics
var results = await manager.RunCableTest(new List<int> { 1, 2, 3 });
```

## Architecture

- **WebClient**: Handles HTTP communication and session management
- **SwitchManager**: Provides high-level operations for switch management
- **Models**: Data structures for ports, VLANs, statistics, and cable test results
- **ILogger**: Interface for logging HTTP requests and responses

## Requirements

- .NET 10.0
- Access to TP-Link switch web interface
