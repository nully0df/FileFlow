# Changelog

## 0.3.0 — 2026-09-30

- Make category chips clickable filters with an active state, keyboard focus and an All files button.
- Limit Move, Select all and Clear to the current view; preserve checkbox choices when switching categories.
- Enable column-header sorting, including numeric file-size sorting, and keep category/size columns compact.
- Draw consistent row separators across custom and native cells.
- Show destination categories and the source folder in Move confirmation, then display the completed file count.
- Exercise actual Move/Undo confirmation dialogs and verify file locations for both filtered and complete batches.

## 0.2.1 — 2026-09-29

- Fix black corners and duplicated labels by restoring background painting for custom buttons.
- Keep confirmation and rule-editor buttons fully inside their layout rows at 125% scaling.
- Add pixel regression checks for reused buffers, hover exit, label changes and enabled states in the main window and dialogs.

## 0.2.0 — 2026-09-29

- Introduce Stillwater, an original visual language with a sage sidebar, teal actions, rounded surfaces and category colors.
- Add an original vector mark, multi-resolution Windows icon and shared drawing components.
- Redesign the file preview, empty state, rule editor and move/undo confirmation dialogs.
- Add Select all / Clear and explicit Add rule / Remove selected controls.
- Show rule validation inside the editor and retain native Windows window controls.
- Fix layout scaling at 125% and extend form-level workflow checks.
- Publish a downloadable Windows build with each successful GitHub Actions run.

## 0.1.0 — 2026-09-29

- Add manual folder preview and per-file selection.
- Add editable and persistent extension rules with six default categories.
- Add collision naming and stale-preview checks.
- Add persistent, content-checked undo and prepared journal recovery.
- Add cancellation, partial-result reporting and sample files.
- Add a WinForms desktop interface, filesystem checks and a UI workflow harness.
