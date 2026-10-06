"""Combine two native SolidWorks output channels; never draw annotations."""
import argparse,json
from pathlib import Path
from pypdf import PdfReader,PdfWriter,Transformation

p=argparse.ArgumentParser()
p.add_argument('annotations');p.add_argument('geometry');p.add_argument('output')
a=p.parse_args();out=Path(a.output)
if not out.is_absolute() or out.exists():raise SystemExit('New absolute output required')
annotations=PdfReader(a.annotations);geometry=PdfReader(a.geometry)
if len(annotations.pages)!=1 or len(geometry.pages)!=1:raise SystemExit('Single sheet only')
page=annotations.pages[0];geo=geometry.pages[0]
aw,ah=float(page.mediabox.width),float(page.mediabox.height)
gw,gh=float(geo.mediabox.width),float(geo.mediabox.height)
if abs(aw-gw)>0.1 or abs(ah-gh)>0.1:raise SystemExit('Paper sizes differ; refusing misregistration')
# Microsoft paper sizes round to hundredths of an inch. Correct only that tiny
# rounding difference; retain the same sheet origin and 100% printing scale.
geo.add_transformation(Transformation().scale(aw/gw,ah/gh))
page.merge_page(geo,over=True)
writer=PdfWriter();writer.add_page(page)
writer.add_metadata({'/Title':'Native SolidWorks drawing','/Subject':'SolidWorks SaveAs geometry + PrintOut4 native annotations; composed PDF'})
out.parent.mkdir(parents=True,exist_ok=True)
with out.open('xb') as f:writer.write(f)
r=PdfReader(out);text=r.pages[0].extract_text()
if not text.strip():raise SystemExit('Missing annotation text')
print(json.dumps({'status':'COMPOSED_NATIVE_OUTPUT_REVIEW_REQUIRED','pages':len(r.pages),'width_mm':aw*25.4/72,'height_mm':ah*25.4/72,'text_characters':len(text),'images':len(r.pages[0].images)},ensure_ascii=False))
