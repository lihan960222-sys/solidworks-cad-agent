"""Offline checks: publishing results or missing source imports must fail."""
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]

class PublicationTests(unittest.TestCase):
    def test_real_git_ignore_excludes_results_but_keeps_core(self):
        paths = ['example/a.SLDPRT','example/a.SlDaSm','example/input.pdf',
                 'example/video.mp4','example/view.bmp','runs/result.json',
                 'example/assembly_original.json','example/part_specifications.json',
                 'example/mate_specifications.json','example/validation.json',
                 'runtime/com/GripperBuild.cs','docs/PROJECT_REPORT.zh-CN.md']
        result = subprocess.run(['git','check-ignore','--stdin','-z'],cwd=ROOT,
                                input=('\0'.join(paths)+'\0').encode(),capture_output=True)
        self.assertEqual(set(result.stdout.decode().rstrip('\0').split('\0')),set(paths[:10]))

    def test_publication_candidates_have_no_generated_cad_or_media(self):
        result = subprocess.run(['git','ls-files','--cached','--others','--exclude-standard','-z'],
                                cwd=ROOT,capture_output=True,check=True)
        forbidden={'.sldprt','.sldasm','.slddrw','.pdf','.png','.bmp','.jpg',
                   '.mp4','.avi','.mkv','.step','.stp','.stl','.igs','.dll','.exe','.zip','.log'}
        for value in result.stdout.decode().rstrip('\0').split('\0'):
            path=ROOT/value
            with self.subTest(path=value):
                self.assertNotIn(path.suffix.lower(),forbidden)
                self.assertNotIn(b'\x00',path.read_bytes(),'Binary content in publication candidate')

    def test_reference_sources_and_report_are_delivered(self):
        required = ['runtime/com/GripperBuild.cs','runtime/com/GripperAssemble.cs',
                    'runtime/com/GripperPrepare.cs','runtime/com/GripperValidate.cs',
                    'runtime/com/GripperAssemblyCheck.cs','runtime/com/DirectionProbe.cs',
                    'runtime/com/Invoke-Reference.ps1','docs/PROJECT_REPORT.zh-CN.md',
                    'docs/assembly-postmortem.md','docs/release-strategy.md']
        for name in required:
            with self.subTest(path=name): self.assertTrue((ROOT/name).is_file(),name)

if __name__ == '__main__': unittest.main()
