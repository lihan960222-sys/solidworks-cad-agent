"""Schema and reference checks for JSON artifacts; never reads SLDPRT files."""
import argparse
from graphlib import CycleError, TopologicalSorter
import json
from pathlib import Path
import sys

from jsonschema import Draft202012Validator, FormatChecker, ValidationError

ROOT = Path(__file__).resolve().parents[1]
NAMES = ('drawing_spec', 'feature_plan', 'model_state', 'evaluation_result')


def read_json(path):
    if Path(path).suffix.lower() != '.json':
        raise ValueError('Only JSON artifacts may be validated; do not provide CAD files')
    def invalid_constant(value):
        raise ValueError('Non-finite JSON number: ' + value)
    return json.loads(Path(path).read_text(encoding='utf-8-sig'), parse_constant=invalid_constant)


def load_schemas():
    schemas = {name: read_json(ROOT / 'schemas' / (name + '.json')) for name in NAMES}
    for schema in schemas.values():
        Draft202012Validator.check_schema(schema)
    return schemas


def validate(name, value, schemas):
    Draft202012Validator(schemas[name], format_checker=FormatChecker()).validate(value)


def ids(records, label):
    names = [item['id'] for item in records]
    if len(names) != len(set(names)):
        raise ValueError('Duplicate IDs in ' + label)
    return set(names)


def check_links(drawing, plan, state):
    if drawing is not None:
        records = [item for value in drawing.values() if isinstance(value, list) for item in value]
        ids(records, 'DrawingSpec')
        views = ids(drawing['views'], 'views')
        dimensions = ids(drawing['dimensions'], 'dimensions')
        for item in records:
            sources = item.get('source_view', [])
            if isinstance(sources, str):
                sources = [sources]
            if set(sources) - views or set(item.get('source_dimensions', [])) - dimensions or set(item.get('related_views', [])) - views:
                raise ValueError('Unknown DrawingSpec source on ' + item['id'])
    if plan is None:
        return
    features = plan['features']
    feature_ids = ids(features, 'FeaturePlan')
    graph = {item['id']: item['depends_on'] for item in features}
    if any(set(dependencies) - feature_ids for dependencies in graph.values()):
        raise ValueError('Unknown feature dependency')
    try:
        tuple(TopologicalSorter(graph).static_order())
    except CycleError as error:
        raise ValueError('Feature dependency cycle') from error
    targets = plan['verification_targets'] + [target for feature in features for target in feature['verification_targets']]
    target_ids = ids(targets, 'verification targets')
    if drawing is not None:
        if drawing['units'] is None:
            raise ValueError('Drawing units must be resolved before planning')
        for item in features + targets + plan['assumptions']:
            if set(item.get('source_view', [])) - views or set(item.get('source_dimensions', [])) - dimensions:
                raise ValueError('Unknown planning source on ' + item['id'])
    if state is None:
        return
    completed = set(state['completed_features'])
    failed = ids(state['failed_features'], 'failed features')
    if (completed | failed) - feature_ids or completed & failed:
        raise ValueError('Unknown/conflicting ModelState feature IDs')
    if set(state['feature_bindings']) - feature_ids:
        raise ValueError('Unknown feature binding')
    for done in completed:
        if set(graph[done]) - completed:
            raise ValueError('Completed feature has incomplete dependencies')
    checks = state['verification_results']['checks']
    checked = [check['target_id'] for check in checks]
    if len(checked) != len(set(checked)) or set(checked) - target_ids:
        raise ValueError('Unknown/duplicate verification target in state')
    if state['phase'] in ('modeling_complete', 'evaluation', 'evaluated'):
        if set(checked) != target_ids:
            raise ValueError('Final state must report every planned verification target')
        if not features or completed != feature_ids or failed or set(state['feature_bindings']) != completed:
            raise ValueError('Frozen model requires all planned features with bindings and no failed features')
        if any(item['blocking'] for item in plan['unresolved'] + state['unresolved']):
            raise ValueError('Frozen model still has blocking unresolved issues')
        if any(not item['approved'] for item in plan['assumptions']):
            raise ValueError('Frozen model still has unapproved assumptions')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('drawing', 'plan', 'state', 'evaluation'):
        parser.add_argument('--' + name, type=Path)
    args = parser.parse_args()
    try:
        schemas = load_schemas()
        files = (args.drawing, args.plan, args.state, args.evaluation)
        docs = [read_json(path) if path else None for path in files]
        for name, doc in zip(NAMES, docs):
            if doc is not None:
                validate(name, doc, schemas)
        check_links(*docs[:3])
        if docs[3] is not None and docs[2] is not None:
            if docs[2]['ground_truth_access'] != 'allowed' or docs[3]['generated_sha256'] != docs[2]['generated_sha256']:
                raise ValueError('Evaluation lacks authorized phase/frozen generated hash')
        print('[OK] JSON Schemas and supplied artifact structure; no geometry or file-access isolation was verified')
        return 0
    except (OSError, ValueError, ValidationError) as error:
        print('[INVALID] ' + str(error))
        return 1


if __name__ == '__main__':
    sys.exit(main())
