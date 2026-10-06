# Scoped font repair

Read only for an existing drawing's PDF/font problem. If the user asks for Windows Computer Use, invoke that skill and reproduce the exact dialog through its supported JS APIs before relying on COM error codes.

Use `scripts/repair_pdf.ps1` with absolute `-Drawing` (must match active saved SLDDRW), `-UpdatedDrawing` (new SLDDRW), `-Pdf` (new PDF), `-Output` (new JSON report), and `-Font` (installed font-family name). Optional `-InteropPath` overrides registry discovery. Windows PowerShell 5.1 is required; for scriptblock execution set `SW_DRAWING_SKILL_ROOT` as for run.ps1. User must authorize font replacement; the helper creates a separate drawing copy and does not overwrite the input.

Check actual glyph coverage, including diameter and degree symbols, before choosing a font. The helper changes document defaults, every annotation text format and table cell; a successful native drawing save is distinct from PDF success. It saves/reopens to persist the change, then performs a single export attempt. Read the report's `pdf_api_success`, `pdf_error` and `status`; PDF_FAILED files are diagnostic artifacts and are not deliverables. Render and inspect a successful PDF before claiming its contents are complete.

The observed Arial Unicode MS failure remained after complete SimSun/STSong replacement. Do not cycle through those same fonts again by default. Report the specific missing font and consult the official SolidWorks documentation. Installing a new font is a distinct repair action, not something this helper does.

## Verified PDF delivery workaround

For a saved, unmodified, active single-sheet SLDDRW, use `scripts/export_pdf.ps1 -Drawing <absolute input.SLDDRW> -Pdf <new absolute output.pdf> -Output <new absolute report.json> -Python <python executable>`. Optional `-InteropPath` and scriptblock `SW_DRAWING_SKILL_ROOT` work as above. Python must have pypdf. The helper creates an isolated `*_native_export/native_work.SLDDRW` copy, prints the native drawing through Microsoft Print to PDF, temporarily hides annotations for a native SaveAs geometry export, restores visibility, composes both PDFs, and reopens the saved working copy. Input drawing disk hash is verified unchanged. It does not generate or recalculate annotation text.

Local single-sheet A2 verification: the composed PDF retained five native views including the section, the overall dimensions, fourteen hole labels, the hole coordinate table and Chinese notes. Output contains vector paths and text, zero image objects. Source assistance and manufacturing-review limits remain unchanged. The SaveAs font defect is bypassed, not repaired; describe the final PDF as composed native output, never as a successful one-call SaveAs export.

The helper rejects multiple sheets, existing outputs, modified/unsaved inputs, missing printer and unsupported matching paper sizes. It prints at 100% (`IPageSetup.Scale2=100`, not 1), then corrects only tiny printer paper rounding differences under 0.1 PDF point. It restores document printer/page settings and annotation visibility. Partial files/report are diagnostic unless the final status is `COMPOSED_NATIVE_OUTPUT_REVIEW_REQUIRED` and visual QA passes. Full native printing alone lost model contours locally, so do not deliver the intermediate print PDF merely because Chinese text exists.

Always render and compare geometry/annotation registration, sheet edges, section hatching, engineering symbols, Chinese text and table rows. This is a tested delivery workaround for one sample, not proof of general batch reliability. Native printing may differ between graphics/printer environments.
