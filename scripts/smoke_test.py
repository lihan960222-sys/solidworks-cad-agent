"""Minimal MCP client: create a disposable 20 x 10 x 5 mm Part, never open originals."""
import argparse
import asyncio
from datetime import timedelta
import json
from pathlib import Path
import sys
import uuid

from check_environment import ROOT, executable, local_path, settings_from


def decode_response(response):
    if response.isError:
        raise RuntimeError('MCP protocol/tool error')
    for block in response.content:
        if block.type == 'text':
            payload = json.loads(block.text)
            if not isinstance(payload, dict) or payload.get('ok') is not True:
                raise RuntimeError('SolidWorks tool failed: ' + str(payload.get('message', 'missing ok=true') if isinstance(payload, dict) else 'invalid payload'))
            return payload.get('data', {})
    raise RuntimeError('No tool result returned')


def check_document(current, created):
    if not created.get('title') or created.get('path') or created.get('document_type') != 'part':
        raise RuntimeError('Creation did not return a new unsaved Part identity')
    if any(current.get(key) != created.get(key) for key in ('title', 'path', 'document_type')):
        raise RuntimeError('Active document changed; stopped before modifying it')


async def build(settings, destination):
    from mcp import ClientSession, StdioServerParameters
    from mcp.client.stdio import stdio_client
    from jsonschema import Draft202012Validator

    command = executable(settings.get('SOLIDWORKS_MCP_EXECUTABLE') or 'solidworks-mcp')
    if not command:
        raise RuntimeError('Set SOLIDWORKS_MCP_EXECUTABLE to the installed external CLI')
    env = {'SW_MCP_OUTPUT_ROOT': str(destination.parent)}
    if settings.get('SW_MCP_TEMPLATE_DIR'):
        env['SW_MCP_TEMPLATE_DIR'] = str(local_path(settings['SW_MCP_TEMPLATE_DIR']))
    params = StdioServerParameters(command=command, args=[], env=env)
    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write, read_timeout_seconds=timedelta(seconds=45)) as session:
            await session.initialize()
            tools = {}
            cursor = None
            while True:
                listing = await session.list_tools(cursor=cursor)
                tools.update({tool.name: tool for tool in listing.tools})
                cursor = listing.nextCursor
                if not cursor:
                    break
            steps = [
                ('create_sketch', {'plane':'front', 'name':'SmokeBase'}),
                ('draw_rectangle', {'x1_mm':0, 'y1_mm':0, 'x2_mm':20, 'y2_mm':10}),
                ('close_sketch', {}),
                ('boss_extrude', {'depth_mm':5, 'sketch_name':'SmokeBase', 'name':'SmokeExtrude'}),
                ('rebuild_document', {}),
                ('get_bounding_box', {}),
                ('save_document', {'path':str(destination), 'overwrite':False}),
            ]
            required = {'solidworks_status', 'get_active_document_info', 'create_new_document'} | {name for name, _ in steps}
            if required - tools.keys():
                raise RuntimeError('Upstream is missing tools: ' + ', '.join(sorted(required - tools.keys())))
            # Reject changed signatures before making any part or feature.
            for name, arguments in [('create_new_document', {'kind':'part'}), *steps]:
                Draft202012Validator(tools[name].inputSchema).validate(arguments)

            async def call(name, arguments=None):
                arguments = arguments or {}
                Draft202012Validator(tools[name].inputSchema).validate(arguments)
                return decode_response(await session.call_tool(name, arguments))

            status = await call('solidworks_status')
            if status.get('document_count') != 0:
                raise RuntimeError('Close all documents before the disposable smoke test; no existing model will be used')
            print('[OK] SolidWorks MCP response')
            created = (await call('create_new_document', {'kind':'part'})).get('document', {})
            info = (await call('get_active_document_info')).get('document', {})
            check_document(info, created)
            for name, arguments in steps:
                current = (await call('get_active_document_info')).get('document', {})
                check_document(current, created)
                data = await call(name, arguments)
                if name == 'get_bounding_box':
                    size = data.get('size_mm', [])
                    if len(size) != 3 or any(abs(actual - expected) > 0.01 for actual, expected in zip(sorted(size), [5, 10, 20])):
                        raise RuntimeError('Unexpected smoke bounding box: ' + str(size))
                print('[OK] ' + name)
            final = (await call('get_active_document_info')).get('document', {})
            saved = Path(final.get('path') or '').resolve()
            if saved != destination.resolve() or final.get('dirty') is not False or not destination.is_file() or destination.stat().st_size == 0:
                raise RuntimeError('Save was not confirmed in both SolidWorks and the filesystem')
            print('[OK] Smoke SLDPRT saved: ' + str(destination))
            print('[INFO] Connectivity/basic geometry only; sketch fully-defined status and industrial reconstruction were not tested.')


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--env-file', type=Path, default=ROOT / 'config/solidworks.local.env')
    args = parser.parse_args(argv)
    if sys.platform != 'win32':
        print('[MISSING] SolidWorks COM requires Windows; no model created')
        return 1
    try:
        settings = settings_from(args.env_file)
        import pythoncom
        import win32com.client
        pythoncom.CoInitialize()
        try:
            try:
                app = win32com.client.GetActiveObject('SldWorks.Application')
            except Exception:
                print('[MISSING] SolidWorks running/COM - install and start SolidWorks in this desktop session first')
                return 1
            print('[OK] Running SolidWorks Application connected through COM')
            # Only test presence; do not inspect an existing document's geometry/metadata.
            if app.ActiveDoc is not None:
                print('[BLOCKED] Active Document exists. Close all documents before running this isolated smoke test.')
                return 1
            print('[OK] Active Document is empty')
            del app
        finally:
            pythoncom.CoUninitialize()
        base = settings.get('SMOKE_OUTPUT_ROOT')
        if not base:
            raise RuntimeError('Set SMOKE_OUTPUT_ROOT to a disposable local folder')
        folder = local_path(base).resolve() / ('smoke-' + uuid.uuid4().hex)
        if any(part.lower() in ('ground_truth', 'input') for part in folder.parts):
            raise RuntimeError('Smoke output cannot be inside ground_truth or input')
        folder.mkdir(parents=True, exist_ok=False)
        asyncio.run(build(settings, folder / 'smoke.SLDPRT'))
        return 0
    except ImportError as error:
        print('[MISSING] Python dependency - ' + str(error) + '; install requirements.txt in the selected venv')
    except Exception as error:
        print('[FAILED] Smoke test - ' + str(error))
        print('[INFO] Start SolidWorks first and inspect modal dialogs. Partial new geometry may remain; do not blindly retry.')
    return 1


if __name__ == '__main__':
    sys.exit(main())
