"""Small offline regression check; never opens CAD models or validates real COM."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]


def module(name):
    file = ROOT / 'scripts' / (name + '.py')
    assert file.is_file(), 'Missing implementation: ' + name
    spec = importlib.util.spec_from_file_location(name, file)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def main():
    env = module('check_environment')
    smoke = module('smoke_test')
    validator = module('validate_artifacts')
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / 'settings.env'
        path.write_text('CASES_PATH="case folder"\nSOLIDWORKS_EXE=\n', encoding='utf-8-sig')
        assert env.load_env(path)['CASES_PATH'] == 'case folder'
        for text in ('not an assignment', 'CASES_PATH=a\nCASES_PATH=b', 'UNEXPECTED=value', 'CASES_PATH="unclosed'):
            path.write_text(text, encoding='utf-8')
            try:
                env.load_env(path)
            except ValueError:
                pass
            else:
                raise AssertionError('Malformed env accepted: ' + text)
        path.write_text('SOLIDWORKS_EXE=absent.exe\nCODEX_MCP_CONFIG=absent.toml\n', encoding='utf-8')
        clean = {k: v for k, v in os.environ.items() if k not in env.KEYS}
        result = subprocess.run([sys.executable, str(ROOT / 'scripts/check_environment.py'), '--env-file', str(path)], env=clean, capture_output=True, text=True, timeout=60)
        assert result.returncode == 0, result.stdout + result.stderr
        assert '[MISSING] SolidWorks installed' in result.stdout
        assert '[MISSING] Codex MCP config' in result.stdout
        assert 'Traceback' not in result.stderr
        config = Path(folder) / 'config.toml'
        config.write_text('[mcp_servers.solidworks]\ncommand="<EXE>"\n', encoding='utf-8')
        assert env.check_codex(config)[0] is False

    # Protocol success must not hide the upstream JSON payload's ok=false.
    for payload in ({'ok':False,'message':'Feature failed'}, {'message':'no status'}):
        response = SimpleNamespace(isError=False, content=[SimpleNamespace(type='text', text=json.dumps(payload))])
        try:
            smoke.decode_response(response)
        except RuntimeError:
            pass
        else:
            raise AssertionError('Tool-level failure accepted')
    response = SimpleNamespace(isError=False, content=[SimpleNamespace(type='text', text='{"ok":true,"data":{"value":7}}')])
    assert smoke.decode_response(response) == {'value':7}
    expected_document = {'title':'NewPart1','path':'','document_type':'part'}
    assert hasattr(smoke, 'check_document'), 'Missing creation-response identity guard'
    smoke.check_document(dict(expected_document), expected_document)
    try:
        smoke.check_document(dict(expected_document, title='OtherUnsavedPart'), expected_document)
    except RuntimeError:
        pass
    else:
        raise AssertionError('Smoke adopted a different active document')

    schemas = validator.load_schemas()
    plan = {'schema_version':'1.0','part_name':'synthetic','document_type':'SLDPRT','units':'mm','features':[],'verification_targets':[],'assumptions':[],'unresolved':[]}
    f = {'id':'f1','type':'sketch','depends_on':[],'sketch_plane':'front','parameters':{},'source_view':[],'source_dimensions':[],'confidence':1,'verification_targets':[]}
    plan['features'] = [f]
    validator.validate('feature_plan', plan, schemas)
    validator.check_links(None, plan, None)
    for change in ('cycle','unknown','duplicate','assembly','confidence'):
        bad = copy.deepcopy(plan)
        feature = bad['features'][0]
        if change == 'cycle':
            feature['depends_on'] = ['f1']
        elif change == 'unknown':
            feature['depends_on'] = ['missing']
        elif change == 'duplicate':
            bad['features'].append(copy.deepcopy(f))
        elif change == 'assembly':
            feature['type'] = 'assembly_component'
        else:
            feature['confidence'] = -1
        try:
            validator.validate('feature_plan', bad, schemas)
            validator.check_links(None, bad, None)
        except (ValueError, validator.ValidationError):
            pass
        else:
            raise AssertionError('Invalid plan accepted: ' + change)

    state = {'schema_version':'1.0','case_name':'synthetic','phase':'modeling','active_document':None,'document_type':'SLDPRT','units':'mm','completed_features':[],'failed_features':[],'unresolved':[],'feature_bindings':{},'rebuild_status':'NOT_CHECKED','measurements':[],'verification_results':{'status':'NEEDS_FIX','checks':[],'problems':[{'feature_id':None,'expected':'evidence','actual':None,'reason':'Not observed','suggested_fix':'Read generated state'}]},'generated_path':None,'generated_sha256':None,'modeling_completed_at':None,'ground_truth_access':'locked'}
    validator.validate('model_state', state, schemas)
    state['ground_truth_access'] = 'allowed'
    try:
        validator.validate('model_state', state, schemas)
    except validator.ValidationError:
        pass
    else:
        raise AssertionError('Ground Truth unlocked before frozen generated artifact')

    final_plan = copy.deepcopy(plan)
    final_plan['verification_targets'] = [{'id':name,'metric':'length','expected':10,'unit':'mm','absolute_tolerance':0.01,'source_dimensions':[],'required':True} for name in ('length_x','length_y')]
    final_state = copy.deepcopy(state)
    final_state.update(phase='modeling_complete', ground_truth_access='locked', generated_path='output/generated.SLDPRT', generated_sha256='0'*64, modeling_completed_at='2026-09-12T00:00:00Z', completed_features=['f1'], feature_bindings={'f1':'Sketch1'}, rebuild_status='PASS')
    final_state['verification_results'] = {'status':'PASS','checks':[{'target_id':'length_x','status':'PASS','expected':10,'actual':10,'unit':'mm','evidence':['measurement_x'],'reason':'measured'}],'problems':[]}
    validator.validate('model_state', final_state, schemas)
    try:
        validator.check_links(None, final_plan, final_state)
    except ValueError:
        pass
    else:
        raise AssertionError('Final PASS omitted a planned verification target')

    measurement = {'id':'length','expected':10,'actual':10,'unit':'mm','absolute_tolerance':0.01,'status':'PASS','reason':'measured'}
    evaluation = {'schema_version':'1.0','case_name':'synthetic','overall_status':'PASS','generated_sha256':'0'*64,'original_sha256':'1'*64,'evaluated_at':'2026-09-12T00:00:00Z','comparison_frame':'aligned origins','bounding_box_error':{'x_mm':0,'y_mm':0,'z_mm':0},'volume_error_percent':0,'surface_area_error_percent':0,'center_of_mass_error':{'x_mm':0,'y_mm':0,'z_mm':0,'norm_mm':0},'critical_dimension_results':[measurement],'feature_results':[dict(measurement,id='body_count',unit=None)],'geometry_check':{'method':'solid_difference','status':'PASS','evidence':['synthetic test only']},'visual_check':'PASS','missing_checks':[],'notes':[]}
    validator.validate('evaluation_result', evaluation, schemas)
    for invalid in ('volume','dimension'):
        bad_evaluation = copy.deepcopy(evaluation)
        if invalid == 'volume':
            bad_evaluation['volume_error_percent'] = None
        else:
            bad_evaluation['critical_dimension_results'][0]['actual'] = None
        try:
            validator.validate('evaluation_result', bad_evaluation, schemas)
        except validator.ValidationError:
            pass
        else:
            raise AssertionError('Evaluation PASS accepted missing ' + invalid)
    print('[OK] Offline regression checks; no SolidWorks/industrial model was used')


if __name__ == '__main__':
    main()
