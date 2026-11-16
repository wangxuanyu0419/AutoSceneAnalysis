"""Exercise the actual offline report controls in an isolated headless Chrome profile."""
import argparse
import json
import os
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CHECK = """<script>
try {
 const os=document.querySelectorAll('#objects button');
 if(!os.length)throw new Error('No object buttons');
 os[0].click();
 const as=document.querySelectorAll('#actions button');
 if(!as.length)throw new Error('No action buttons');
 as[0].click();
 const s=JSON.parse(document.getElementById('selection').textContent);
 if(s.object_one_hot.length!==os.length)throw new Error('Wrong target-vector dimension');
 if(s.object_one_hot.reduce((a,b)=>a+b,0)!==1)throw new Error('Wrong target vector');
 if(s.action_one_hot.reduce((a,b)=>a+b,0)!==1)throw new Error('Wrong action vector');
 if(s.executes_robot_motion!==false)throw new Error('Unexpected actuation flag');
 document.getElementById('cancel').click();
 if(JSON.parse(document.getElementById('selection').textContent).control!=='idle')throw new Error('Cancel failed');
 if(document.querySelectorAll('#actions button').length)throw new Error('Stale action buttons');
 os[0].click();document.querySelector('#actions button').click();
 document.documentElement.dataset.qa='passed';
} catch(error) { document.documentElement.dataset.qa='failed';document.body.append(String(error)); }
</script>"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("report", type=Path)
    parser.add_argument("--browser", type=Path, required=True)
    args = parser.parse_args()
    report = args.report.resolve()
    qa_file = report.with_name(".browser-qa.html")
    qa_file.write_text(report.read_text(encoding="utf-8").replace("</html>", CHECK + "</html>"), encoding="utf-8")
    profile = ROOT / ".cache" / "browser-qa"
    screenshot = report.with_name("browser-preview.png")
    try:
        result = subprocess.run([
            str(args.browser), "--headless", "--disable-gpu", "--disable-extensions",
            "--disable-background-networking", "--no-first-run", "--no-default-browser-check",
            f"--user-data-dir={profile}", "--window-size=1440,1200",
            "--hide-scrollbars", "--dump-dom", f"--screenshot={screenshot}", qa_file.as_uri()
        ], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60,
           creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        passed = 'data-qa="passed"' in result.stdout and screenshot.exists()
        record = {"controls_passed": passed, "browser_exit_code": result.returncode,
                  "checks": ["object selection", "conditional action selection", "two one-hot vectors", "cancel to idle"],
                  "screenshot": screenshot.name}
        (report.parent / "browser-qa.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(record, indent=2))
        if not passed:
            print(result.stderr[-2000:])
            raise SystemExit(1)
    finally:
        qa_file.unlink(missing_ok=True)


if __name__ == "__main__":
    main()
