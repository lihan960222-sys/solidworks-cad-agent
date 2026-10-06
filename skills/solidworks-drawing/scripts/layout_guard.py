"""Prepare declarative native annotation layouts and audit saved/reopened results.

Layout spans are AI-authored sheet coordinates grounded in drawing geometry.
This checks those supplied spans, not automatic extraction of witness endpoints.
"""
import argparse, json, math
from pathlib import Path

def require(ok, message):
    if not ok: raise ValueError(message)

def place(item, rows, sheet):
    layout=item.get('layout')
    if layout:
        axis=layout['axis']; require(axis in (0,1),'axis must be 0 or 1')
        lo,hi=sorted(layout['span_mm'])
        require(math.isfinite(lo) and math.isfinite(hi) and hi>lo,'invalid witness span')
        if 'position_mm' not in item:
            if 'row_mm' in layout: row=layout['row_mm']
            else:
                key=(layout['group'],axis,layout['base_mm'],layout['sign'])
                tracks=rows.setdefault(key,[]); gap=layout.get('gap_mm',8); pad=layout.get('padding_mm',3)
                require(gap>0 and pad>=0 and layout['sign'] in (-1,1),'invalid row spacing')
                track=next((i for i,t in enumerate(tracks) if all(hi+pad<a or lo-pad>b for a,b in t)),len(tracks))
                if track==len(tracks): tracks.append([])
                tracks[track].append((lo,hi)); row=layout['base_mm']+layout['sign']*gap*(track+1)
            pos=[0,0];pos[axis]=(lo+hi)/2;pos[1-axis]=row;item['position_mm']=pos
        # Outside placement can be legitimate, but must carry explicit reviewed justification.
        require(lo-0.5<=item['position_mm'][axis]<=hi+0.5 or bool(layout.get('outside_span_reason')),'text outside witness span')
    if 'position_mm' in item:
        p=item['position_mm'];require(len(p)==2 and all(math.isfinite(v) and 0<=v<=s for v,s in zip(p,sheet)),'annotation outside sheet')

def prepare(plan):
    rows={};sheet=plan['sheet_mm']; seen=set()
    for field in ('keep','linear','leaders','note_moves','view_moves'):
        for item in plan.get(field,[]):
            if field=='keep':
                key=(item['view'],item['name']);require(key not in seen,'duplicate retained dimension');seen.add(key)
            place(item,rows,sheet)
    for field in ('source','facts','input','output','report','pdf','pdf_report'):
        require(Path(plan[field]).is_absolute(),'absolute paths required: '+field)
        plan[field]=str(Path(plan[field]))  # Native Windows COM calls may reject forward slashes.
    return plan

def audit(report,plan):
    require(not report['errors'],'native operation errors')
    def inventory(stage):
        return {(v['view'],d['name'].rsplit('@',1)[0]):(round(d['value_si'],10),d['prefix'],d['suffix'],d['dangling'],tuple(round(x,7) for x in d['position'])) for v in report[stage] for d in v['dimensions']}
    require(inventory('before')==inventory('after'),'save/reopen changed dimensions, text or position')
    def notes(stage):
        return sorted((v['view'],n['text'].replace('\r',''),tuple(round(x,7) for x in n['position']),n['dangling']) for v in report[stage] for n in v['note_texts'])
    require(notes('before')==notes('after'),'save/reopen changed notes')
    for v in report['after']:require(v['dangling']==0,'dangling annotation')
    dims={(v['view'],d['name'].rsplit('@',1)[0]):d for v in report['after'] for d in v['dimensions']}
    for k in plan['keep']:
        d=dims[(k['view'],k['name'])]
        require(abs(d['value_si']-k['expected_si'])<1e-8,'retained value mismatch')
        for text in ('prefix','suffix'):
            if text in k:require(d[text]==k[text],'reopened '+text+' missing')
        if 'position_mm' in k:require(all(abs(a*1000-b)<.1 for a,b in zip(d['position'],k['position_mm'])),'reopened position mismatch')
    return {'status':'PASS','dimension_count':len(dims),'span_checks':sum('layout' in k for k in plan['keep']), 'visual_review':'REQUIRED','feature_completeness':'REQUIRES_ENGINEERING_REVIEW'}

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('mode',choices=['prepare','audit']);ap.add_argument('plan');ap.add_argument('output');args=ap.parse_args()
    plan=json.loads(Path(args.plan).read_text(encoding='utf-8-sig'))
    result=prepare(plan) if args.mode=='prepare' else audit(json.loads(Path(plan['report']).read_text(encoding='utf-8-sig')),plan)
    out=Path(args.output);require(not out.exists(),'refusing existing output');out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
