# Shop CNC Labeler Agent Handover

## Current incident

On 2026-08-19 the Excitech/OSAI label process repeatedly cycled while running:

```text
22_OHC_Divider_Recut.anc
```

The controller entered machine subprogram `P2M701`, displayed `@I54=0`, and did not reach the programmed U/V label position.

The Excitech Label Printing V3.0 application then reported:

```text
The requested address is not valid in its context
Socket.Bind(localEP)
```

## Working diagnosis

This is currently a printer/controller handshake and network-binding failure, not evidence of a U/V coordinate error.

Configured addresses shown on the machine:

| Role | Address |
|---|---|
| OSAI controller | `192.168.0.2` |
| Label printer | `192.168.0.3` |
| Windows label-network adapter | `192.168.0.4` |

The bind exception means Windows could not bind the label application to `192.168.0.4`. Confirm that the physical Ethernet adapter connected to the controller/printer is statically configured as:

```text
IP:      192.168.0.4
Mask:    255.255.255.0
Gateway: blank
DNS:     blank
```

Then verify:

```text
ping 192.168.0.2
ping 192.168.0.3
```

Do not resume Process 2 until `Connect Controller` succeeds without the bind/disconnected error.

## Label image location

The machine label application is configured with:

```text
Print picture path: D:\CNC
```

The required files must therefore be directly in that folder:

```text
D:\CNC\OHC_OH_D0_2.bmp
D:\CNC\OHC_OH_D1_2.bmp
```

They must not remain under `D:\CNC\label\`, and the real names must not end in `.bmp.bmp`.

## ANC sequence under investigation

```text
LS11='OHC_OH_D0_2'
M701
(GTO,ST01,E41=0)
G90 G0 V218.491 U226.266
M702
(GTO,ST01,E42=0)
```

Observed execution was still inside `M701`/`P2M701`. Therefore the U/V move had not started.

The `E41/E42` retry branches have no explicit timeout and should be treated as a separate safety risk. Do not bypass machine inputs or remove waits to force execution.

## OmniCam implementation notes

Relevant files:

```text
dotnet/src/CabinetNC.Domain/Manufacturing/LabelExport.cs
dotnet/src/CabinetNC.Desktop/LabelBmp.cs
dotnet/src/CabinetNC.Desktop/MainWindow.xaml.cs
```

Current behavior:

- ANC uses `LS11='<bitmap stem>'`.
- BMPs are generated as 236×157, 24-bit files.
- Desktop export creates a `label` subfolder.
- UI text currently instructs users to flatten-copy BMPs to `D:\Label`.

The shop machine is configured for `D:\CNC`, so the hard-coded UI guidance does not match this machine.

## Recommended software follow-up

1. Make the machine label-image destination configurable; use `D:\CNC` for this shop profile.
2. Export a flat, machine-ready transfer folder or show an explicit list of BMPs that must accompany each ANC.
3. Validate that every `LS11` stem has a matching BMP before export completes.
4. Add bounded retry/error handling for `E41/E42` instead of unlimited loops.
5. Do not change label U/V calculations until network connection and `M701` completion are proven.

## Acceptance checklist

- Label application connects without `Socket.Bind(localEP)` errors.
- Controller `.2` and printer `.3` respond from the label PC.
- `M701` completes and `@I54` leaves the waiting state.
- The controller then executes the expected U/V move.
- `M702` completes once without returning to `ST01`.
- Both labels print, pick, and apply correctly.

## Detailed incident record

See:

```text
docs/sprint/LABELING_INCIDENT_HANDOVER_2026-08-19.md
```
