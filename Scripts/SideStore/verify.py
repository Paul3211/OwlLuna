"""Validate the actual device IPA; --macos also checks Apple's Mach-O platform."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import subprocess
import tempfile
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument("ipa", type=Path)
parser.add_argument("--macos", action="store_true")
args = parser.parse_args()
with zipfile.ZipFile(args.ipa) as archive:
    assert archive.testzip() is None, "ZIP CRC failure"
    names = archive.namelist()
    assert len(names) == len(set(names)), "Duplicate ZIP entries"
    for name in names:
        path = PurePosixPath(name)
        assert not path.is_absolute() and ".." not in path.parts, "Unsafe archive path"
    apps = {name.split("/")[1] for name in names if name.startswith("Payload/") and len(name.split("/")) > 2}
    assert apps == {"OwlLuna.app"}, f"Unexpected applications: {apps}"
    prefix = "Payload/OwlLuna.app/"
    assert not any(".appex/" in n or n.endswith("embedded.mobileprovision") or "_CodeSignature/" in n for n in names), "Unexpected signing material or widget"
    info = plistlib.loads(archive.read(prefix + "Info.plist"))
    assert info["CFBundlePackageType"] == "APPL"
    assert info["CFBundleIdentifier"] == "io.github.paul3211.OwlLuna"
    assert 2 in info["UIDeviceFamily"], "iPad not supported"
    assert info["CFBundleSupportedPlatforms"] == ["iPhoneOS"], "Simulator application"
    assert not info.get("OwlLunaAppGroup"), "App Group still configured"
    executable = info["CFBundleExecutable"]
    assert PurePosixPath(executable).name == executable
    binary = archive.read(prefix + executable)
    assert binary[:4] in (b"\xcf\xfa\xed\xfe", b"\xfe\xed\xfa\xcf", b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca"), "Not a Mach-O executable"
    assert len(binary) > 100_000, "Unexpectedly small executable"
    native_checks = {}
    if args.macos:
        with tempfile.TemporaryDirectory() as temp:
            archive.extractall(temp)
            app = Path(temp) / prefix
            for path in app.rglob("*"):
                if not path.is_file():
                    continue
                if b"Mach-O" not in subprocess.check_output(["file", str(path)]):
                    continue
                signed = subprocess.run(["codesign", "-d", str(path)], capture_output=True)
                assert signed.returncode != 0, f"Still signed: {path}"
                archs = subprocess.check_output(["lipo", "-archs", str(path)], text=True).strip()
                assert "arm64" in archs.split() and "x86_64" not in archs, archs
                platform = subprocess.check_output(["xcrun", "vtool", "-show-build", str(path)], text=True)
                assert re.search(r"platform\s+IOS\s", platform), f"Not an iOS device binary: {platform}"
                native_checks[str(path.relative_to(app))] = {"architectures": archs, "buildCommands": platform}
            assert executable in native_checks
    report = {
        "ipa": args.ipa.name,
        "sha256": hashlib.sha256(args.ipa.read_bytes()).hexdigest(),
        "bytes": args.ipa.stat().st_size,
        "bundleIdentifier": info["CFBundleIdentifier"],
        "version": info["CFBundleShortVersionString"],
        "build": info["CFBundleVersion"],
        "minimumOS": info["MinimumOSVersion"],
        "sdk": info.get("DTSDKName"),
        "xcode": info.get("DTXcode"),
        "commit": os.environ.get("GITHUB_SHA"),
        "runURL": (f"https://github.com/{os.environ['GITHUB_REPOSITORY']}/actions/runs/{os.environ['GITHUB_RUN_ID']}" if "GITHUB_RUN_ID" in os.environ else None),
        "checks": {"zipCRC": True, "devicePlatform": True, "iPad": True, "noWidgets": True, "noProvisioningProfile": True, "nativeUnsignedMachO": bool(native_checks)},
        "binaries": native_checks,
        "deviceInstallationTested": False,
    }
    args.ipa.with_suffix(".verification.json").write_text(json.dumps(report, indent=2) + "\n")
    args.ipa.with_suffix(".sha256").write_text(f"{report['sha256']}  {args.ipa.name}\n")
    print(json.dumps(report, indent=2))
