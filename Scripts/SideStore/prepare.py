"""Detach the widget dependency for this build; leave its source and target intact."""
from pathlib import Path
import re

project = Path("OwlLuna.xcodeproj/project.pbxproj")
text = project.read_text()
target_id = "96D9BDAE2C13E3AC00726199"
pattern = rf"(\t\t{target_id} /\* OwlLuna \*/ = {{\n)(.*?)(\n\t\t}};)"
match = re.search(pattern, text, re.S)
if not match:
    raise SystemExit("Upstream app target changed; inspect the project before updating this script.")
body = match[2]
for line in (
    "\t\t\t\t57A1DE70000000000000000B /* Embed Foundation Extensions */,\n",
    "\t\t\t\t57A1DE70000000000000000D /* PBXTargetDependency */,\n",
):
    if body.count(line) != 1:
        raise SystemExit(f"Expected one app reference: {line.strip()}")
    body = body.replace(line, "")
project.write_text(text[:match.start(2)] + body + text[match.end(2):])
print("App target detached from widget build and embedding; original widget target retained.")
