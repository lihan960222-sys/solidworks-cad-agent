# Drawing plan v1

All paths are absolute. JSON has no executable expressions. Each run needs a new native output filename. Units: plan positions/distances mm, scales dimensionless. Geometry facts retain `*_si` metres/radians; `bounds_mm` is provided for convenience.

Required root fields:

- `version`: 1
- `facts`: path to the inspect JSON
- `output_drawing`: a new `.SLDDRW` path
- `template`: an installed `.drwdot` path; pass an explicit template appropriate to the target environment
- `sheet`: `{ "width_mm": 594, "height_mm": 420, "first_angle": true }`; arrange views consistently with the projection convention
- `views`: nonempty array `{ "id": "front", "model_view": <exact name from facts>, "position_mm": [x,y], "scale": 1, "reason": <engineering choice> }`
- `dimensions`: array `{ "view": "front", "direction": "horizontal" | "vertical", "expected_mm": <exact expected span>, "position_mm": [x,y], "evidence": <facts reference and coordinate meaning> }`. Only extremal visible-vertex overall spans are supported; rounded silhouettes without extremal vertices must not be assumed equivalent to their bounding box.
- Optional `diameters`: array `{ "view": "front", "edge_id": <circular edge ID>, "expected_mm": <twice source circle radius>, "position_mm": [x,y], "evidence": <source geometry meaning> }`. Creates an associated native diameter, verifies its underlying value before saving and after reopening. A circular boundary is not necessarily a hole diameter; document whether it represents an external cylinder, opening or step.
- `sections`: array `{ "id": "section-a", "parent": "front", "label": "A", "line_model_mm": [[x,y,z],[x,y,z]], "position_mm": [x,y], "scale": 1, "reason": <what hidden geometry it exposes> }`. Endpoints are straight cutting-line endpoints expressed in model coordinates; do not assume an arbitrary plane can be represented in the parent projection.
- `labels`: array `{ "view": "front", "edge_id": <integer from facts.edges>, "text": "A1", "offset_mm": [dx,dy] }`. Edge must have circular geometry. Its exact 3D center is projected; annotation is attached to that same edge.
- `tables`: array `{ "position_mm": [x,y], "column_widths_mm": [30,40,...], "row_height_mm": 7, "rows": [[<strings>,...], ...], "evidence": <facts and coordinate datum>, "role": "nominal_hole_schedule" }`. Position is the top-left. Equal column count in every row. Strings are literal text, so the model must check source provenance and units.
- `notes`: array `{ "position_mm": [x,y], "text": <string>, "font_mm": 3.5 }`. Position is native note anchor; actual rendered text bounds require visual review.
- `unresolved`: array of strings explaining absent engineering requirements. Missing manufacturing data does not block a nominal capability-test drawing; it blocks release claims.

Optional `output_pdf`: new `.pdf` path for one native SaveAs attempt, reported separately from drawing success. No automatic fallback or overlay.

Inspection verifies the saved model is unmodified. Execution verifies its file hash and configuration again. A changed model needs new facts and a new plan, not recycled edge IDs. Inspect/Execute restore an originally open source model's configuration; they close only models they opened. A source-assisted plan is not a blind reconstruction test.

The program refuses out-of-sheet anchors, duplicate view IDs, unresolved view/edge IDs, invalid sizes/scales, unequal table rows, short section lines, nonfinite values and existing deliverable paths. After execution it checks bounds and flags possible view overlaps. Bounds include margins and sections' annotations, so these flags need geometric/visual review. It reports `visual_review: NOT_CHECKED` and `manufacturing_release: NOT_APPROVED` until reviewed externally.
