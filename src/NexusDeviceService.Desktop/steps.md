Prompt 11 — Real device discovery


Implement the first real hardware integration behind the existing discovery interfaces.
Target Windows device discovery only. Initially identify USB devices and available COM ports using documented Windows APIs and supported libraries.
Requirements:
•	Report hardware identifiers, interface identifiers, friendly name, connection type and detected port where accessible.
•	Separate physical device identity from individual interfaces belonging to the same device.
•	Avoid claiming that a device is a modem based solely on its friendly name.
•	Handle unplug/replug events, permissions, unavailable drivers and device enumeration errors.
•	Make the provider injectable and independently testable.
•	Keep hardware-specific communication outside the ViewModels.
•	Never write to device firmware or change configuration during discovery.
•	Do not collect or display sensitive identifiers unless needed; redact them in logs.
Start with read-only detection. Provide manual verification instructions in Windows Device Manager and tests using mocked enumeration results.
Do not add AT commands or modem unlock functionality yet.


Prompt 12 — Read-only modem communication


Implement a separately registered modem communication provider using documented interfaces and supported commands for a specifically identified, authorized test device.
Begin by documenting the exact interface, driver, model and supported protocol. Do not guess commands from the manufacturer name alone.
Provide:
•	Connection and disconnection lifecycle.
•	Timeouts.
•	Cancellation.
•	Serialized access to the communication channel.
•	Response parsing.
•	Structured logs with sensitive-data redaction.
•	Explicit indication when the interface is unsupported.
•	Mock transport for automated tests.
Start with non-destructive, read-only information queries. Validate commands against authoritative device documentation or an explicitly authorized test environment.
Do not perform SIM-lock bypass, security-code extraction, firmware changes or undocumented destructive commands. Do not silently fall back to a different interface after a failure.
Acceptance criteria include hardware-independent tests and a manual test report for the specific device used.
Prompt 13 — Firmware workflow without writing
Implement a firmware preparation and validation workflow, but do not write firmware to a physical device yet.
Provide:
•	File selection.
•	Supported-format identification.
•	File size and cryptographic hash calculation.
•	Manifest parsing where documented.
•	Device/model/firmware compatibility checks based on verified metadata.
•	Signature verification where the format provides a verifiable signature.
•	Clear unsupported-format and invalid-file errors.
•	Operation preview and confirmation UI.
•	Audit log of validation results.
Do not infer compatibility from filenames alone. Do not fabricate firmware metadata. Never bypass signature checks or download untrusted firmware automatically.
Use sample fixtures in tests and label them as fixtures.
Acceptance criteria: malformed, truncated, incompatible and unsupported files are rejected with useful explanations. No physical device is modified.

Prompt 14 — Authorized firmware flashing
Design the flashing provider only for a specific, documented, authorized device family and an official or otherwise legitimately authorized firmware package.
Before implementation, write a compatibility specification listing exact models, hardware revisions, firmware formats, required drivers, documented flashing protocol and recovery requirements.
Implement only the verified supported path. Reuse the operation lifecycle, capability checks, cancellation policy and audit logging.
Require:
•	Exact device and firmware match.
•	File integrity and signature validation where supported.
•	Battery/power and connection checks when applicable.
•	Explicit destructive-operation confirmation.
•	Recovery instructions.
•	Prevention of concurrent operations.
•	Clear partial-failure states.
Do not guess undocumented command sequences, bypass authentication, modify calibration/security partitions, or force unsupported firmware. Do not treat cancellation as safe after an irreversible write has begun.
If the required protocol or recovery path cannot be verified, implement the UI and validation contracts only, and report the missing prerequisites instead of guessing.


Prompt 15 — Authorized SIM-unlock integration
Design a SIM-unlock service abstraction for devices where the manufacturer, carrier or authorized service provider supplies a legitimate unlock mechanism.
First determine the supported device models, regional/carrier restrictions, eligibility rules, documented API or procedure, and authorization requirements.
Separate:
•	Eligibility checks.
•	User consent.
•	Authorization request.
•	Operation status.
•	Result verification.
•	Audit history.
Keep provider credentials on the server when a server integration is used. Never place secret keys in the desktop binary. Do not invent a working unlock method for a model simply because another model supports unlocking.
Do not implement credential extraction, authentication bypasses, unauthorized lock-counter manipulation or exploit-based unlocks.
If no authorized mechanism is available, provide an honest unsupported status and guidance to the appropriate carrier/manufacturer service route.
Use mock authorization responses in development and automated tests. Clearly distinguish mock success from a real completed unlock.


Run Review the phase you just implemented against its prompt and acceptance criteria.
1.	Inspect all changed files.
2.	Build the solution.
3.	Run relevant tests.
4.	Fix compilation errors and regressions introduced by your changes.
5.	Check that existing functionality has not been accidentally removed.
6.	Check architecture boundaries and remove unnecessary duplication.
7.	Report exact build/test results, remaining warnings, files changed and any unmet acceptance criteria.
Do not start the next phase. Stop and report the verified state.



