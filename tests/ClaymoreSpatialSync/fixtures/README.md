# Independent Claymore II wire-address fixture

`claymore-ii-main-ansi.csv` contains the 87 ANSI main-key addresses from the explicit `vKeysRight`, `vKeysLeft`, and `vKeyNames` arrays in SignalRGB's Claymore II plugin:

https://gitlab.com/signalrgb/signal-plugins/-/blob/d1b7c903a250bd1d318f9685b93e8fc3ac1de648/Plugins/Asus/ASUS_ROG_Claymore_II.js

The exporter removes numpad/logo entries and normalizes key labels to G-Helper's UI names. Left-side addresses come from the reference's left-side array, not the production implementation. All 87 were also checked to equal the right-side address plus 0x20.

The published revision uses matched ANSI names and address arrays. Later revisions append ISO addresses without maintaining the ANSI name-array length, so their positional arrays must not be blindly zipped together. ISO-only 0x0C and 0x6B addresses were cross-checked against revision b3ad58ded220eb4069c3bfd70033ad561c54af40; an ISO Enter is represented twice in the UI but shares LED 0x7B.

The fixture was added before changing production code. The old production assembly failed at F1: expected 0x18, received 0x10. The corrected implementation passes both complete left/right maps and targeted checks for the user's 9, 0, O, P, J, L, N, M report.

This is a protocol reference, not a physical-device validation. USB packets are captured by a fake transport in the tests; actual illumination remains to be confirmed on the user's device.
