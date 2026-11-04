# Sprite boundary audit (2026-11-03)

Scope: all 19 embedded character images: sixteen 4×2 PNG sheets (128 frames), two single PNGs (`sleep`, `peek`), and one single JPEG (`completion`). Completion uses the whole image and has no inter-frame boundary.

Checked both horizontal and vertical cuts, every cell's nonempty content, and a two-pixel edge margin matching the loader's crop padding. Alpha >= 8 is treated as visible for boundary checks, stricter than the loader's alpha >= 40 content bounds. Very faint alpha 1–5 noise exists in some gutters and is not treated as a neighboring character fragment.

| Sheets | Result |
|---|---|
| balloon, clean, doze, focus, heart, look, notices, payday, snack, sprites, stretch, tickle, umbrella, water, welcome | Existing equal cuts have clear margins; no boundary corrections needed. |
| roll | Row cut 443 crossed artwork ending at 448; previous fix moved it to 478. First column cut 443 lacked the two-pixel margin on the lower-left pose; now moved to 478, inside the shared transparent gutter. |
| sleep, peek | Single images; outer edges have clear margins. |
| completion.jpg | Whole-image display; no sheet subdivision. |

`CuteMotionTests.AuditSheets` reads every PNG and checks the actual `SpriteCellRect` geometry used in production. It checks 128 frame boundaries plus the two single-image borders and fails if the asset inventory changes without review. Source PNGs remain unchanged.
