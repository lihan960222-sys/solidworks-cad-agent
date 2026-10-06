# Integrated manufacturing-draft profile

Apply this profile to the integrated 3D-to-manufacturing-draft workflow. Preserve other explicitly requested capability tests or authorized legacy batch workflows. These are confirmed product requirements, not a claim that all required executor operations already exist.

## Input and engineering information

- First stage: one saved native SOLIDWORKS part per request. Support for imported STEP/B-rep is a later extension; batch generation and assemblies are outside this first-stage acceptance scope.
- Deliver a manufacturing-drawing draft for an engineer to review. Express all reliably determinable geometry; list missing manufacturing requirements and unsupported annotations explicitly.
- Reuse existing model dimensions, PMI and specified drafting rules. Use DimXpert where a tested operation is available. Do not generate guessed tolerances, functional datum schemes, thread classes or roughness values. Missing requirements belong in a consolidated review list.
- The drawing skill is the planning/verification entry point. The automation-port skill is a candidate source of reusable native model-dimension import operations; this profile does not establish an integrated dependency or certify that import as a DimXpert operation.

## Sheet, views and layout references

- Exactly one sheet, selected from A4, A3 or A2. Choose orientation, scale and positions using projected model extent plus annotation/table space and the usable area outside the border/title block. Keep text and arrows readable at the intended print size.
- Select informative views by feature coverage rather than requiring a fixed three-view set. Add supported sections/opposite views where needed; detail views remain an implementation gap. Follow the selected template's projection convention and verify actual view placement, alignment and projection identification agree; do not infer projection from the visual style alone.
- If coverage and readability cannot both fit on one sheet up to A2 after deliberate layout repairs, report a layout failure for manual handling. Do not silently add sheets, enlarge beyond A2, omit dimensions or shrink text to force a successful status. Preserve the failed draft and diagnostics.
- Use part-family layout patterns (such as plate, shaft or bracket) with a consistent annotation style. Select and summarize suitable styles from the supplied drawing library. The migration-skill drawing is an additional visual reference, not a fixed style constraint. No particular 45-degree leader or angle-dimension arrangement is required; choose clear annotation placement while correcting view sizing and missing feature definitions.
- Reference library: https://www.innerscene.com/tools/library/drawings/format/dwg . Screen mechanical, dimensioned and title-block examples for relevance. A catalog label does not establish layout quality; inspect actual geometry and annotations before adopting a reference.
- Extract border/title-block regions, view proportions/alignment, view spacing, dimension tiers, leader placement, text/arrow/line styles and whitespace into configurable rules. A DWG is reference material, not a directly usable `.drwdot`; reconstruct or adapt an actual SOLIDWORKS template and layout plan.
- Learn layout/style only. Model-specific geometry, dimension values, tolerances and manufacturing notes must come from the authorized model or specified rules, not the reference drawing.

## Dense dimensions and tables

Tables are explicitly allowed when dimensions are numerous, heterogeneous or visually cluttered. Choose on-view dimensions, a table, or a mixed presentation according to coverage and readability; do not enforce a blanket preference against tables or invent a fixed quantity threshold.

For a table to replace repeated on-view information:

- Give each feature/group a unique visible identifier and an unambiguous row mapping. Include units and any coordinate origin/axes needed to interpret position values.
- Preserve the required size, location, count, depth and through/blind definition. Use thread data only where supplied by the model or engineering rules. List unknown fields as unresolved.
- Check correspondence against model facts and avoid contradictory duplication between the table and views. Keep sections and on-view dimensions needed to understand shape and orientation.
- Treat a native general table as a general table, not an associative Hole Table. Its rows are literal supplied data and need source validation.
- The v1 executable plan currently supports general tables with `role: nominal_hole_schedule`. Other feature-table forms need a validated reusable extension; do not send unsupported roles or fields to the runner. The separate layout runner does not create tables.

## Delivery and verification

Target flow: authorized 3D model -> existing/defined dimensions and PMI -> internal native drawing with complete layout -> DWG -> PDF/PNG -> output checks. Complete layout in SOLIDWORKS; DWG is the principal editable exchange deliverable, PDF is the review artifact, and PNG is a preview. A saved SLDDRW may remain an internal verification artifact.

The current runner requires `output_drawing` as a new SLDDRW path and saves/reopens that file. Retain this verified path until a replacement is implemented and tested. It has no bundled DWG export/reopen pipeline; the DWG-first delivery and downstream conversion are requirements awaiting implementation. Do not claim that a temporary unsaved drawing or a DWG has passed the existing native reopen checks.

For every generated draft, reconcile a feature-to-view/dimension/callout/table-row checklist, expected values, association/dangling state, duplicate/conflicting annotations and rendered legibility. Inspect text, symbols, leaders, linework, table cells, title-block conflicts and view/annotation overlaps. Source integrity and native persistence remain mandatory. Planned DWG validation must inspect the exported file itself; native success does not certify export fidelity or editability.

Development acceptance additionally includes representative blind reconstruction from the frozen final review drawing. The fresh reconstruction worker receives only that drawing plus generic tooling; source geometry is withheld until its result is frozen. This is not required for every ordinary generation and does not certify manufacturing tolerances. See [native-layout.md](native-layout.md) for isolation and comparison rules.

## Implementation status

Implemented foundations: model geometry inspection; explicit template/sheet/view/scale planning; standard views and straight full sections; supported associated dimensions; attached labels/general tables; dimension selection and row placement; source integrity and SLDDRW save/reopen checks; native PDF routes with documented limits.

Not yet integrated: DimXpert Auto Dimension Scheme and PMI transfer; template/reference extraction; automatic paper/scale/view-layout selection; general text/leader collision resolution; native Auto Arrange Dimensions; detail views; DWG export/reopen and DWG-to-PDF/PNG validation. Existing arbitrary feature-dimension and GD&T limits still apply.

Annotation style may be derived from suitable library references without a fixed 45-degree constraint. Projection follows the selected template and must be checked against the actual layout. These decisions do not remove the feature-completeness, readability, single-sheet or A2-maximum requirements.
